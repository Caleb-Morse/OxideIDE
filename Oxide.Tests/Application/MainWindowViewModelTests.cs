using Oxide.App.ViewModels;
using Oxide.App.Settings;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Application;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task Open_workspace_projects_real_states_problems_and_selection()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"");
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=\"STATE_ONE\" history={ owner=USA } provinces={ 10 11 } }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=\"STATE_TWO\" history={ owner=ZZZ } }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service);
        viewModel.GameRootPath = fixture.GameRoot;

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Workspace, viewModel.Screen);
        Assert.Equal(2, viewModel.States.Count);
        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Contains(viewModel.Problems, problem => problem.Code == "OXIDE4006" && problem.StateId == 2);
        Assert.Contains("2 states", viewModel.WorkspaceSummary, StringComparison.Ordinal);
        Assert.Contains(" ms", viewModel.StatusSummary, StringComparison.Ordinal);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Search_filters_by_id_name_owner_category_and_source()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"");
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=\"ALPHA\" state_category=city history={ owner=USA } }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=\"BETA\" state_category=rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        viewModel.SearchText = "rural";
        Assert.Equal(2, Assert.Single(viewModel.States).Id);

        viewModel.SearchText = "USA";
        Assert.Equal(1, Assert.Single(viewModel.States).Id);

        viewModel.SearchText = "One.txt";
        Assert.Equal(1, Assert.Single(viewModel.States).Id);
    }

    [Fact]
    public async Task Language_switch_reprojects_names_search_and_sort_without_reloading_the_workspace()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=STATE_TWO }");
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n STATE_ONE:0 \"Zulu\"\n STATE_TWO:0 \"Alpha\"\n");
        fixture.WriteGameFile("localisation/russian/names_l_russian.yml", "l_russian:\n STATE_ONE:0 \"Альфа\"\n STATE_TWO:0 \"Янтарь\"\n");
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings()));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service, settings) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var published = service.CurrentSnapshot;
        viewModel.SelectedState = viewModel.States.Single(state => state.Id == 1);
        viewModel.SearchText = "Zulu";

        await viewModel.ChangeLanguageAsync("russian");

        Assert.Same(published, service.CurrentSnapshot);
        Assert.Equal("russian", viewModel.SelectedLanguage);
        Assert.Equal([1, 2], viewModel.States.Select(state => state.Id));
        Assert.Equal("Альфа", viewModel.SelectedState?.DisplayName);
        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("russian", settings.Saved?.PreferredLanguage);
        viewModel.SearchText = "STATE_TWO";
        Assert.Equal(2, Assert.Single(viewModel.States).Id);
    }

    [Fact]
    public async Task State_and_country_names_share_exact_and_english_fallback_presentation()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TST=\"countries/Test.txt\"");
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE history={ owner=TST } }");
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n STATE_ONE:0 \"English State\"\n TST:0 \"Test Country\"\n");
        fixture.WriteGameFile("localisation/russian/names_l_russian.yml", "l_russian:\n STATE_ONE:0 \"Русский штат\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        await viewModel.ChangeLanguageAsync("russian");

        var state = Assert.Single(viewModel.States);
        var country = Assert.Single(viewModel.Countries);
        Assert.Equal("Русский штат", state.DisplayName);
        Assert.Equal("Exact russian match", state.NameStatus);
        Assert.Equal("Test Country", country.DisplayName);
        Assert.Equal("English fallback for russian", country.NameStatus);
        Assert.Equal("Test Country · TST", state.Owner);
        Assert.EndsWith("names_l_english.yml", country.LocalisationSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task State_region_presentation_is_language_aware_searchable_and_source_backed()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/1-One.txt",
            "strategic_region={ id=1 name=REGION_ONE provinces={ 10 11 30 } }");
        fixture.WriteGameFile(
            "map/strategicregions/2-Two.txt",
            "strategic_region={ id=2 name=REGION_TWO provinces={ 20 30 } }");
        fixture.WriteGameFile("history/states/1-Single.txt", "state={ id=1 provinces={ 10 11 } }");
        fixture.WriteGameFile("history/states/2-Partial.txt", "state={ id=2 provinces={ 10 99 } }");
        fixture.WriteGameFile("history/states/3-Split.txt", "state={ id=3 provinces={ 10 20 } }");
        fixture.WriteGameFile("history/states/4-Missing.txt", "state={ id=4 provinces={ 99 } }");
        fixture.WriteGameFile("history/states/5-Ambiguous.txt", "state={ id=5 provinces={ 30 } }");
        fixture.WriteGameFile(
            "localisation/english/regions_l_english.yml",
            "l_english:\n REGION_ONE:0 \"Northern Reach\"\n REGION_TWO:0 \"Southern Reach\"\n");
        fixture.WriteGameFile(
            "localisation/russian/regions_l_russian.yml",
            "l_russian:\n REGION_ONE:0 \"Северный край\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var published = service.CurrentSnapshot;

        var single = viewModel.States.Single(state => state.Id == 1);
        Assert.Equal("Northern Reach · Region 1", single.StrategicRegion);
        Assert.Equal("Single region", single.StrategicRegionStatus);
        Assert.Equal(2, single.StrategicRegionEvidence.Length);
        Assert.All(single.StrategicRegionEvidence, evidence =>
        {
            Assert.Contains("history/states", evidence.StateSource, StringComparison.Ordinal);
            Assert.Contains("map/strategicregions", evidence.RegionSources, StringComparison.Ordinal);
            Assert.Contains("line", evidence.StateSource, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("line", evidence.RegionSources, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal("Partial coverage", viewModel.States.Single(state => state.Id == 2).StrategicRegionStatus);
        Assert.Equal("Split across regions", viewModel.States.Single(state => state.Id == 3).StrategicRegionStatus);
        Assert.Equal("Missing membership", viewModel.States.Single(state => state.Id == 4).StrategicRegionStatus);
        Assert.Equal("Ambiguous membership", viewModel.States.Single(state => state.Id == 5).StrategicRegionStatus);

        viewModel.SearchText = "Northern Reach";
        Assert.Equal([1, 2, 3], viewModel.States.Select(state => state.Id).Order());
        viewModel.SearchText = string.Empty;

        await viewModel.ChangeLanguageAsync("russian");

        Assert.Same(published, service.CurrentSnapshot);
        Assert.Equal("Северный край · Region 1", viewModel.States.Single(state => state.Id == 1).StrategicRegion);
        Assert.Contains("Southern Reach · Region 2", viewModel.States.Single(state => state.Id == 3).StrategicRegion,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Country_browser_searches_names_and_tags_and_links_owned_states()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "AAA=\"countries/A.txt\" BBB=\"countries/B.txt\"");
        fixture.WriteGameFile("history/states/7-Seven.txt", "state={ id=7 history={ owner=BBB add_core_of=AAA } }");
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n AAA:0 \"Amber Republic\"\n BBB:0 \"Blue Kingdom\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        viewModel.CountrySearchText = "Blue Kingdom";
        var country = Assert.Single(viewModel.Countries);
        Assert.Equal("BBB", country.Tag);
        Assert.Equal([7], country.OwnedStateIds.ToArray());
        Assert.Empty(country.CoreStateIds);

        viewModel.CountrySearchText = "AAA";
        Assert.Equal([7], Assert.Single(viewModel.Countries).CoreStateIds.ToArray());
    }

    [Fact]
    public async Task Missing_and_ambiguous_names_keep_stable_identifiers_visible()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=MISSING }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=DUP }");
        fixture.WriteGameFile("localisation/english/a_l_english.yml", "l_english:\n DUP:0 \"One\"\n");
        fixture.WriteGameFile("localisation/english/b_l_english.yml", "l_english:\n DUP:0 \"Two\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        Assert.Equal("State 1", viewModel.States.Single(state => state.Id == 1).DisplayName);
        Assert.Equal("Missing localisation", viewModel.States.Single(state => state.Id == 1).NameStatus);
        Assert.Equal("State 2", viewModel.States.Single(state => state.Id == 2).DisplayName);
        Assert.Equal("Ambiguous localisation", viewModel.States.Single(state => state.Id == 2).NameStatus);
    }

    [Fact]
    public async Task Simplified_chinese_names_flow_through_the_application_projection()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE }");
        fixture.WriteGameFile("localisation/simp_chinese/names_l_simp_chinese.yml", "l_simp_chinese:\n STATE_ONE:0 \"钢铁海岸\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        Assert.Equal("simp_chinese", viewModel.SelectedLanguage);
        Assert.Equal("钢铁海岸", Assert.Single(viewModel.States).DisplayName);
        Assert.Equal("Exact simp_chinese match", Assert.Single(viewModel.States).NameStatus);
    }

    [Fact]
    public async Task Unavailable_preference_is_preserved_and_reactivates_after_reload()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE }");
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n STATE_ONE:0 \"English\"\n");
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings(
            PreferredLanguage: "russian")));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service, settings);
        await viewModel.InitializeAsync();
        viewModel.GameRootPath = fixture.GameRoot;
        await viewModel.OpenWorkspaceAsync();

        Assert.Equal("russian", viewModel.PreferredLanguage);
        Assert.Equal("english", viewModel.SelectedLanguage);
        Assert.Equal("russian", settings.Saved?.PreferredLanguage);

        fixture.WriteGameFile("localisation/russian/names_l_russian.yml", "l_russian:\n STATE_ONE:0 \"Русский\"\n");
        await viewModel.ReloadAsync();

        Assert.Equal("russian", viewModel.PreferredLanguage);
        Assert.Equal("russian", viewModel.SelectedLanguage);
        Assert.Equal("Русский", Assert.Single(viewModel.States).DisplayName);
    }

    [Fact]
    public async Task Fallback_toggle_reprojects_without_reloading_and_is_persisted()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE }");
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n STATE_ONE:0 \"English fallback\"\n");
        fixture.WriteGameFile("localisation/russian/other_l_russian.yml", "l_russian:\n OTHER:0 \"Другое\"\n");
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings()));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service, settings) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        await viewModel.ChangeLanguageAsync("russian");
        var published = service.CurrentSnapshot;
        Assert.Equal("English fallback", Assert.Single(viewModel.States).DisplayName);

        await viewModel.SetEnglishFallbackAsync(false);

        Assert.Same(published, service.CurrentSnapshot);
        Assert.Equal("State 1", Assert.Single(viewModel.States).DisplayName);
        Assert.Equal("Missing localisation", Assert.Single(viewModel.States).NameStatus);
        Assert.False(settings.Saved?.EnglishFallbackEnabled);
    }

    [Fact]
    public async Task Readable_language_options_keep_canonical_identifiers()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/a_l_english.yml", "l_english:\n A:0 \"A\"\n");
        fixture.WriteGameFile("localisation/spanish/a_l_spanish.yml", "l_spanish:\n A:0 \"A\"\n");
        fixture.WriteGameFile("localisation/russian/a_l_russian.yml", "l_russian:\n A:0 \"A\"\n");
        fixture.WriteGameFile("localisation/simp_chinese/a_l_simp_chinese.yml", "l_simp_chinese:\n A:0 \"A\"\n");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(
            [("english", "🇬🇧  English"), ("russian", "🇷🇺  Русский"), ("simp_chinese", "🇨🇳  简体中文"), ("spanish", "🇪🇸  Español")],
            viewModel.AvailableLanguages.Select(language => (language.Id, language.DisplayName)));
    }

    [Theory]
    [InlineData("braz_por", "🇧🇷  Português (Brasil)")]
    [InlineData("french", "🇫🇷  Français")]
    [InlineData("german", "🇩🇪  Deutsch")]
    [InlineData("japanese", "🇯🇵  日本語")]
    [InlineData("korean", "🇰🇷  한국어")]
    [InlineData("polish", "🇵🇱  Polski")]
    public void Hoi4_language_options_have_native_names_and_flags(string id, string displayName)
    {
        Assert.Equal(displayName, LanguageOptionViewModel.Create(id).DisplayName);
    }

    [Fact]
    public async Task Workspace_without_localisation_remains_usable_with_no_selector_options()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=STATE_ONE }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };

        await viewModel.OpenWorkspaceAsync();

        Assert.Empty(viewModel.AvailableLanguages);
        Assert.False(viewModel.HasAvailableLanguages);
        Assert.Equal("State 1", Assert.Single(viewModel.States).DisplayName);
        Assert.Contains("No localisation", viewModel.LanguageSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Selecting_a_state_problem_reveals_that_state_and_clears_a_hiding_filter()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 history={ owner=ZZZ } }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        viewModel.SearchText = "State 1";

        viewModel.SelectedProblem = Assert.Single(viewModel.Problems, problem => problem.StateId == 2);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(2, viewModel.SelectedState?.Id);
    }

    [Fact]
    public async Task Reload_preserves_selection_and_publishes_new_source_values()
    {
        using var fixture = new TemporaryWorkspace();
        var statePath = fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 manpower=10 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        File.WriteAllText(statePath, "state={ id=1 manpower=20 }");

        await viewModel.ReloadAsync();

        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Equal("20", viewModel.SelectedState?.Manpower);
        Assert.Contains("Snapshot 2", viewModel.StatusSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_root_is_a_recoverable_workspace_with_a_visible_problem()
    {
        using var fixture = new TemporaryWorkspace();
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = Path.Combine(fixture.Root, "missing"),
        };

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Workspace, viewModel.Screen);
        Assert.Empty(viewModel.States);
        Assert.Contains(viewModel.Problems, problem => problem.Code == "OXIDE3001");
        Assert.Equal(1, viewModel.ErrorCount);
    }

    [Fact]
    public async Task Cancellation_returns_to_welcome_without_publishing_partial_state()
    {
        var service = new CancellableWorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = "/unused" };

        var opening = viewModel.OpenWorkspaceAsync();
        Assert.Equal(ApplicationScreen.Loading, viewModel.Screen);
        viewModel.CancelLoading();
        await opening;

        Assert.Equal(ApplicationScreen.Welcome, viewModel.Screen);
        Assert.True(viewModel.HasError);
        Assert.Empty(viewModel.States);
    }

    [Fact]
    public async Task Initialization_restores_last_workspace_paths_and_copper_verdigris_theme()
    {
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings(
            LastGameRoot: "/remembered/game",
            LastActiveModRoot: "/remembered/mod",
            Theme: OxideTheme.CopperVerdigrisLight)));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service, settings);
        OxideTheme? appliedTheme = null;
        viewModel.ThemeChanged += value => appliedTheme = value;

        await viewModel.InitializeAsync();

        Assert.Equal("/remembered/game", viewModel.GameRootPath);
        Assert.Equal("/remembered/mod", viewModel.ActiveModRootPath);
        Assert.Equal(OxideTheme.CopperVerdigrisLight, viewModel.Theme);
        Assert.Equal(OxideTheme.CopperVerdigrisLight, appliedTheme);
        Assert.Equal("Copper Verdigris Light", viewModel.ThemeName);
        Assert.False(viewModel.IsDarkMode);
    }

    [Fact]
    public async Task Theme_toggle_is_persisted_with_current_workspace_paths()
    {
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings()));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service, settings)
        {
            GameRootPath = "/game",
            ActiveModRootPath = "/mod",
        };

        await viewModel.ToggleThemeAsync();

        Assert.Equal(OxideTheme.CopperVerdigrisLight, viewModel.Theme);
        Assert.Equal("/game", settings.Saved!.LastGameRoot);
        Assert.Equal("/mod", settings.Saved.LastActiveModRoot);
        Assert.Equal(OxideTheme.CopperVerdigrisLight, settings.Saved.Theme);
        Assert.False(viewModel.IsDarkMode);
    }

    [Fact]
    public async Task Settings_save_failure_does_not_discard_a_successfully_opened_workspace()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var settings = new RecordingSettingsStore(
            new ApplicationSettingsLoadResult(new ApplicationSettings()),
            saveException: new IOException("disk unavailable"));
        using var viewModel = new MainWindowViewModel(service, settings) { GameRootPath = fixture.GameRoot };

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Workspace, viewModel.Screen);
        Assert.Single(viewModel.States);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("could not save", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_pasted_workspace_path_is_recoverable_on_the_welcome_screen()
    {
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = "invalid\0path" };

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Welcome, viewModel.Screen);
        Assert.True(viewModel.HasError);
        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Contains("workspace paths", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(service.CurrentSnapshot);
    }

    [Fact]
    public async Task State_and_country_concept_views_expose_shared_contribution_details()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 }");
        fixture.WriteGameFile("common/country_tags/00_base.txt", "AAA=\"countries/Base.txt\"");
        fixture.WriteModFile("common/country_tags/00_mod.txt", "AAA=\"countries/Mod.txt\"");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };

        await viewModel.OpenWorkspaceAsync();

        var state = Assert.Single(viewModel.States).Contribution;
        var country = Assert.Single(viewModel.Countries).Contribution;
        Assert.All(new[] { state, country }, contribution =>
        {
            Assert.Equal("Effective from Active mod", contribution.EffectiveLayerLabel);
            Assert.Equal("2 contributions", contribution.ContributionCountLabel);
            Assert.Contains(contribution.Contributions,
                item => item.Disposition is ContributionDisposition.Effective);
            Assert.Contains(contribution.Contributions,
                item => item.Disposition is ContributionDisposition.Shadowed);
        });
    }

    [Fact]
    public async Task Source_navigation_action_publishes_the_exact_navigation_request()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var request = Assert.Single(viewModel.States).Contribution.EffectiveNavigationRequest!;
        SourceNavigationRequest? published = null;
        viewModel.SourceNavigationRequested += value => published = value;

        viewModel.RequestSourceNavigation(request);

        Assert.Same(request, published);
        Assert.Same(request, viewModel.LastSourceNavigationRequest);
        Assert.True(viewModel.LastSourceNavigationResolution?.IsResolved);
        Assert.Equal(service.CurrentSnapshot!.Version, request.SnapshotVersion);
        Assert.Equal(request.DocumentId, request.Target.DocumentId);
        Assert.True(viewModel.HasSourceNavigationRequest);
        Assert.Contains(request.VirtualPath, viewModel.SourceNavigationSummary, StringComparison.Ordinal);
        Assert.Contains(request.Location, viewModel.SourceNavigationSummary, StringComparison.Ordinal);
        Assert.True(viewModel.IsSourceViewerVisible);
        Assert.False(viewModel.IsConceptWorkspaceVisible);
        Assert.NotNull(viewModel.SourceViewer);
        Assert.Equal("1-Test.txt", viewModel.SourceViewer.FileName);
        Assert.Equal("state={ id=1 }", viewModel.SourceViewer.FullText);
        Assert.Equal(request.SpanStart, viewModel.SourceViewer.SelectionStart);

        viewModel.SourceViewer.SearchText = "id=1";
        Assert.Equal("1 match", viewModel.SourceViewer.SearchSummary);
        Assert.True(viewModel.SourceViewer.FindNext());
        Assert.Equal("id=1", viewModel.SourceViewer.VisibleText[
            viewModel.SourceViewer.SelectionStart..viewModel.SourceViewer.SelectionEnd]);

        viewModel.CloseSourceViewer();

        Assert.False(viewModel.IsSourceViewerVisible);
        Assert.True(viewModel.IsConceptWorkspaceVisible);
    }

    [Fact]
    public async Task Reload_remaps_a_compatible_source_view_to_the_new_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 manpower=10 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var oldState = Assert.Single(viewModel.States);
        viewModel.RequestSourceNavigation(oldState.Contribution.EffectiveNavigationRequest!);
        viewModel.SourceViewer!.SearchText = "manpower";
        var oldVersion = viewModel.LastSourceNavigationRequest!.SnapshotVersion;
        File.WriteAllText(path, "state={ id=1 manpower=20 }");

        await viewModel.ReloadAsync();

        var newState = Assert.Single(viewModel.States);
        Assert.NotSame(oldState, newState);
        Assert.Equal("10", oldState.Manpower);
        Assert.Equal("20", newState.Manpower);
        Assert.NotNull(viewModel.LastSourceNavigationRequest);
        Assert.True(viewModel.LastSourceNavigationRequest.SnapshotVersion > oldVersion);
        Assert.NotNull(viewModel.SourceViewer);
        Assert.Contains("manpower=20", viewModel.SourceViewer.FullText, StringComparison.Ordinal);
        Assert.Equal("manpower", viewModel.SourceViewer.SearchText);
        Assert.Equal("1 of 1", viewModel.SourceHistorySummary);
        Assert.Null(viewModel.SourceRefreshNotice);
    }

    [Fact]
    public async Task Reload_closes_a_stale_source_without_substituting_another_layer()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 manpower=10 }");
        var modPath = fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 manpower=20 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.RequestSourceNavigation(Assert.Single(viewModel.States).Contribution.EffectiveNavigationRequest!);
        File.Delete(modPath);

        await viewModel.ReloadAsync();

        Assert.Null(viewModel.SourceViewer);
        Assert.True(viewModel.IsConceptWorkspaceVisible);
        Assert.NotNull(viewModel.SourceRefreshNotice);
        Assert.Contains("stale", viewModel.SourceNavigationSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("document and layer", viewModel.SourceNavigationSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("No source history", viewModel.SourceHistorySummary);
        Assert.Equal("10", Assert.Single(viewModel.States).Manpower);
    }

    [Fact]
    public async Task Source_history_supports_back_forward_and_discards_the_forward_branch()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 }");
        fixture.WriteGameFile("history/states/3-Three.txt", "state={ id=3 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var requests = viewModel.States.Select(state => state.Contribution.EffectiveNavigationRequest!).ToArray();

        viewModel.RequestSourceNavigation(requests[0]);
        viewModel.RequestSourceNavigation(requests[1]);

        Assert.True(viewModel.CanNavigateSourceBack);
        Assert.False(viewModel.CanNavigateSourceForward);
        Assert.Equal("2 of 2", viewModel.SourceHistorySummary);
        viewModel.NavigateSourceBack();
        Assert.Same(requests[0], viewModel.LastSourceNavigationRequest);
        Assert.True(viewModel.CanNavigateSourceForward);
        viewModel.NavigateSourceForward();
        Assert.Same(requests[1], viewModel.LastSourceNavigationRequest);

        viewModel.NavigateSourceBack();
        viewModel.RequestSourceNavigation(requests[2]);

        Assert.Same(requests[2], viewModel.LastSourceNavigationRequest);
        Assert.Equal("2 of 2", viewModel.SourceHistorySummary);
        Assert.False(viewModel.CanNavigateSourceForward);
    }

    [Fact]
    public async Task Source_history_is_bounded_and_related_contributions_remain_navigable()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/All.txt",
            string.Join('\n', Enumerable.Range(1, 55).Select(id => $"state={{ id={id} }}")));
        fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 manpower=10 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        var requests = viewModel.States.Select(state => state.Contribution.EffectiveNavigationRequest!).ToArray();

        foreach (var request in requests)
        {
            viewModel.RequestSourceNavigation(request);
        }

        Assert.Equal("50 of 50", viewModel.SourceHistorySummary);
        var firstState = viewModel.States.Single(state => state.Id == 1);
        viewModel.RequestSourceNavigation(firstState.Contribution.EffectiveNavigationRequest!);
        var viewer = Assert.IsType<SourceViewerViewModel>(viewModel.SourceViewer);
        Assert.Equal(2, viewer.Relationships.Length);
        Assert.Single(viewer.Relationships, relationship => relationship.IsCurrent);
        var shadowed = Assert.Single(viewer.Relationships, relationship =>
            relationship.Label.StartsWith("Shadowed", StringComparison.Ordinal));

        viewModel.RequestSourceNavigation(shadowed.NavigationRequest);

        Assert.True(viewModel.CanNavigateSourceBack);
        Assert.Single(viewModel.SourceViewer!.Relationships, relationship =>
            relationship.IsCurrent && relationship.Label.StartsWith("Shadowed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Source_relationship_lookup_supports_each_semantic_identity_domain()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=STATE_1 provinces={ 10 } }");
        fixture.WriteGameFile("common/country_tags/00_test.txt", "AAA=\"countries/Test.txt\"");
        fixture.WriteGameFile("map/strategicregions/1-Test.txt", "strategic_region={ id=1 name=REGION_1 provinces={ 10 } }");
        fixture.WriteGameFile(
            "localisation/english/test_l_english.yml",
            "l_english:\n STATE_1:0 \"Test State\"\n REGION_1:0 \"Test Region\"");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var snapshot = service.CurrentSnapshot!;
        var requests = new[]
        {
            Assert.Single(viewModel.States).Contribution.EffectiveNavigationRequest!,
            Assert.Single(viewModel.Countries).Contribution.EffectiveNavigationRequest!,
            ContributionSetPresentation.Create(snapshot.Semantics.StrategicRegions[1], snapshot)
                .EffectiveNavigationRequest!,
            ContributionSetPresentation.Create(
                    snapshot.Semantics.Localisations[new(
                        new("english"),
                        new("STATE_1"))],
                    snapshot)
                .EffectiveNavigationRequest!,
        };

        foreach (var request in requests)
        {
            viewModel.RequestSourceNavigation(request);
            Assert.Single(viewModel.SourceViewer!.Relationships);
            Assert.True(viewModel.SourceViewer.Relationships[0].IsCurrent);
        }
    }

    [Fact]
    public async Task Source_viewer_bounds_large_same_identity_relationship_sets()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/Duplicates.txt",
            string.Join('\n', Enumerable.Range(1, 205).Select(_ => "state={ id=1 }")));
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        var contribution = Assert.Single(viewModel.States).Contribution;

        viewModel.RequestSourceNavigation(contribution.Contributions[0].NavigationRequest);

        Assert.Equal(200, viewModel.SourceViewer!.Relationships.Length);
        Assert.True(viewModel.SourceViewer.RelationshipsTruncated);
        Assert.Equal("200+ related contributions", viewModel.SourceViewer.RelationshipSummary);
    }

    [Fact]
    public async Task Automatic_refresh_reprojects_the_published_snapshot_and_preserves_selection()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 manpower=10 }");
        var path = fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 manpower=10 }");
        using var service = new WorkspaceService();
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        using var viewModel = new MainWindowViewModel(
            service,
            refreshCoordinator: coordinator,
            changeSourceFactory: _ => source)
        {
            GameRootPath = fixture.GameRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.SelectedState = viewModel.States.Single(state => state.Id == 2);
        viewModel.RequestSourceNavigation(viewModel.SelectedState.Contribution.EffectiveNavigationRequest!);
        var previousVersion = service.CurrentSnapshot!.Version;
        File.WriteAllText(path, "state={ id=2 manpower=20 }");

        source.Emit(ChangeBatch(service.CurrentSnapshot, path));
        await WaitForAsync(() => service.CurrentSnapshot!.Version > previousVersion);
        await WaitForAsync(() =>
            viewModel.SelectedState?.Manpower == "20" &&
            viewModel.SourceViewer?.FullText.Contains("manpower=20", StringComparison.Ordinal) is true &&
            viewModel.LastSourceNavigationRequest?.SnapshotVersion == service.CurrentSnapshot.Version);

        Assert.Equal(2, viewModel.SelectedState?.Id);
        Assert.NotNull(viewModel.SourceViewer);
        Assert.Contains("manpower=20", viewModel.SourceViewer.FullText, StringComparison.Ordinal);
        Assert.Equal(service.CurrentSnapshot.Version, viewModel.LastSourceNavigationRequest?.SnapshotVersion);
        Assert.Equal("Watching for changes", viewModel.AutomaticRefreshSummary);
        Assert.False(viewModel.RefreshNeedsAttention);
    }

    [Fact]
    public async Task Automatic_refresh_preference_is_restored_toggled_and_persisted()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        var settings = new RecordingSettingsStore(new ApplicationSettingsLoadResult(new ApplicationSettings(
            AutomaticRefreshEnabled: false)));
        using var service = new WorkspaceService();
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        using var viewModel = new MainWindowViewModel(
            service,
            settings,
            refreshCoordinator: coordinator,
            changeSourceFactory: _ => source)
        {
            GameRootPath = fixture.GameRoot,
        };
        await viewModel.InitializeAsync();
        viewModel.GameRootPath = fixture.GameRoot;
        await viewModel.OpenWorkspaceAsync();

        Assert.False(viewModel.AutomaticRefreshEnabled);
        Assert.False(source.IsRunning);
        Assert.Equal("Automatic refresh is off", viewModel.AutomaticRefreshSummary);

        await viewModel.SetAutomaticRefreshAsync(true);
        Assert.True(source.IsRunning);
        Assert.True(settings.Saved?.AutomaticRefreshEnabled);

        await viewModel.SetAutomaticRefreshAsync(false);
        Assert.False(source.IsRunning);
        Assert.False(settings.Saved?.AutomaticRefreshEnabled);
    }

    [Fact]
    public async Task Watcher_failure_is_visible_without_replacing_the_workspace()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        using var viewModel = new MainWindowViewModel(
            service,
            refreshCoordinator: coordinator,
            changeSourceFactory: _ => source)
        {
            GameRootPath = fixture.GameRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        var published = service.CurrentSnapshot;
        viewModel.RequestSourceNavigation(Assert.Single(viewModel.States).Contribution.EffectiveNavigationRequest!);
        var sourceViewer = viewModel.SourceViewer;

        source.EmitError(new WorkspaceChangeSourceError("The watcher needs recovery."));
        await WaitForAsync(() => viewModel.RefreshNeedsAttention);

        Assert.Same(published, service.CurrentSnapshot);
        Assert.Same(sourceViewer, viewModel.SourceViewer);
        Assert.Equal(published!.Version, viewModel.LastSourceNavigationRequest?.SnapshotVersion);
        Assert.Equal("File watching unavailable", viewModel.AutomaticRefreshSummary);
        Assert.Equal("The watcher needs recovery.", viewModel.AutomaticRefreshDetail);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task State_edit_surface_previews_an_eligible_active_mod_scalar_without_writing()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural } # preserved";
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();

        Assert.True(viewModel.CanEditSelectedStateManpower);
        Assert.True(viewModel.CanEditSelectedStateCategory);
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);

        Assert.True(viewModel.IsStateEditOpen);
        Assert.Equal("10", viewModel.StateEditOriginalValue);
        Assert.False(viewModel.CanApplyStateEdit);
        viewModel.StateEditDraftValue = "25";

        Assert.True(viewModel.CanApplyStateEdit);
        Assert.Contains("manpower = 10", viewModel.StateEditPreviewBefore);
        Assert.Contains("manpower = 25", viewModel.StateEditPreviewAfter);
        Assert.Equal("history/states/1-Test.txt", viewModel.StateEditSourcePath);
        Assert.Equal(source, File.ReadAllText(path));
    }

    [Fact]
    public async Task State_edit_source_preview_has_a_hard_character_bound_for_single_line_files()
    {
        using var fixture = new TemporaryWorkspace();
        var source = "state = { id = 1 manpower = 10 state_category = rural }" + new string(' ', 20_000);
        fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        viewModel.StateEditDraftValue = "20";

        Assert.InRange(viewModel.StateEditPreviewBefore.Length, 1, 4_000);
        Assert.InRange(viewModel.StateEditPreviewAfter.Length, 1, 4_000);
        Assert.Contains("manpower = 10", viewModel.StateEditPreviewBefore);
        Assert.Contains("manpower = 20", viewModel.StateEditPreviewAfter);
    }

    [Fact]
    public async Task State_edit_surface_explains_read_only_base_values()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/1-Base.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        Assert.False(viewModel.CanEditSelectedStateManpower);
        Assert.False(viewModel.CanEditSelectedStateCategory);
        Assert.Contains("read-only", viewModel.SelectedStateManpowerEditHint, StringComparison.OrdinalIgnoreCase);
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        Assert.False(viewModel.IsStateEditOpen);
        Assert.True(viewModel.HasStateEditFeedback);
    }

    [Fact]
    public async Task Applying_state_edit_writes_reloads_and_preserves_selection()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.StateCategory);
        viewModel.StateEditDraftValue = "large_city";

        await viewModel.ApplyStateEditAsync();

        Assert.False(viewModel.IsStateEditOpen);
        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Equal("large_city", viewModel.SelectedState?.Category);
        Assert.Contains("state_category = large_city", File.ReadAllText(path));
        Assert.Equal("Saved state category.", viewModel.StateEditFeedback);
        Assert.True(viewModel.CanUndoLastEdit);

        await viewModel.UndoLastEditAsync();

        Assert.False(viewModel.CanUndoLastEdit);
        Assert.Equal("rural", viewModel.SelectedState?.Category);
        Assert.Contains("state_category = rural", File.ReadAllText(path));
        Assert.Equal("Undid the last edit.", viewModel.StateEditFeedback);
    }

    [Fact]
    public async Task Applying_state_edit_reports_external_conflict_without_overwriting()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        viewModel.StateEditDraftValue = "20";
        File.WriteAllText(path, "state = { id = 1 manpower = 99 state_category = rural }");

        await viewModel.ApplyStateEditAsync();

        Assert.True(viewModel.IsStateEditOpen);
        Assert.Contains("changed", viewModel.StateEditFeedback!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manpower = 99", File.ReadAllText(path));
    }

    [Fact]
    public async Task Undo_surface_reports_conflict_after_a_later_external_change()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        viewModel.StateEditDraftValue = "20";
        await viewModel.ApplyStateEditAsync();
        const string external = "state = { id = 1 manpower = 99 state_category = rural }";
        File.WriteAllText(path, external);

        await viewModel.UndoLastEditAsync();

        Assert.True(viewModel.CanUndoLastEdit);
        Assert.Contains("changed", viewModel.StateEditFeedback!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(external, File.ReadAllText(path));
    }

    [Fact]
    public async Task Automatic_refresh_closes_a_stale_edit_preview_with_an_explanation()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        using var viewModel = new MainWindowViewModel(
            service,
            refreshCoordinator: coordinator,
            changeSourceFactory: _ => source)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        viewModel.StateEditDraftValue = "20";
        var previousVersion = service.CurrentSnapshot!.Version;
        File.WriteAllText(path, "state = { id = 1 manpower = 30 state_category = rural }");
        var layer = service.CurrentSnapshot.Layers.Single(candidate => candidate.IsWritable);
        var classified = WorkspaceChangeClassifier.Classify(layer, path);
        source.Emit(new WorkspaceChangeBatch(
        [
            new DocumentChange(
                new WorkspaceChange(
                    WorkspaceChangeKind.Changed,
                    classified.Source,
                    classified.Source,
                    DateTimeOffset.UnixEpoch,
                    WorkspaceChangeOrigin.Watcher),
                classified.DocumentKind!.Value,
                classified.Category!.Value),
        ]));

        await WaitForAsync(() => service.CurrentSnapshot!.Version > previousVersion);
        await WaitForAsync(() => !viewModel.IsStateEditOpen);

        Assert.Contains("closed", viewModel.StateEditFeedback!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("30", viewModel.SelectedState?.Manpower);
    }

    [Fact]
    public async Task Applying_an_edit_pauses_own_file_events_and_resumes_watching_after_reload()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        using var viewModel = new MainWindowViewModel(
            service,
            refreshCoordinator: coordinator,
            changeSourceFactory: _ => source)
        {
            GameRootPath = fixture.GameRoot,
            ActiveModRootPath = fixture.ModRoot,
        };
        await viewModel.OpenWorkspaceAsync();
        viewModel.BeginStateEdit(StateScalarProperty.Manpower);
        viewModel.StateEditDraftValue = "20";

        await viewModel.ApplyStateEditAsync();

        Assert.True(source.IsRunning);
        Assert.Equal(WorkspaceRefreshCoordinatorState.Watching, coordinator.Status.State);
        Assert.Equal("20", viewModel.SelectedState?.Manpower);
        Assert.Contains("manpower = 20", File.ReadAllText(path));
        Assert.True(viewModel.CanUndoLastEdit);
    }

    private static WorkspaceChangeBatch ChangeBatch(WorkspaceSnapshot snapshot, string path)
    {
        var classified = WorkspaceChangeClassifier.Classify(snapshot.Layers[0], path);
        return new WorkspaceChangeBatch(
        [
            new DocumentChange(
                new WorkspaceChange(
                    WorkspaceChangeKind.Changed,
                    classified.Source,
                    classified.Source,
                    DateTimeOffset.UnixEpoch,
                    WorkspaceChangeOrigin.Watcher),
                classified.DocumentKind!.Value,
                classified.Category!.Value),
        ]);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class CancellableWorkspaceService : IWorkspaceService
    {
        public WorkspaceSnapshot? CurrentSnapshot => null;

        public event Action<WorkspaceSnapshot>? SnapshotPublished
        {
            add { }
            remove { }
        }

        public async Task<WorkspaceSnapshot> OpenAsync(
            WorkspaceConfiguration configuration,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellable test service unexpectedly completed.");
        }

        public Task<WorkspaceSnapshot> ReloadAsync(
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WorkspaceRefreshResult> RefreshAsync(
            IncrementalRefreshRequest request,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSettingsStore(
        ApplicationSettingsLoadResult loadResult,
        Exception? saveException = null) : IApplicationSettingsStore
    {
        public ApplicationSettings? Saved { get; private set; }

        public Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(loadResult);

        public Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
        {
            if (saveException is not null)
            {
                return Task.FromException(saveException);
            }

            Saved = settings;
            return Task.CompletedTask;
        }
    }
}

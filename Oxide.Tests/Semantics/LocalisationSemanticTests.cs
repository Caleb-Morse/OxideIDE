using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class LocalisationSemanticTests
{
    [Fact]
    public async Task Whole_value_localisation_references_resolve_with_a_bounded_provenance_chain()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "localisation/english/states_l_english.yml",
            "l_english:\n STATE_1050:0 \"$VICTORY_POINTS_1001$\"\n");
        fixture.WriteGameFile(
            "localisation/english/victory_points_l_english.yml",
            "l_english:\n VICTORY_POINTS_1001:0 \"Bengbu\"\n CYCLE_A:0 \"$CYCLE_B$\"\n CYCLE_B:0 \"$CYCLE_A$\"\n");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var resolved = Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("english", "STATE_1050"));
        var cycle = Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("english", "CYCLE_A"));

        Assert.Equal("Bengbu", resolved.Value);
        Assert.Equal(["STATE_1050", "VICTORY_POINTS_1001"],
            resolved.ReferenceChain.Select(step => step.Identity.Key.Value));
        Assert.Equal("$VICTORY_POINTS_1001$", resolved.Declaration.Value.Value);
        Assert.Equal("Bengbu", resolved.ReferenceChain[^1].Declaration.Value.Value);
        Assert.NotEqual(resolved.RootProvenance, resolved.Provenance);
        Assert.Equal(["CYCLE_A", "CYCLE_B"], cycle.ReferenceChain.Select(step => step.Identity.Key.Value));
        Assert.Equal("$CYCLE_A$", cycle.Value);
    }

    [Fact]
    public async Task Declarations_are_language_qualified_and_resolve_with_exact_value_provenance()
    {
        using var fixture = new TemporaryWorkspace();
        var englishPath = fixture.WriteGameFile(
            "localisation/english/names_l_english.yml",
            "\uFEFFl_english:\n STATE_1:0 \"The Iron Coast\"\n");
        fixture.WriteGameFile(
            "localisation/spanish/names_l_spanish.yml",
            "\uFEFFl_spanish:\n STATE_1:0 \"La Costa de Hierro\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var result = Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("english", "STATE_1"));

        Assert.Equal(2, snapshot.Semantics.LocalisationDeclarations.Length);
        Assert.Equal("The Iron Coast", result.Value);
        Assert.Equal("Exact language match", result.SelectionReason);
        Assert.Equal(englishPath, result.Provenance.PhysicalPath);
        Assert.Equal("The Iron Coast", snapshot.DocumentsById[result.Provenance.DocumentId].Text!.GetText(result.Provenance.Span));
        Assert.Equal("The Iron Coast", result.Declaration.Value.OriginalText);
    }

    [Fact]
    public async Task Cross_layer_entries_select_mod_and_retain_shadowed_base_contribution()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/a_l_english.yml", "l_english:\n KEY:0 \"Base\"\n");
        fixture.WriteModFile("localisation/english/b_l_english.yml", "l_english:\n KEY:0 \"Mod\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var result = Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("english", "KEY"));

        Assert.Equal("Mod", result.Value);
        Assert.Equal(ContributionResolutionReasonKind.HigherLayerPrecedence,
            result.ContributionResolution.Reason.Kind);
        Assert.Equal("Base", Assert.Single(result.ContributionResolution.ShadowedContributions)
            .Contribution.Declaration.Value.Value);
        var entry = snapshot.Semantics.Localisations[new(
            new LocalisationLanguage("english"),
            new LocalisationKey("KEY"))];
        Assert.False(entry.IsAmbiguous);
        Assert.Equal(2, entry.Contributions.Length);
        Assert.DoesNotContain(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4009");
    }

    [Fact]
    public async Task Missing_ambiguous_and_english_fallback_outcomes_are_distinct()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/a_l_english.yml", "l_english:\n FALLBACK:0 \"English\"\n DUP:0 \"One\"\n");
        fixture.WriteGameFile("localisation/english/b_l_english.yml", "l_english:\n DUP:0 \"Two\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var resolver = snapshot.Semantics.LocalisationResolver;

        var fallback = Assert.IsType<ResolvedLocalisation>(resolver.Resolve("russian", "FALLBACK"));
        Assert.Equal("English fallback", fallback.SelectionReason);
        Assert.Equal("English", fallback.Value);
        Assert.IsType<MissingLocalisation>(resolver.Resolve("russian", "MISSING"));
        Assert.IsType<AmbiguousLocalisation>(resolver.Resolve("russian", "DUP"));
        Assert.IsType<InvalidLocalisation>(resolver.Resolve("", "FALLBACK"));
    }

    [Fact]
    public async Task Ambiguous_requested_language_never_falls_back_to_english()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/a_l_english.yml", "l_english:\n KEY:0 \"English\"\n");
        fixture.WriteGameFile("localisation/russian/a_l_russian.yml", "l_russian:\n KEY:0 \"Один\"\n");
        fixture.WriteGameFile("localisation/russian/b_l_russian.yml", "l_russian:\n KEY:0 \"Два\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var result = Assert.IsType<AmbiguousLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("russian", "KEY", allowEnglishFallback: true));

        Assert.Equal("russian", result.CandidateLanguage.Value);
        Assert.Equal(2, result.Candidates.Length);
    }

    [Fact]
    public async Task Language_resolution_precedes_english_fallback_and_each_language_resolves_its_layers()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/base_l_english.yml", "l_english:\n KEY:0 \"Base English\"\n");
        fixture.WriteModFile("localisation/english/mod_l_english.yml", "l_english:\n KEY:0 \"Mod English\"\n");
        fixture.WriteGameFile("localisation/russian/base_l_russian.yml", "l_russian:\n KEY:0 \"База\"\n");
        fixture.WriteModFile("localisation/russian/mod_l_russian.yml", "l_russian:\n KEY:0 \"Мод\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var resolver = snapshot.Semantics.LocalisationResolver;
        var russian = Assert.IsType<ResolvedLocalisation>(resolver.Resolve("russian", "KEY"));
        var fallback = Assert.IsType<ResolvedLocalisation>(resolver.Resolve("german", "KEY"));

        Assert.Equal("Мод", russian.Value);
        Assert.False(russian.IsFallback);
        Assert.Equal(ContributionResolutionReasonKind.HigherLayerPrecedence,
            russian.ContributionResolution.Reason.Kind);
        Assert.Equal("Mod English", fallback.Value);
        Assert.True(fallback.IsFallback);
        Assert.Equal("English fallback", fallback.SelectionReason);
        Assert.Equal(ContributionResolutionReasonKind.HigherLayerPrecedence,
            fallback.ContributionResolution.Reason.Kind);
        Assert.Equal("english", fallback.ResolvedLanguage.Value);
        Assert.Equal(["german", "english"], fallback.Attempts.Select(attempt => attempt.CandidateLanguage.Value));
        Assert.True(fallback.Attempts[0].WasMissing);
        Assert.Null(fallback.Attempts[0].ContributionResolution);
        Assert.False(fallback.Attempts[1].WasMissing);
        Assert.Same(fallback.ContributionResolution, fallback.Attempts[1].ContributionResolution);
    }

    [Fact]
    public async Task File_excluded_localisation_remains_queryable_but_cannot_resolve()
    {
        using var fixture = new TemporaryWorkspace();
        const string path = "localisation/spanish/shared_l_spanish.yml";
        fixture.WriteGameFile(path, "l_spanish:\n BASE_ONLY:0 \"Base\"\n");
        fixture.WriteModFile(path, "l_spanish:\n MOD_ONLY:0 \"Mod\"\n");
        fixture.WriteGameFile(
            "localisation/english/fallback_l_english.yml",
            "l_english:\n BASE_ONLY:0 \"English fallback\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var fallback = Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("spanish", "BASE_ONLY"));
        Assert.Equal("English fallback", fallback.Value);
        Assert.True(fallback.IsFallback);
        Assert.Equal(2, fallback.Attempts.Length);
        Assert.Equal(ContributionResolutionKind.Missing,
            fallback.Attempts[0].ContributionResolution?.Kind);
        Assert.Single(fallback.Attempts[0].ContributionResolution!.ExcludedContributions);
        Assert.Equal("Mod", Assert.IsType<ResolvedLocalisation>(
            snapshot.Semantics.LocalisationResolver.Resolve("spanish", "MOD_ONLY")).Value);
        var excludedEntry = snapshot.Semantics.Localisations[new(
            new LocalisationLanguage("spanish"),
            new LocalisationKey("BASE_ONLY"))];
        Assert.Equal(ContributionResolutionKind.Missing, excludedEntry.Resolution.Kind);
        Assert.Single(excludedEntry.Resolution.ExcludedContributions);
        Assert.Single(snapshot.Semantics.DeclarationInventory.Localisations, item => !item.IsEligible);
    }

    [Fact]
    public async Task States_and_countries_use_the_same_human_readable_name_resolver()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=STATE_1 }");
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TST=\"countries/Test.txt\"");
        fixture.WriteGameFile(
            "localisation/english/names_l_english.yml",
            "l_english:\n STATE_1:0 \"Test State\"\n TST:0 \"Test Country\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var resolver = snapshot.Semantics.LocalisationResolver;

        Assert.Equal("Test State", resolver.ResolveName(snapshot.Semantics.States[1], "english").DisplayText);
        Assert.Equal("Test Country", resolver.ResolveName(snapshot.Semantics.Countries["TST"], "english").DisplayText);
        Assert.IsType<ResolvedLocalisation>(resolver.ResolveName(snapshot.Semantics.States[1], "english").Resolution);
        Assert.IsType<ResolvedLocalisation>(resolver.ResolveName(snapshot.Semantics.Countries["TST"], "english").Resolution);
    }

    [Fact]
    public async Task Reload_publishes_localisation_atomically_without_mutating_the_previous_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n KEY:0 \"Before\"\n");
        using var service = new WorkspaceService();
        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        fixture.WriteGameFile("localisation/english/names_l_english.yml", "l_english:\n KEY:0 \"After\"\n");
        var second = await service.ReloadAsync();

        Assert.Equal("Before", Assert.IsType<ResolvedLocalisation>(
            first.Semantics.LocalisationResolver.Resolve("english", "KEY")).Value);
        Assert.Equal("After", Assert.IsType<ResolvedLocalisation>(
            second.Semantics.LocalisationResolver.Resolve("english", "KEY")).Value);
        Assert.Same(second, service.CurrentSnapshot);
    }
}

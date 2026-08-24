using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Loading;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Localisation;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task Open_discovers_supported_files_in_base_and_mod_layers()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TST=\"countries/Test.txt\"");
        fixture.WriteGameFile("localisation/english/states_l_english.yml", "l_english:\n STATE_1: \"Test\"");
        fixture.WriteGameFile("events/ignored.txt", "country_event={ id=test.1 }");
        fixture.WriteModFile("history/states/2-Mod.txt", "state={ id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(
            fixture.GameRoot,
            fixture.ModRoot,
            "Fixture"));

        Assert.Equal("Fixture", snapshot.Configuration.DisplayName);
        Assert.Equal(2, snapshot.Layers.Length);
        Assert.Equal(4, snapshot.Documents.Length);
        Assert.Equal(4, snapshot.LoadMetrics.DocumentCount);
        Assert.Equal(4, snapshot.LoadMetrics.LoadedDocumentCount);
        Assert.Equal(3, snapshot.Documents.Count(document => document.Layer.Kind is ContentLayerKind.BaseGame));
        Assert.Single(snapshot.Documents, document => document.Layer.Kind is ContentLayerKind.ActiveMod);
        Assert.DoesNotContain(snapshot.Documents, document => document.VirtualPath.Value.StartsWith("events/", StringComparison.Ordinal));
        Assert.All(snapshot.Documents, document => Assert.True(document.IsLoaded));
        var localisation = Assert.Single(snapshot.Documents, document =>
            document.Kind is SourceDocumentKind.Localisation);
        Assert.Null(localisation.SyntaxTree);
        Assert.NotNull(localisation.LocalisationSyntaxTree);
    }

    [Fact]
    public async Task Ordered_configuration_supports_multiple_named_mod_layers()
    {
        using var fixture = new TemporaryWorkspace();
        var firstModRoot = Path.Combine(fixture.Root, "first-mod");
        var secondModRoot = Path.Combine(fixture.Root, "second-mod");
        Directory.CreateDirectory(firstModRoot);
        Directory.CreateDirectory(secondModRoot);
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        WriteLayerFile(firstModRoot, "history/states/2-First.txt", "state={ id=2 }");
        WriteLayerFile(secondModRoot, "history/states/3-Second.txt", "state={ id=3 }");
        var configuration = new WorkspaceConfiguration(
        [
            ContentLayer.Mod("second-mod", "Second mod", secondModRoot, 20),
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("first-mod", "First mod", firstModRoot, 10),
        ], "Layered fixture");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(configuration);

        Assert.Equal(["base-game", "first-mod", "second-mod"],
            snapshot.Layers.Select(layer => layer.Id.Value));
        Assert.Equal(["Base game", "First mod", "Second mod"],
            snapshot.Layers.Select(layer => layer.DisplayName));
        Assert.Equal([0, 10, 20], snapshot.Layers.Select(layer => layer.Position));
        Assert.Equal(["base-game", "first-mod", "second-mod"],
            snapshot.Documents.Select(document => document.Layer.Id.Value));
    }

    [Fact]
    public async Task Disabled_layer_is_preserved_in_configuration_but_not_loaded()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        var disabledRoot = Path.Combine(fixture.Root, "missing-disabled-mod");
        var configuration = new WorkspaceConfiguration(
        [
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("disabled-mod", "Disabled mod", disabledRoot, 1, isEnabled: false),
        ]);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(configuration);

        Assert.Equal(2, snapshot.Configuration.Layers.Length);
        Assert.Single(snapshot.Layers);
        Assert.Single(snapshot.Documents);
        Assert.DoesNotContain(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3001");
    }

    [Fact]
    public async Task Source_identity_includes_layer_path_and_stable_document_identity()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-Test.txt";
        var physicalPath = fixture.WriteGameFile(virtualPath, "state={ id=1 }");
        using var service = new WorkspaceService();

        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var second = await service.ReloadAsync();
        var firstIdentity = Assert.Single(first.Documents).SourceIdentity;
        var secondIdentity = Assert.Single(second.Documents).SourceIdentity;

        Assert.Equal(firstIdentity, secondIdentity);
        Assert.Equal("base-game", firstIdentity.LayerId.Value);
        Assert.Equal(virtualPath, firstIdentity.VirtualPath.Value);
        Assert.Equal(Path.GetFullPath(physicalPath), firstIdentity.PhysicalPath);
        Assert.Equal(first.Documents[0].Id, firstIdentity.DocumentId);
    }

    [Fact]
    public void Configuration_rejects_duplicate_layer_ids_and_positions()
    {
        using var fixture = new TemporaryWorkspace();
        var otherRoot = Path.Combine(fixture.Root, "other");

        var duplicateId = Assert.Throws<ArgumentException>(() => new WorkspaceConfiguration(
        [
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("base-game", "Duplicate", otherRoot, 1),
        ]));
        var duplicatePosition = Assert.Throws<ArgumentException>(() => new WorkspaceConfiguration(
        [
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("mod", "Same position", otherRoot, 0),
        ]));

        Assert.Contains("duplicated", duplicateId.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicated", duplicatePosition.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reload_publishes_a_new_version_with_stable_document_ids()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var published = new List<long>();
        service.SnapshotPublished += snapshot => published.Add(snapshot.Version);

        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var second = await service.ReloadAsync();

        Assert.True(second.Version > first.Version);
        Assert.Equal(first.Documents[0].Id, second.Documents[0].Id);
        Assert.Same(second, service.CurrentSnapshot);
        Assert.Equal([first.Version, second.Version], published);
    }

    [Fact]
    public async Task Malformed_syntax_produces_diagnostics_without_aborting_workspace()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Broken.txt", "state={ id=1 owner=\"broken\n");
        fixture.WriteGameFile("history/states/2-Good.txt", "state={ id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.All(snapshot.Documents, document => Assert.True(document.IsLoaded));
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE1001");
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE2003");
    }

    [Fact]
    public async Task Invalid_encoding_creates_a_failed_document_and_load_diagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameBytes("history/states/1-BadEncoding.txt", [0xC3, 0x28]);
        fixture.WriteGameFile("history/states/2-Good.txt", "state={ id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var failed = Assert.Single(snapshot.Documents, document => !document.IsLoaded);
        Assert.Equal(DocumentLoadStatus.Failed, failed.LoadStatus);
        Assert.Null(failed.SyntaxTree);
        Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
        Assert.Single(snapshot.Documents, document => document.IsLoaded);
    }

    [Fact]
    public async Task Malformed_localisation_is_loaded_with_lossless_syntax_diagnostics()
    {
        using var fixture = new TemporaryWorkspace();
        const string text = "l_english:\n STATE_1: \"unterminated\n";
        fixture.WriteGameFile("localisation/english/broken_l_english.yml", text);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var document = Assert.Single(snapshot.Documents);
        Assert.True(document.IsLoaded);
        Assert.Equal(SourceDocumentKind.Localisation, document.Kind);
        Assert.Null(document.SyntaxTree);
        var syntax = Assert.IsType<LocalisationSyntaxTree>(document.LocalisationSyntaxTree);
        Assert.Equal(text, syntax.ToFullString());
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "OXIDE1203");
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE1203");
    }

    [Fact]
    public async Task Invalid_localisation_encoding_remains_a_failed_document()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameBytes("localisation/english/bad_l_english.yml", [0xC3, 0x28]);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var document = Assert.Single(snapshot.Documents);
        Assert.Equal(SourceDocumentKind.Localisation, document.Kind);
        Assert.Equal(DocumentLoadStatus.Failed, document.LoadStatus);
        Assert.Null(document.Text);
        Assert.Null(document.SyntaxTree);
        Assert.Null(document.LocalisationSyntaxTree);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
    }

    [Fact]
    public async Task Same_virtual_path_in_multiple_layers_selects_higher_path_without_discarding_base()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-Collision.txt";
        fixture.WriteGameFile(virtualPath, "state={ id=1 history={ owner=AAA } }");
        fixture.WriteModFile(virtualPath, "state={ id=1 history={ owner=BBB } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var candidates = snapshot.DocumentsByVirtualPath[new VirtualPath(virtualPath)];

        Assert.Equal(2, candidates.Length);
        var baseDocument = candidates[0];
        var modDocument = candidates[1];
        Assert.Equal(DocumentParticipationKind.ShadowedByHigherLayerPath, baseDocument.Participation.Kind);
        Assert.Equal(modDocument.Id, baseDocument.Participation.ShadowingDocumentId);
        Assert.Equal(modDocument.Layer.Id, baseDocument.Participation.CausedByLayerId);
        Assert.Equal(DocumentParticipationKind.Participating, modDocument.Participation.Kind);
        Assert.NotEqual(candidates[0].Id, candidates[1].Id);
        var stateDeclaration = Assert.Single(snapshot.Semantics.StateDeclarations);
        Assert.Equal("BBB", Assert.Single(stateDeclaration.OwnerCandidates).Value);
        var inventory = snapshot.Semantics.DeclarationInventory.States;
        Assert.Equal(2, inventory.Length);
        Assert.False(inventory[0].IsEligible);
        Assert.True(inventory[1].IsEligible);
        Assert.Equal(baseDocument.SourceIdentity, inventory[0].Source);
        Assert.Equal(modDocument.SourceIdentity, inventory[1].Source);
    }

    [Fact]
    public async Task Descriptor_replace_path_excludes_lower_directory_but_preserves_documents()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        fixture.WriteGameFile("common/country_tags/00_base.txt", "AAA=\"countries/A.txt\"");
        fixture.WriteModFile("descriptor.mod", "name=\"Replacement\"\nreplace_path=\"history/states\"\n");
        fixture.WriteModFile("history/states/2-Mod.txt", "state={ id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var baseState = Assert.Single(snapshot.Documents, document =>
            document.Layer.Kind is ContentLayerKind.BaseGame
            && document.Participation.Category is ContentCategory.StateHistory);
        Assert.Equal(DocumentParticipationKind.ExcludedByReplacementPath, baseState.Participation.Kind);
        Assert.Equal("active-mod", baseState.Participation.CausedByLayerId?.Value);
        Assert.Equal("history/states", baseState.Participation.ReplacementRule?.Path.Value);
        Assert.EndsWith("descriptor.mod", baseState.Participation.ReplacementRule?.DescriptorPath, StringComparison.Ordinal);
        Assert.NotNull(baseState.Text);
        Assert.False(snapshot.Semantics.States.ContainsKey(1));
        Assert.True(snapshot.Semantics.States.ContainsKey(2));

        var baseCountryTags = Assert.Single(snapshot.Documents, document =>
            document.Participation.Category is ContentCategory.CountryTags);
        Assert.True(baseCountryTags.Participates);
        Assert.True(snapshot.Semantics.Countries.ContainsKey("AAA"));
        var stateInventory = snapshot.Semantics.DeclarationInventory.States;
        Assert.Equal(2, stateInventory.Length);
        Assert.Contains(stateInventory, item =>
            !item.IsEligible
            && item.Participation.Kind is DocumentParticipationKind.ExcludedByReplacementPath);
    }

    [Fact]
    public async Task Replacement_paths_are_case_insensitive_game_paths()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        fixture.WriteModFile("descriptor.mod", "replace_path=\"HISTORY/STATES\"");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var document = Assert.Single(snapshot.Documents);
        Assert.Equal(DocumentParticipationKind.ExcludedByReplacementPath, document.Participation.Kind);
    }

    [Fact]
    public async Task Replacement_paths_respect_directory_segment_boundaries()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        var configuration = new WorkspaceConfiguration(
        [
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("mod", "Mod", fixture.ModRoot, 1, replacePaths: ["history/state"]),
        ]);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(configuration);

        Assert.True(Assert.Single(snapshot.Documents).Participates);
    }

    [Fact]
    public async Task Malformed_descriptor_is_diagnostic_and_does_not_hide_base_documents()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        fixture.WriteModFile("descriptor.mod", "replace_path={ history/states }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        Assert.True(Assert.Single(snapshot.Documents).Participates);
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3011");
        Assert.True(snapshot.Semantics.States.ContainsKey(1));
    }

    [Fact]
    public async Task Differently_named_files_remain_participating_semantic_candidates()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 name=BASE }");
        fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 name=MOD }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.All(snapshot.Documents, document => Assert.True(document.Participates));
        Assert.Equal(2, snapshot.Semantics.StateDeclarations.Length);
        Assert.Null(snapshot.Semantics.States[1].Name);
    }

    [Fact]
    public async Task Highest_layer_replacement_is_the_decisive_participation_reason()
    {
        using var fixture = new TemporaryWorkspace();
        var firstModRoot = Path.Combine(fixture.Root, "first-mod");
        var secondModRoot = Path.Combine(fixture.Root, "second-mod");
        Directory.CreateDirectory(firstModRoot);
        Directory.CreateDirectory(secondModRoot);
        const string virtualPath = "history/states/1-State.txt";
        fixture.WriteGameFile(virtualPath, "state={ id=1 }");
        WriteLayerFile(firstModRoot, virtualPath, "state={ id=1 }");
        var configuration = new WorkspaceConfiguration(
        [
            ContentLayer.BaseGame(fixture.GameRoot),
            ContentLayer.Mod("first", "First", firstModRoot, 1),
            ContentLayer.Mod("second", "Second", secondModRoot, 2, replacePaths: ["history/states"]),
        ]);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(configuration);

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.All(snapshot.Documents, document =>
        {
            Assert.Equal(DocumentParticipationKind.ExcludedByReplacementPath, document.Participation.Kind);
            Assert.Equal("second", document.Participation.CausedByLayerId?.Value);
        });
        Assert.Empty(snapshot.Semantics.StateDeclarations);
    }

    [Fact]
    public async Task Failed_higher_path_still_shadows_lower_path_and_remains_inspectable()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-State.txt";
        fixture.WriteGameFile(virtualPath, "state={ id=1 }");
        var modPath = Path.Combine(fixture.ModRoot, virtualPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modPath)!);
        File.WriteAllBytes(modPath, [0xC3, 0x28]);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var candidates = snapshot.DocumentsByVirtualPath[new VirtualPath(virtualPath)];

        Assert.Equal(DocumentParticipationKind.ShadowedByHigherLayerPath, candidates[0].Participation.Kind);
        Assert.True(candidates[1].Participates);
        Assert.Equal(DocumentLoadStatus.Failed, candidates[1].LoadStatus);
        Assert.Contains(candidates[1].Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
        Assert.Empty(snapshot.Semantics.StateDeclarations);
    }

    [Fact]
    public async Task Excluded_unidentifiable_declaration_keeps_inventory_diagnostic_without_affecting_active_model()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-State.txt";
        fixture.WriteGameFile(virtualPath, "not_a_state={ value=broken }");
        fixture.WriteModFile(virtualPath, "state={ id=1 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        Assert.Single(snapshot.Semantics.StateDeclarations);
        Assert.Single(snapshot.Semantics.DeclarationInventory.States);
        var diagnostic = Assert.Single(snapshot.Semantics.DeclarationInventory.Diagnostics, item =>
            item.Diagnostic.Code == "OXIDE4001");
        Assert.False(diagnostic.IsActive);
        Assert.Equal(DocumentParticipationKind.ShadowedByHigherLayerPath, diagnostic.Participation.Kind);
        Assert.DoesNotContain(snapshot.Semantics.Diagnostics, item => item.Code == "OXIDE4001");
        Assert.Equal(SemanticEntityStatus.Effective, snapshot.Semantics.States[1].Status);
    }

    [Fact]
    public async Task Missing_root_is_reported_in_a_published_inspectable_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        var missingRoot = Path.Combine(fixture.Root, "missing");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(missingRoot));

        Assert.Empty(snapshot.Documents);
        var diagnostic = Assert.Single(snapshot.Diagnostics);
        Assert.Equal("OXIDE3001", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Same(snapshot, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Cancelled_reload_leaves_the_previous_snapshot_published()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReloadAsync(cancellationToken: cancellation.Token));

        Assert.Same(original, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Failed_reload_leaves_the_previous_snapshot_published()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var progress = new InlineProgress<WorkspaceLoadProgress>(report =>
        {
            if (report.Stage is WorkspaceLoadStage.LoadingDocuments)
            {
                throw new ExpectedLoadFailureException();
            }
        });

        await Assert.ThrowsAsync<ExpectedLoadFailureException>(() => service.ReloadAsync(progress));

        Assert.Same(original, service.CurrentSnapshot);
        Assert.Equal(original.Version, service.CurrentSnapshot!.Version);
    }

    [Fact]
    public async Task File_removed_after_discovery_becomes_a_failed_document_diagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        var removedPath = fixture.WriteGameFile("history/states/1-Vanishing.txt", "state={ id=1 }");
        fixture.WriteGameFile("history/states/2-Stable.txt", "state={ id=2 }");
        using var service = new WorkspaceService();
        var removed = false;
        var progress = new InlineProgress<WorkspaceLoadProgress>(report =>
        {
            if (!removed && report.Stage is WorkspaceLoadStage.LoadingDocuments)
            {
                File.Delete(removedPath);
                removed = true;
            }
        });

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot), progress);

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.Single(snapshot.Documents, document => document.LoadStatus is DocumentLoadStatus.Failed);
        Assert.Single(snapshot.Documents, document => document.IsLoaded);
        Assert.Contains(snapshot.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
    }

    [Fact]
    public async Task Reload_reflects_added_changed_and_removed_files_atomically()
    {
        using var fixture = new TemporaryWorkspace();
        var firstPath = fixture.WriteGameFile("history/states/1-First.txt", "state={ id=1 name=OLD }");
        using var service = new WorkspaceService();
        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        File.WriteAllText(firstPath, "state={ id=1 name=NEW }");
        fixture.WriteGameFile("history/states/2-Second.txt", "state={ id=2 }");
        var second = await service.ReloadAsync();

        File.Delete(firstPath);
        var third = await service.ReloadAsync();

        Assert.Equal("OLD", first.Semantics.States[1].Name?.Value);
        Assert.Equal("NEW", second.Semantics.States[1].Name?.Value);
        Assert.True(second.Semantics.States.ContainsKey(2));
        Assert.False(third.Semantics.States.ContainsKey(1));
        Assert.True(third.Semantics.States.ContainsKey(2));
        Assert.Same(third, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Cancellation_during_reload_leaves_the_previous_snapshot_published()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        fixture.WriteGameFile("history/states/2-Test.txt", "state={ id=2 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<WorkspaceLoadProgress>(report =>
        {
            if (report.Stage is WorkspaceLoadStage.LoadingDocuments)
            {
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReloadAsync(progress, cancellation.Token));

        Assert.Same(original, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Load_reports_discovery_document_and_publication_stages()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var reports = new List<WorkspaceLoadProgress>();
        var progress = new InlineProgress<WorkspaceLoadProgress>(reports.Add);

        await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot), progress);

        Assert.Contains(reports, report => report.Stage is WorkspaceLoadStage.Discovering);
        Assert.Contains(reports, report => report.Stage is WorkspaceLoadStage.LoadingDocuments);
        Assert.Contains(reports, report => report.Stage is WorkspaceLoadStage.Publishing);
        Assert.Equal(WorkspaceLoadStage.Complete, reports[^1].Stage);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ExpectedLoadFailureException : Exception;

    private static string WriteLayerFile(string root, string virtualPath, string text)
    {
        var path = Path.Combine(root, virtualPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}

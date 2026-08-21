using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Loading;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task Open_discovers_supported_files_in_base_and_mod_layers()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TST=\"countries/Test.txt\"");
        fixture.WriteGameFile("events/ignored.txt", "country_event={ id=test.1 }");
        fixture.WriteModFile("history/states/2-Mod.txt", "state={ id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(
            fixture.GameRoot,
            fixture.ModRoot,
            "Fixture"));

        Assert.Equal("Fixture", snapshot.Configuration.DisplayName);
        Assert.Equal(2, snapshot.Layers.Length);
        Assert.Equal(3, snapshot.Documents.Length);
        Assert.Equal(2, snapshot.Documents.Count(document => document.Layer.Kind is ContentLayerKind.BaseGame));
        Assert.Single(snapshot.Documents, document => document.Layer.Kind is ContentLayerKind.ActiveMod);
        Assert.DoesNotContain(snapshot.Documents, document => document.VirtualPath.Value.StartsWith("events/", StringComparison.Ordinal));
        Assert.All(snapshot.Documents, document => Assert.True(document.IsLoaded));
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
    public async Task Same_virtual_path_in_multiple_layers_is_explicitly_unresolved()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-Collision.txt";
        fixture.WriteGameFile(virtualPath, "state={ id=1 owner=AAA }");
        fixture.WriteModFile(virtualPath, "state={ id=1 owner=BBB }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var candidates = snapshot.DocumentsByVirtualPath[new VirtualPath(virtualPath)];

        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, document =>
            Assert.Equal(DocumentContributionStatus.UnknownPrecedence, document.ContributionStatus));
        Assert.NotEqual(candidates[0].Id, candidates[1].Id);
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
}

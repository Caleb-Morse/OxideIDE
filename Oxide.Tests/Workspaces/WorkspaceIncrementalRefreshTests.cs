using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceIncrementalRefreshTests
{
    [Fact]
    public async Task Changed_document_is_reparsed_while_unchanged_document_instance_is_reused()
    {
        using var fixture = new TemporaryWorkspace();
        var changedPath = fixture.WriteGameFile("history/states/1-Changed.txt", "state={ id=1 name=OLD }");
        fixture.WriteGameFile("history/states/2-Unchanged.txt", "state={ id=2 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var changedBefore = original.Semantics.States[1].Contributions[0].DocumentId;
        var unchangedBefore = original.Documents.Single(document => document.VirtualPath.Value.Contains("Unchanged", StringComparison.Ordinal));
        File.WriteAllText(changedPath, "state={ id=1 name=NEW }");
        var change = DocumentChangeFor(original, WorkspaceChangeKind.Changed, changedPath, changedPath);

        var result = await service.RefreshAsync(Request(original, [change]));
        var refreshed = service.CurrentSnapshot!;

        Assert.Equal(WorkspaceRefreshOutcome.Published, result.Outcome);
        Assert.Equal("NEW", refreshed.Semantics.States[1].Name?.Value);
        Assert.Equal(changedBefore, refreshed.Semantics.States[1].Contributions[0].DocumentId);
        Assert.NotSame(original.DocumentsById[changedBefore], refreshed.DocumentsById[changedBefore]);
        Assert.Same(unchangedBefore, refreshed.DocumentsById[unchangedBefore.Id]);
        Assert.Equal(1, result.Metrics.DocumentsChanged);
        Assert.Equal(1, result.Metrics.DocumentsReparsed);
        Assert.Equal(1, result.Metrics.DocumentsReused);
        Assert.False(result.Metrics.UsedFullRescan);
    }

    [Fact]
    public async Task Created_and_deleted_documents_update_the_complete_candidate_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        var deletedPath = fixture.WriteGameFile("history/states/1-Deleted.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var deletedSource = original.Documents[0].SourceIdentity;
        File.Delete(deletedPath);
        var createdPath = fixture.WriteGameFile("history/states/2-Created.txt", "state={ id=2 }");
        var deleted = new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Deleted,
                deletedSource,
                null,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            SourceDocumentKind.Clausewitz,
            ContentCategory.StateHistory);
        var created = DocumentChangeFor(original, WorkspaceChangeKind.Created, null, createdPath);

        var result = await service.RefreshAsync(Request(original, [deleted, created]));
        var refreshed = service.CurrentSnapshot!;

        Assert.False(refreshed.Semantics.States.ContainsKey(1));
        Assert.True(refreshed.Semantics.States.ContainsKey(2));
        Assert.Equal(1, result.Metrics.DocumentsAdded);
        Assert.Equal(1, result.Metrics.DocumentsRemoved);
        Assert.Equal(1, result.Metrics.DocumentsReparsed);
    }

    [Fact]
    public async Task Rename_replaces_document_identity_and_preserves_semantic_content()
    {
        using var fixture = new TemporaryWorkspace();
        var previousPath = fixture.WriteGameFile("history/states/1-Before.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var currentPath = Path.Combine(fixture.GameRoot, "history", "states", "1-After.txt");
        File.Move(previousPath, currentPath);
        var rename = DocumentChangeFor(original, WorkspaceChangeKind.Renamed, previousPath, currentPath);

        var result = await service.RefreshAsync(Request(original, [rename]));
        var refreshed = service.CurrentSnapshot!;

        Assert.Single(refreshed.Semantics.States);
        Assert.DoesNotContain(original.Documents[0].Id, refreshed.DocumentsById.Keys);
        Assert.Contains(refreshed.Documents, document => document.VirtualPath.Value == "history/states/1-After.txt");
        Assert.Equal(1, result.Metrics.DocumentsAdded);
        Assert.Equal(1, result.Metrics.DocumentsRemoved);
    }

    [Fact]
    public async Task Failed_changed_file_remains_an_inspectable_failed_document()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.Delete(path);
        var changed = DocumentChangeFor(original, WorkspaceChangeKind.Changed, path, path);

        await service.RefreshAsync(Request(original, [changed]));
        var refreshed = service.CurrentSnapshot!;

        var document = Assert.Single(refreshed.Documents);
        Assert.Equal(DocumentLoadStatus.Failed, document.LoadStatus);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
    }

    [Fact]
    public async Task Removing_higher_same_path_document_recalculates_base_participation()
    {
        using var fixture = new TemporaryWorkspace();
        const string virtualPath = "history/states/1-Test.txt";
        fixture.WriteGameFile(virtualPath, "state={ id=1 name=BASE }");
        var modPath = fixture.WriteModFile(virtualPath, "state={ id=1 name=MOD }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var modDocument = original.Documents.Single(document => document.Layer.Kind is ContentLayerKind.Mod);
        File.Delete(modPath);
        var deleted = new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Deleted,
                modDocument.SourceIdentity,
                null,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            modDocument.Kind,
            modDocument.Participation.Category);

        await service.RefreshAsync(Request(original, [deleted]));
        var refreshed = service.CurrentSnapshot!;

        var remaining = Assert.Single(refreshed.Documents);
        Assert.Equal(DocumentParticipationKind.Participating, remaining.Participation.Kind);
        Assert.Equal("BASE", refreshed.Semantics.States[1].Name?.Value);
    }

    [Fact]
    public async Task Full_rescan_request_rediscovers_files_and_descriptor_participation()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 }");
        fixture.WriteModFile("history/states/2-Mod.txt", "state={ id=2 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        fixture.WriteModFile("descriptor.mod", "replace_path=\"history/states\"");
        fixture.WriteGameFile("history/states/3-New.txt", "state={ id=3 }");
        var batch = new WorkspaceChangeBatch([], true, "Descriptor changed.", rawEventCount: 1);
        var request = new IncrementalRefreshRequest(
            original.Version,
            WorkspaceRefreshTrigger.RecoveryFullRescan,
            batch);

        var result = await service.RefreshAsync(request);
        var refreshed = service.CurrentSnapshot!;

        Assert.True(result.Metrics.UsedFullRescan);
        Assert.Equal(3, refreshed.Documents.Length);
        Assert.All(
            refreshed.Documents.Where(document => document.Layer.Kind is ContentLayerKind.BaseGame),
            document => Assert.Equal(
                DocumentParticipationKind.ExcludedByReplacementPath,
                document.Participation.Kind));
    }

    [Fact]
    public async Task Stale_or_cancelled_refresh_cannot_replace_current_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var change = DocumentChangeFor(original, WorkspaceChangeKind.Changed, path, path);
        var stale = new IncrementalRefreshRequest(
            original.Version + 1,
            WorkspaceRefreshTrigger.Automatic,
            new WorkspaceChangeBatch([change]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync(stale));
        Assert.Same(original, service.CurrentSnapshot);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshAsync(
            Request(original, [change]),
            cancellationToken: cancellation.Token));
        Assert.Same(original, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Unknown_previous_document_identity_cannot_modify_the_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var unknownPath = Path.Combine(fixture.GameRoot, "history", "states", "999-Unknown.txt");
        var classified = WorkspaceChangeClassifier.Classify(original.Layers[0], unknownPath);
        var deletion = new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Deleted,
                classified.Source,
                null,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            classified.DocumentKind!.Value,
            classified.Category!.Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RefreshAsync(Request(original, [deletion])));

        Assert.Same(original, service.CurrentSnapshot);
        Assert.True(original.Semantics.States.ContainsKey(1));
    }

    private static IncrementalRefreshRequest Request(
        WorkspaceSnapshot snapshot,
        IEnumerable<DocumentChange> changes) =>
        new(
            snapshot.Version,
            WorkspaceRefreshTrigger.Automatic,
            new WorkspaceChangeBatch(changes));

    private static DocumentChange DocumentChangeFor(
        WorkspaceSnapshot snapshot,
        WorkspaceChangeKind kind,
        string? previousPath,
        string? currentPath)
    {
        var layerId = snapshot.Documents
            .FirstOrDefault(document =>
                string.Equals(document.PhysicalPath, previousPath ?? currentPath, StringComparison.Ordinal))
            ?.Layer.Id
            ?? snapshot.Layers[0].Id;
        var layer = snapshot.Layers.Single(candidate => candidate.Id == layerId);
        var previous = previousPath is null ? null : WorkspaceChangeClassifier.Classify(layer, previousPath);
        var current = currentPath is null ? null : WorkspaceChangeClassifier.Classify(layer, currentPath);
        var classification = current?.IsSupported is true ? current : previous!;
        return new DocumentChange(
            new WorkspaceChange(
                kind,
                kind is WorkspaceChangeKind.Created ? null : previous!.Source,
                kind is WorkspaceChangeKind.Deleted ? null : current!.Source,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            classification.DocumentKind!.Value,
            classification.Category!.Value);
    }
}

using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceRefreshContractTests
{
    [Theory]
    [InlineData("history/states/1-Test.txt", SourceDocumentKind.Clausewitz, ContentCategory.StateHistory)]
    [InlineData("map/strategicregions/1-Test.txt", SourceDocumentKind.Clausewitz, ContentCategory.StrategicRegion)]
    [InlineData("common/country_tags/00_tags.txt", SourceDocumentKind.Clausewitz, ContentCategory.CountryTags)]
    [InlineData("localisation/english/test_l_english.yml", SourceDocumentKind.Localisation, ContentCategory.Localisation)]
    public void Supported_paths_use_the_same_profile_as_full_discovery(
        string relativePath,
        SourceDocumentKind expectedKind,
        ContentCategory expectedCategory)
    {
        using var fixture = new TemporaryWorkspace();
        var layer = ContentLayer.BaseGame(fixture.GameRoot);
        var physicalPath = Path.Combine(fixture.GameRoot, relativePath);

        var result = WorkspaceChangeClassifier.Classify(layer, physicalPath);

        Assert.Equal(WorkspaceChangePathStatus.Supported, result.Status);
        Assert.Equal(expectedKind, result.DocumentKind);
        Assert.Equal(expectedCategory, result.Category);
        Assert.Equal(relativePath, result.Source!.VirtualPath.Value);
        Assert.Equal(DocumentId.Create(layer.Id, result.Source.VirtualPath), result.Source.DocumentId);
    }

    [Theory]
    [InlineData("events/test.txt")]
    [InlineData("history/states/nested/1-Test.txt")]
    [InlineData("history/states/1-Test.yml")]
    [InlineData("localisation/english/readme.txt")]
    public void Unsupported_paths_are_distinct_from_unsafe_paths(string relativePath)
    {
        using var fixture = new TemporaryWorkspace();
        var layer = ContentLayer.BaseGame(fixture.GameRoot);

        var result = WorkspaceChangeClassifier.Classify(
            layer,
            Path.Combine(fixture.GameRoot, relativePath));

        Assert.Equal(WorkspaceChangePathStatus.Unsupported, result.Status);
        Assert.NotNull(result.Source);
        Assert.Null(result.DocumentKind);
        Assert.Null(result.Category);
    }

    [Fact]
    public void Paths_outside_the_content_layer_cannot_become_source_identities()
    {
        using var fixture = new TemporaryWorkspace();
        var layer = ContentLayer.BaseGame(fixture.GameRoot);
        var siblingPath = Path.Combine(fixture.Root, "game-copy", "history", "states", "1-Escape.txt");

        var result = WorkspaceChangeClassifier.Classify(layer, siblingPath);

        Assert.Equal(WorkspaceChangePathStatus.OutsideContentLayer, result.Status);
        Assert.Null(result.Source);
    }

    [Fact]
    public void Create_change_requires_only_a_current_source()
    {
        using var fixture = new TemporaryWorkspace();
        var source = SupportedSource(fixture, "history/states/1-Created.txt");

        var change = new WorkspaceChange(
            WorkspaceChangeKind.Created,
            null,
            source.Source,
            DateTimeOffset.UnixEpoch,
            WorkspaceChangeOrigin.Watcher);

        Assert.Null(change.PreviousSource);
        Assert.Equal(source.Source, change.CurrentSource);
        Assert.Throws<ArgumentException>(() => new WorkspaceChange(
            WorkspaceChangeKind.Created,
            source.Source,
            source.Source,
            DateTimeOffset.UnixEpoch,
            WorkspaceChangeOrigin.Watcher));
    }

    [Fact]
    public void Delete_change_requires_only_a_previous_source()
    {
        using var fixture = new TemporaryWorkspace();
        var source = SupportedSource(fixture, "history/states/1-Deleted.txt");

        var change = new WorkspaceChange(
            WorkspaceChangeKind.Deleted,
            source.Source,
            null,
            DateTimeOffset.UnixEpoch,
            WorkspaceChangeOrigin.Watcher);

        Assert.Equal(source.Source, change.PreviousSource);
        Assert.Null(change.CurrentSource);
    }

    [Fact]
    public void Rename_retains_previous_and_current_stable_identities()
    {
        using var fixture = new TemporaryWorkspace();
        var previous = SupportedSource(fixture, "history/states/1-Before.txt");
        var current = SupportedSource(fixture, "history/states/1-After.txt");

        var change = new WorkspaceChange(
            WorkspaceChangeKind.Renamed,
            previous.Source,
            current.Source,
            DateTimeOffset.UnixEpoch,
            WorkspaceChangeOrigin.Watcher);

        Assert.NotEqual(change.PreviousSource!.DocumentId, change.CurrentSource!.DocumentId);
        Assert.Equal("history/states/1-Before.txt", change.PreviousSource.VirtualPath.Value);
        Assert.Equal("history/states/1-After.txt", change.CurrentSource.VirtualPath.Value);
    }

    [Fact]
    public void Replacement_save_can_be_represented_as_one_changed_document()
    {
        using var fixture = new TemporaryWorkspace();
        var source = SupportedSource(fixture, "localisation/english/test_l_english.yml");

        var change = new WorkspaceChange(
            WorkspaceChangeKind.Changed,
            source.Source,
            source.Source,
            DateTimeOffset.UnixEpoch,
            WorkspaceChangeOrigin.Watcher);

        Assert.Equal(change.PreviousSource!.DocumentId, change.CurrentSource!.DocumentId);
        Assert.Equal(change.PreviousSource.VirtualPath, change.CurrentSource.VirtualPath);
    }

    [Fact]
    public void Change_batch_is_deterministic_and_uncertainty_requests_a_rescan()
    {
        using var fixture = new TemporaryWorkspace();
        var later = SupportedDocumentChange(
            fixture,
            "history/states/2-Later.txt",
            WorkspaceChangeKind.Changed,
            DateTimeOffset.UnixEpoch.AddSeconds(2));
        var uncertain = SupportedDocumentChange(
            fixture,
            "history/states/1-Uncertain.txt",
            WorkspaceChangeKind.Uncertain,
            DateTimeOffset.UnixEpoch.AddSeconds(1));

        var batch = new WorkspaceChangeBatch([later, uncertain]);

        Assert.Equal([uncertain, later], batch.Changes.ToArray());
        Assert.True(batch.RequiresFullRescan);
        Assert.Contains("uncertain", batch.FullRescanReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(1), batch.FirstObservedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(2), batch.LastObservedAt);
    }

    [Fact]
    public void Explicit_full_rescan_requires_a_reason()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceChangeBatch([], requiresFullRescan: true));

        var batch = new WorkspaceChangeBatch([], requiresFullRescan: true, "Watcher buffer overflowed.");

        Assert.True(batch.RequiresFullRescan);
        Assert.Equal("Watcher buffer overflowed.", batch.FullRescanReason);
    }

    [Fact]
    public void One_raw_rename_may_expand_to_delete_and_create_changes()
    {
        using var fixture = new TemporaryWorkspace();
        var stateSource = SupportedSource(fixture, "history/states/1-Test.txt");
        var localisationSource = SupportedSource(fixture, "localisation/english/test_l_english.yml");
        var observedAt = DateTimeOffset.UnixEpoch;
        var deleted = new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Deleted,
                stateSource.Source,
                null,
                observedAt,
                WorkspaceChangeOrigin.Watcher),
            stateSource.DocumentKind!.Value,
            stateSource.Category!.Value);
        var created = new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Created,
                null,
                localisationSource.Source,
                observedAt,
                WorkspaceChangeOrigin.Watcher),
            localisationSource.DocumentKind!.Value,
            localisationSource.Category!.Value);

        var batch = new WorkspaceChangeBatch([deleted, created], rawEventCount: 1);

        Assert.Equal(1, batch.RawEventCount);
        Assert.Equal(2, batch.Changes.Length);
    }

    [Theory]
    [InlineData(WorkspaceRefreshTrigger.ConfigurationChanged)]
    [InlineData(WorkspaceRefreshTrigger.RecoveryFullRescan)]
    public void Configuration_and_recovery_requests_always_require_full_rescan(WorkspaceRefreshTrigger trigger)
    {
        var request = new IncrementalRefreshRequest(7, trigger, new WorkspaceChangeBatch([]));

        Assert.True(request.RequiresFullRescan);
        Assert.Equal(7, request.BaseSnapshotVersion);
    }

    private static WorkspaceChangePathResult SupportedSource(
        TemporaryWorkspace fixture,
        string relativePath)
    {
        var result = WorkspaceChangeClassifier.Classify(
            ContentLayer.BaseGame(fixture.GameRoot),
            Path.Combine(fixture.GameRoot, relativePath));
        Assert.True(result.IsSupported);
        return result;
    }

    private static DocumentChange SupportedDocumentChange(
        TemporaryWorkspace fixture,
        string relativePath,
        WorkspaceChangeKind kind,
        DateTimeOffset observedAt)
    {
        var source = SupportedSource(fixture, relativePath);
        var change = new WorkspaceChange(
            kind,
            source.Source,
            source.Source,
            observedAt,
            WorkspaceChangeOrigin.Watcher);
        return new DocumentChange(change, source.DocumentKind!.Value, source.Category!.Value);
    }
}

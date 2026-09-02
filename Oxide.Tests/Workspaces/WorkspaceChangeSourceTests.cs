using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceChangeSourceTests
{
    [Fact]
    public void Options_require_bounded_capacity_and_non_negative_debounce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceChangeSourceOptions(queueCapacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(-1)));

        var options = new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(25), 16);

        Assert.Equal(TimeSpan.FromMilliseconds(25), options.DebounceInterval);
        Assert.Equal(16, options.QueueCapacity);
    }

    [Fact]
    public void Repeated_create_and_change_events_coalesce_to_one_creation()
    {
        using var fixture = new TemporaryWorkspace();
        var created = CreateChange(fixture, WorkspaceChangeKind.Created, previous: false, current: true, 1);
        var changed = CreateChange(fixture, WorkspaceChangeKind.Changed, previous: true, current: true, 2);

        var batch = WorkspaceChangeCoalescer.Coalesce([created, changed], rawEventCount: 2);

        var result = Assert.Single(batch.Changes);
        Assert.Equal(WorkspaceChangeKind.Created, result.Change.Kind);
        Assert.Equal(2, batch.RawEventCount);
        Assert.Null(result.Change.PreviousSource);
        Assert.NotNull(result.Change.CurrentSource);
    }

    [Fact]
    public void Temporary_creation_deleted_in_the_same_burst_disappears()
    {
        using var fixture = new TemporaryWorkspace();
        var created = CreateChange(fixture, WorkspaceChangeKind.Created, previous: false, current: true, 1);
        var deleted = CreateChange(fixture, WorkspaceChangeKind.Deleted, previous: true, current: false, 2);

        var batch = WorkspaceChangeCoalescer.Coalesce([created, deleted], rawEventCount: 2);

        Assert.Empty(batch.Changes);
        Assert.Equal(2, batch.RawEventCount);
    }

    [Fact]
    public void Delete_then_create_for_one_identity_becomes_a_replacement_change()
    {
        using var fixture = new TemporaryWorkspace();
        var deleted = CreateChange(fixture, WorkspaceChangeKind.Deleted, previous: true, current: false, 1);
        var created = CreateChange(fixture, WorkspaceChangeKind.Created, previous: false, current: true, 2);

        var batch = WorkspaceChangeCoalescer.Coalesce([deleted, created], rawEventCount: 2);

        var result = Assert.Single(batch.Changes);
        Assert.Equal(WorkspaceChangeKind.Changed, result.Change.Kind);
        Assert.Equal(result.Change.PreviousSource!.DocumentId, result.Change.CurrentSource!.DocumentId);
    }

    [Fact]
    public async Task Deterministic_source_obeys_start_and_stop_lifecycle()
    {
        await using var source = new DeterministicWorkspaceChangeSource();
        var received = new List<WorkspaceChangeBatch>();
        source.ChangesAvailable += received.Add;
        var batch = new WorkspaceChangeBatch([], true, "Test recovery.");

        await source.StartAsync();
        source.Emit(batch);
        await source.StopAsync();

        Assert.False(source.IsRunning);
        Assert.Equal([batch], received);
        Assert.Throws<InvalidOperationException>(() => source.Emit(batch));
    }

    [Fact]
    public async Task Filesystem_source_publishes_supported_changes_and_ignores_unsupported_files()
    {
        using var fixture = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(fixture.GameRoot, "events"));
        Directory.CreateDirectory(Path.Combine(fixture.GameRoot, "history", "states"));
        var options = new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(75), 32);
        await using var source = new FileSystemWorkspaceChangeSource(
            new WorkspaceConfiguration(fixture.GameRoot),
            options);
        var completion = new TaskCompletionSource<WorkspaceChangeBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.ChangesAvailable += batch => completion.TrySetResult(batch);

        await source.StartAsync();
        fixture.WriteGameFile("events/ignored.txt", "country_event = { }");
        fixture.WriteGameFile("history/states/1-Watched.txt", "state = { id = 1 }");
        var batch = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var change = Assert.Single(batch.Changes, candidate =>
            candidate.Change.Source.VirtualPath.Value == "history/states/1-Watched.txt");
        Assert.Equal(ContentCategory.StateHistory, change.Category);
        Assert.DoesNotContain(batch.Changes, candidate =>
            candidate.Change.Source.VirtualPath.Value.StartsWith("events/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mod_descriptor_change_requests_a_safe_full_rescan()
    {
        using var fixture = new TemporaryWorkspace();
        await using var source = new FileSystemWorkspaceChangeSource(
            new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot),
            new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(50), 32));
        var completion = new TaskCompletionSource<WorkspaceChangeBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.ChangesAvailable += batch =>
        {
            if (batch.RequiresFullRescan)
            {
                completion.TrySetResult(batch);
            }
        };

        await source.StartAsync();
        fixture.WriteModFile("descriptor.mod", "replace_path = \"history/states\"");
        var batch = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(batch.RequiresFullRescan);
        Assert.Contains("descriptor", batch.FullRescanReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_content_root_reports_error_and_requests_recovery_rescan()
    {
        using var fixture = new TemporaryWorkspace();
        var missingRoot = Path.Combine(fixture.Root, "missing-game");
        await using var source = new FileSystemWorkspaceChangeSource(
            new WorkspaceConfiguration(missingRoot),
            new WorkspaceChangeSourceOptions(TimeSpan.Zero, 8));
        var errorCompletion = new TaskCompletionSource<WorkspaceChangeSourceError>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var batchCompletion = new TaskCompletionSource<WorkspaceChangeBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.Error += error => errorCompletion.TrySetResult(error);
        source.ChangesAvailable += batch => batchCompletion.TrySetResult(batch);

        await source.StartAsync();
        var error = await errorCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var batch = await batchCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<DirectoryNotFoundException>(error.Exception);
        Assert.True(batch.RequiresFullRescan);
        Assert.Contains("uncertain", batch.FullRescanReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stopped_source_cannot_publish_later_workspace_changes()
    {
        using var fixture = new TemporaryWorkspace();
        await using var source = new FileSystemWorkspaceChangeSource(
            new WorkspaceConfiguration(fixture.GameRoot),
            new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(25), 16));
        var published = 0;
        source.ChangesAvailable += _ => Interlocked.Increment(ref published);

        await source.StartAsync();
        await source.StopAsync();
        fixture.WriteGameFile("history/states/1-AfterStop.txt", "state = { id = 1 }");
        await Task.Delay(150);

        Assert.Equal(0, Volatile.Read(ref published));
    }

    [Fact]
    public async Task Bounded_queue_overflow_requests_one_safe_full_rescan()
    {
        using var fixture = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(fixture.GameRoot, "history", "states"));
        await using var source = new FileSystemWorkspaceChangeSource(
            new WorkspaceConfiguration(fixture.GameRoot),
            new WorkspaceChangeSourceOptions(TimeSpan.FromMilliseconds(300), queueCapacity: 1));
        var completion = new TaskCompletionSource<WorkspaceChangeBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.ChangesAvailable += batch =>
        {
            if (batch.RequiresFullRescan)
            {
                completion.TrySetResult(batch);
            }
        };

        await source.StartAsync();
        for (var index = 0; index < 20; index++)
        {
            fixture.WriteGameFile($"history/states/{index + 1}-Burst.txt", $"state = {{ id = {index + 1} }}");
        }

        var batch = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(batch.RequiresFullRescan);
        Assert.Contains("overflow", batch.FullRescanReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(batch.RawEventCount > batch.Changes.Length);
    }

    private static DocumentChange CreateChange(
        TemporaryWorkspace fixture,
        WorkspaceChangeKind kind,
        bool previous,
        bool current,
        int seconds)
    {
        var classified = WorkspaceChangeClassifier.Classify(
            ContentLayer.BaseGame(fixture.GameRoot),
            Path.Combine(fixture.GameRoot, "history/states/1-Test.txt"));
        var source = classified.Source!;
        return new DocumentChange(
            new WorkspaceChange(
                kind,
                previous ? source : null,
                current ? source : null,
                DateTimeOffset.UnixEpoch.AddSeconds(seconds),
                WorkspaceChangeOrigin.Watcher),
            classified.DocumentKind!.Value,
            classified.Category!.Value);
    }
}

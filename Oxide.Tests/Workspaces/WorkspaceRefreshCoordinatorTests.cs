using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceRefreshCoordinatorTests
{
    [Fact]
    public async Task Automatic_change_refreshes_and_publishes_the_updated_workspace()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=OLD }");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        var upToDate = StatusCompletion(coordinator, WorkspaceRefreshCoordinatorState.UpToDate);
        await coordinator.StartAsync(source);
        File.WriteAllText(path, "state={ id=1 name=NEW }");

        source.Emit(BatchFor(original, path));
        await upToDate.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("NEW", service.CurrentSnapshot!.Semantics.States[1].Name?.Value);
        Assert.Equal(original.Version + 1, service.CurrentSnapshot.Version);
        Assert.Equal(WorkspaceRefreshOutcome.Published, coordinator.Status.LastRefresh?.Outcome);
    }

    [Fact]
    public async Task Manual_reload_cancels_active_incremental_work_and_takes_priority()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=OLD }");
        using var inner = new WorkspaceService();
        var original = await inner.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var service = new BlockingRefreshWorkspaceService(inner);
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        await coordinator.StartAsync(source);
        File.WriteAllText(path, "state={ id=1 name=NEW }");
        source.Emit(BatchFor(original, path));
        await service.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reloaded = await coordinator.ReloadAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(service.RefreshWasCancelled);
        Assert.Equal(1, service.MaximumActiveOperations);
        Assert.Equal("NEW", reloaded.Semantics.States[1].Name?.Value);
        Assert.Same(reloaded, inner.CurrentSnapshot);
    }

    [Fact]
    public async Task Replacing_change_source_stops_old_source_and_ignores_its_queued_work()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        await using var first = new DeterministicWorkspaceChangeSource();
        await using var second = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        await coordinator.StartAsync(first);

        await coordinator.ReplaceChangeSourceAsync(second);

        Assert.False(first.IsRunning);
        Assert.True(second.IsRunning);
        Assert.Throws<InvalidOperationException>(() => first.Emit(
            new WorkspaceChangeBatch([], true, "Old source recovery.")));
        Assert.Equal(WorkspaceRefreshCoordinatorState.Watching, coordinator.Status.State);
    }

    [Fact]
    public async Task Source_errors_are_observable_and_subscriber_failures_do_not_stop_coordination()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(service);
        coordinator.StatusChanged += _ => throw new InvalidOperationException("Observer failure");
        await coordinator.StartAsync(source);
        var error = new WorkspaceChangeSourceError("Watcher overflowed.");

        source.EmitError(error);

        Assert.Equal(WorkspaceRefreshCoordinatorState.WatcherUnavailable, coordinator.Status.State);
        Assert.Same(error, coordinator.Status.LastSourceError);
        Assert.True(source.IsRunning);
    }

    [Fact]
    public void Options_reject_an_unbounded_or_empty_queue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceRefreshCoordinatorOptions(0));
        Assert.Equal(4, new WorkspaceRefreshCoordinatorOptions(4).QueueCapacity);
    }

    [Fact]
    public async Task Queue_overflow_stays_serial_and_recovers_with_one_full_rescan()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var inner = new WorkspaceService();
        var original = await inner.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var service = new GatedRecordingWorkspaceService(inner);
        await using var source = new DeterministicWorkspaceChangeSource();
        await using var coordinator = new WorkspaceRefreshCoordinator(
            service,
            new WorkspaceRefreshCoordinatorOptions(queueCapacity: 1));
        var current = StatusCompletion(coordinator, WorkspaceRefreshCoordinatorState.UpToDate);
        await coordinator.StartAsync(source);
        var batch = BatchFor(original, path);
        source.Emit(batch);
        await service.FirstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        for (var index = 0; index < 20; index++)
        {
            source.Emit(batch);
        }

        service.ReleaseFirstRefresh.TrySetResult();
        await service.TwoRefreshesCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await current.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, service.MaximumActiveOperations);
        Assert.Equal(2, service.Requests.Count);
        Assert.False(service.Requests[0].RequiresFullRescan);
        Assert.True(service.Requests[1].RequiresFullRescan);
        Assert.Equal(WorkspaceRefreshTrigger.RecoveryFullRescan, service.Requests[1].Trigger);
        Assert.InRange(coordinator.Status.PendingCommandCount, 0, 1);
    }

    private static TaskCompletionSource<WorkspaceRefreshCoordinatorStatus> StatusCompletion(
        WorkspaceRefreshCoordinator coordinator,
        WorkspaceRefreshCoordinatorState expected)
    {
        var completion = new TaskCompletionSource<WorkspaceRefreshCoordinatorStatus>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.StatusChanged += status =>
        {
            if (status.State == expected)
            {
                completion.TrySetResult(status);
            }
        };
        return completion;
    }

    private static WorkspaceChangeBatch BatchFor(WorkspaceSnapshot snapshot, string path)
    {
        var layer = snapshot.Layers[0];
        var classified = WorkspaceChangeClassifier.Classify(layer, path);
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

    private sealed class BlockingRefreshWorkspaceService(WorkspaceService inner) : IWorkspaceService
    {
        private int activeOperations;
        private int maximumActiveOperations;

        public WorkspaceSnapshot? CurrentSnapshot => inner.CurrentSnapshot;

        public TaskCompletionSource RefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool RefreshWasCancelled { get; private set; }

        public int MaximumActiveOperations => Volatile.Read(ref maximumActiveOperations);

        public event Action<WorkspaceSnapshot>? SnapshotPublished
        {
            add => inner.SnapshotPublished += value;
            remove => inner.SnapshotPublished -= value;
        }

        public Task<WorkspaceSnapshot> OpenAsync(
            WorkspaceConfiguration configuration,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            inner.OpenAsync(configuration, progress, cancellationToken);

        public async Task<WorkspaceSnapshot> ReloadAsync(
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            EnterOperation();
            try
            {
                return await inner.ReloadAsync(progress, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref activeOperations);
            }
        }

        public async Task<WorkspaceRefreshResult> RefreshAsync(
            IncrementalRefreshRequest request,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            EnterOperation();
            RefreshStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return await inner.RefreshAsync(request, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RefreshWasCancelled = true;
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref activeOperations);
            }
        }

        private void EnterOperation()
        {
            var active = Interlocked.Increment(ref activeOperations);
            var observed = Volatile.Read(ref maximumActiveOperations);
            while (active > observed)
            {
                var previous = Interlocked.CompareExchange(ref maximumActiveOperations, active, observed);
                if (previous == observed)
                {
                    break;
                }

                observed = previous;
            }
        }
    }

    private sealed class GatedRecordingWorkspaceService(WorkspaceService inner) : IWorkspaceService
    {
        private int activeOperations;
        private int maximumActiveOperations;
        private int refreshCount;

        public WorkspaceSnapshot? CurrentSnapshot => inner.CurrentSnapshot;

        public List<IncrementalRefreshRequest> Requests { get; } = [];

        public TaskCompletionSource FirstRefreshStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRefresh { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource TwoRefreshesCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaximumActiveOperations => Volatile.Read(ref maximumActiveOperations);

        public event Action<WorkspaceSnapshot>? SnapshotPublished
        {
            add => inner.SnapshotPublished += value;
            remove => inner.SnapshotPublished -= value;
        }

        public Task<WorkspaceSnapshot> OpenAsync(
            WorkspaceConfiguration configuration,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            inner.OpenAsync(configuration, progress, cancellationToken);

        public Task<WorkspaceSnapshot> ReloadAsync(
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            inner.ReloadAsync(progress, cancellationToken);

        public async Task<WorkspaceRefreshResult> RefreshAsync(
            IncrementalRefreshRequest request,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            var active = Interlocked.Increment(ref activeOperations);
            UpdateMaximum(active);
            var ordinal = Interlocked.Increment(ref refreshCount);
            try
            {
                if (ordinal == 1)
                {
                    FirstRefreshStarted.TrySetResult();
                    await ReleaseFirstRefresh.Task.WaitAsync(cancellationToken);
                }

                return await inner.RefreshAsync(request, progress, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref activeOperations);
                if (ordinal >= 2)
                {
                    TwoRefreshesCompleted.TrySetResult();
                }
            }
        }

        private void UpdateMaximum(int active)
        {
            var observed = Volatile.Read(ref maximumActiveOperations);
            while (active > observed)
            {
                var previous = Interlocked.CompareExchange(ref maximumActiveOperations, active, observed);
                if (previous == observed)
                {
                    return;
                }

                observed = previous;
            }
        }
    }
}

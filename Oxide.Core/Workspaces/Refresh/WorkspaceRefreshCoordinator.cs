using System.Threading.Channels;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Workspaces.Refresh;

public sealed class WorkspaceRefreshCoordinator : IAsyncDisposable
{
    private const string OverflowReason = "The refresh queue reached its bounded capacity; a full rescan is required.";
    private readonly IWorkspaceService workspaceService;
    private readonly Channel<CoordinatorCommand> commands;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private readonly Task worker;
    private IWorkspaceChangeSource? changeSource;
    private CancellationTokenSource? activeOperation;
    private WorkspaceRefreshCoordinatorStatus status = new(
        WorkspaceRefreshCoordinatorState.Stopped,
        "Refresh coordination is stopped.");
    private long nextSequence;
    private long sourceGeneration;
    private int queuedCommandCount;
    private int overflowed;
    private bool disposed;

    public WorkspaceRefreshCoordinator(
        IWorkspaceService workspaceService,
        WorkspaceRefreshCoordinatorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceService);
        this.workspaceService = workspaceService;
        var capacity = (options ?? new WorkspaceRefreshCoordinatorOptions()).QueueCapacity;
        commands = Channel.CreateBounded<CoordinatorCommand>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        worker = ProcessCommandsAsync(lifetime.Token);
    }

    public WorkspaceRefreshCoordinatorStatus Status
    {
        get
        {
            lock (sync)
            {
                return status;
            }
        }
    }

    public event Action<WorkspaceRefreshCoordinatorStatus>? StatusChanged;

    public async Task StartAsync(
        IWorkspaceChangeSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(disposed, this);
        await ReplaceChangeSourceAsync(source, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceChangeSourceAsync(
        IWorkspaceChangeSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(disposed, this);
        IWorkspaceChangeSource? previous;
        lock (sync)
        {
            previous = changeSource;
            changeSource = null;
            sourceGeneration++;
            activeOperation?.Cancel();
        }

        if (previous is not null)
        {
            Unsubscribe(previous);
            await previous.StopAsync().ConfigureAwait(false);
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            changeSource = source;
        }

        Subscribe(source);
        try
        {
            await source.StartAsync(cancellationToken).ConfigureAwait(false);
            PublishStatus(WorkspaceRefreshCoordinatorState.Watching, "Watching for workspace changes.");
        }
        catch
        {
            Unsubscribe(source);
            lock (sync)
            {
                if (ReferenceEquals(changeSource, source))
                {
                    changeSource = null;
                }
            }

            PublishStatus(WorkspaceRefreshCoordinatorState.WatcherUnavailable, "The workspace watcher could not be started.");
            throw;
        }
    }

    public async Task<WorkspaceSnapshot> ReloadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var completion = new TaskCompletionSource<WorkspaceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ReloadCommand(Interlocked.Increment(ref nextSequence), SourceGeneration, completion);
        CancelActiveOperation();
        await commands.Writer.WriteAsync(command, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref queuedCommandCount);
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        if (disposed)
        {
            return;
        }

        IWorkspaceChangeSource? source;
        lock (sync)
        {
            source = changeSource;
            changeSource = null;
            sourceGeneration++;
            activeOperation?.Cancel();
        }

        if (source is not null)
        {
            Unsubscribe(source);
            await source.StopAsync().ConfigureAwait(false);
            await source.DisposeAsync().ConfigureAwait(false);
        }

        PublishStatus(WorkspaceRefreshCoordinatorState.Stopped, "Refresh coordination is stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        disposed = true;
        lifetime.Cancel();
        commands.Writer.TryComplete();
        try
        {
            await worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        lifetime.Dispose();
        activeOperation?.Dispose();
    }

    private long SourceGeneration
    {
        get
        {
            lock (sync)
            {
                return sourceGeneration;
            }
        }
    }

    private void Subscribe(IWorkspaceChangeSource source)
    {
        source.ChangesAvailable += OnChangesAvailable;
        source.Error += OnSourceError;
    }

    private void Unsubscribe(IWorkspaceChangeSource source)
    {
        source.ChangesAvailable -= OnChangesAvailable;
        source.Error -= OnSourceError;
    }

    private void OnChangesAvailable(WorkspaceChangeBatch batch)
    {
        var command = new BatchCommand(Interlocked.Increment(ref nextSequence), SourceGeneration, batch);
        QueueCommand(command);
        PublishStatus(WorkspaceRefreshCoordinatorState.ChangesPending, "Workspace changes are waiting to be refreshed.");
    }

    private void OnSourceError(WorkspaceChangeSourceError error)
    {
        PublishStatus(
            WorkspaceRefreshCoordinatorState.WatcherUnavailable,
            error.Message,
            lastSourceError: error);
    }

    private void QueueCommand(CoordinatorCommand command)
    {
        if (commands.Writer.TryWrite(command))
        {
            Interlocked.Increment(ref queuedCommandCount);
            return;
        }

        Interlocked.Exchange(ref overflowed, 1);
    }

    private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var first in commands.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref queuedCommandCount);
                var drained = new List<CoordinatorCommand> { first };
                while (commands.Reader.TryRead(out var command))
                {
                    Interlocked.Decrement(ref queuedCommandCount);
                    drained.Add(command);
                }

                var generation = SourceGeneration;
                var current = drained.Where(command => command.Generation == generation).ToArray();
                CancelSupersededReloads(drained, current);
                var reloads = current.OfType<ReloadCommand>().ToArray();
                if (reloads.Length > 0)
                {
                    await ProcessReloadAsync(reloads, cancellationToken).ConfigureAwait(false);
                    var latestReload = reloads.Max(command => command.Sequence);
                    current = current.Where(command => command.Sequence > latestReload).ToArray();
                }

                var batches = current.OfType<BatchCommand>().ToArray();
                var overflow = Interlocked.Exchange(ref overflowed, 0) == 1;
                if (batches.Length > 0 || overflow)
                {
                    await ProcessBatchesAsync(batches, generation, overflow, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            while (commands.Reader.TryRead(out var command))
            {
                if (command is ReloadCommand reload)
                {
                    reload.Completion.TrySetCanceled(cancellationToken);
                }
            }
        }
    }

    private async Task ProcessReloadAsync(
        IReadOnlyCollection<ReloadCommand> reloads,
        CancellationToken cancellationToken)
    {
        PublishStatus(WorkspaceRefreshCoordinatorState.Refreshing, "Reloading the complete workspace.");
        using var operation = BeginOperation(cancellationToken);
        try
        {
            var snapshot = await workspaceService.ReloadAsync(cancellationToken: operation.Token).ConfigureAwait(false);
            foreach (var reload in reloads)
            {
                reload.Completion.TrySetResult(snapshot);
            }

            PublishStatus(WorkspaceRefreshCoordinatorState.UpToDate, "The workspace is up to date.");
        }
        catch (OperationCanceledException exception)
        {
            foreach (var reload in reloads)
            {
                reload.Completion.TrySetCanceled(exception.CancellationToken);
            }
        }
        catch (Exception exception)
        {
            foreach (var reload in reloads)
            {
                reload.Completion.TrySetException(exception);
            }

            PublishStatus(WorkspaceRefreshCoordinatorState.RefreshFailed, exception.Message);
        }
        finally
        {
            EndOperation(operation);
        }
    }

    private async Task ProcessBatchesAsync(
        IReadOnlyCollection<BatchCommand> batches,
        long generation,
        bool overflow,
        CancellationToken cancellationToken)
    {
        var changes = batches.SelectMany(command => command.Batch.Changes);
        var rawEventCount = batches.Sum(command => command.Batch.RawEventCount);
        var fullRescan = overflow || batches.Any(command => command.Batch.RequiresFullRescan);
        var reason = overflow
            ? OverflowReason
            : batches.Select(command => command.Batch.FullRescanReason).FirstOrDefault(value => value is not null);
        var batch = WorkspaceChangeCoalescer.Coalesce(changes, rawEventCount, fullRescan, reason);
        var snapshot = workspaceService.CurrentSnapshot;
        if (snapshot is null || generation != SourceGeneration)
        {
            return;
        }

        PublishStatus(WorkspaceRefreshCoordinatorState.Refreshing, "Refreshing workspace changes.");
        using var operation = BeginOperation(cancellationToken);
        try
        {
            var trigger = fullRescan
                ? WorkspaceRefreshTrigger.RecoveryFullRescan
                : WorkspaceRefreshTrigger.Automatic;
            var result = await workspaceService.RefreshAsync(
                new IncrementalRefreshRequest(snapshot.Version, trigger, batch),
                cancellationToken: operation.Token).ConfigureAwait(false);
            if (generation == SourceGeneration)
            {
                PublishStatus(
                    result.Outcome is WorkspaceRefreshOutcome.Published
                        ? WorkspaceRefreshCoordinatorState.UpToDate
                        : WorkspaceRefreshCoordinatorState.RefreshFailed,
                    result.Outcome is WorkspaceRefreshOutcome.Published
                        ? "The workspace is up to date."
                        : "The workspace refresh did not publish a snapshot.",
                    lastRefresh: result);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            PublishStatus(WorkspaceRefreshCoordinatorState.RefreshFailed, exception.Message);
        }
        finally
        {
            EndOperation(operation);
        }
    }

    private CancellationTokenSource BeginOperation(CancellationToken cancellationToken)
    {
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (sync)
        {
            activeOperation = operation;
        }

        return operation;
    }

    private void EndOperation(CancellationTokenSource operation)
    {
        lock (sync)
        {
            if (ReferenceEquals(activeOperation, operation))
            {
                activeOperation = null;
            }
        }
    }

    private void CancelActiveOperation()
    {
        lock (sync)
        {
            activeOperation?.Cancel();
        }
    }

    private void PublishStatus(
        WorkspaceRefreshCoordinatorState state,
        string message,
        WorkspaceRefreshResult? lastRefresh = null,
        WorkspaceChangeSourceError? lastSourceError = null)
    {
        WorkspaceRefreshCoordinatorStatus next;
        lock (sync)
        {
            next = new WorkspaceRefreshCoordinatorStatus(
                state,
                message,
                Math.Max(0, Volatile.Read(ref queuedCommandCount)),
                lastRefresh ?? status.LastRefresh,
                lastSourceError ?? status.LastSourceError);
            status = next;
        }

        try
        {
            StatusChanged?.Invoke(next);
        }
        catch
        {
            // Observers cannot terminate refresh coordination.
        }
    }

    private static void CancelSupersededReloads(
        IEnumerable<CoordinatorCommand> drained,
        IReadOnlyCollection<CoordinatorCommand> current)
    {
        var retained = current.ToHashSet();
        foreach (var reload in drained.OfType<ReloadCommand>().Where(command => !retained.Contains(command)))
        {
            reload.Completion.TrySetCanceled();
        }
    }

    private abstract record CoordinatorCommand(long Sequence, long Generation);

    private sealed record BatchCommand(long Sequence, long Generation, WorkspaceChangeBatch Batch)
        : CoordinatorCommand(Sequence, Generation);

    private sealed record ReloadCommand(
        long Sequence,
        long Generation,
        TaskCompletionSource<WorkspaceSnapshot> Completion)
        : CoordinatorCommand(Sequence, Generation);
}

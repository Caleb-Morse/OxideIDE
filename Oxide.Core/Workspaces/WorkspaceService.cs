using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Core.Workspaces;

public sealed class WorkspaceService : IWorkspaceService, IDisposable
{
    private readonly WorkspaceLoader loader = new();
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private WorkspaceSnapshot? currentSnapshot;
    private long nextVersion;
    private bool disposed;

    public WorkspaceSnapshot? CurrentSnapshot => Volatile.Read(ref currentSnapshot);

    public event Action<WorkspaceSnapshot>? SnapshotPublished;

    public Task<WorkspaceSnapshot> OpenAsync(
        WorkspaceConfiguration configuration,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return LoadAndPublishAsync(configuration, progress, cancellationToken);
    }

    public Task<WorkspaceSnapshot> ReloadAsync(
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = CurrentSnapshot?.Configuration
            ?? throw new InvalidOperationException("A workspace must be opened before it can be reloaded.");
        return LoadAndPublishAsync(configuration, progress, cancellationToken);
    }

    public async Task<WorkspaceRefreshResult> RefreshAsync(
        IncrementalRefreshRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(disposed, this);
        await loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previousSnapshot = CurrentSnapshot
                ?? throw new InvalidOperationException("A workspace must be opened before it can be refreshed.");
            var version = Interlocked.Increment(ref nextVersion);
            var loaded = await loader
                .RefreshAsync(version, previousSnapshot, request, progress, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = loaded.Snapshot;
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.Publishing,
                snapshot.Documents.Length,
                snapshot.Documents.Length,
                ElapsedMilliseconds: snapshot.LoadMetrics.TotalMilliseconds,
                DiagnosticCount: snapshot.Diagnostics.Length + snapshot.Semantics.Diagnostics.Length));
            var publicationStart = System.Diagnostics.Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref currentSnapshot, snapshot);
            SnapshotPublished?.Invoke(snapshot);
            var publicationElapsed = System.Diagnostics.Stopwatch.GetElapsedTime(publicationStart);
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.Complete,
                snapshot.Documents.Length,
                snapshot.Documents.Length,
                ElapsedMilliseconds: snapshot.LoadMetrics.TotalMilliseconds,
                DiagnosticCount: snapshot.Diagnostics.Length + snapshot.Semantics.Diagnostics.Length));
            return new WorkspaceRefreshResult(
                request,
                WorkspaceRefreshOutcome.Published,
                previousSnapshot.Version,
                snapshot.Version,
                loaded.Metrics with
                {
                    PublicationMilliseconds = publicationElapsed.TotalMilliseconds,
                    TotalMilliseconds = snapshot.LoadMetrics.TotalMilliseconds
                        + publicationElapsed.TotalMilliseconds,
                },
                snapshot.Diagnostics);
        }
        finally
        {
            loadGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        loadGate.Dispose();
        disposed = true;
    }

    private async Task<WorkspaceSnapshot> LoadAndPublishAsync(
        WorkspaceConfiguration configuration,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = Interlocked.Increment(ref nextVersion);
            var snapshot = await loader
                .LoadAsync(version, configuration, progress, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.Publishing,
                snapshot.Documents.Length,
                snapshot.Documents.Length,
                ElapsedMilliseconds: snapshot.LoadMetrics.TotalMilliseconds,
                DiagnosticCount: snapshot.Diagnostics.Length + snapshot.Semantics.Diagnostics.Length));
            Interlocked.Exchange(ref currentSnapshot, snapshot);
            SnapshotPublished?.Invoke(snapshot);
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.Complete,
                snapshot.Documents.Length,
                snapshot.Documents.Length,
                ElapsedMilliseconds: snapshot.LoadMetrics.TotalMilliseconds,
                DiagnosticCount: snapshot.Diagnostics.Length + snapshot.Semantics.Diagnostics.Length));
            return snapshot;
        }
        finally
        {
            loadGate.Release();
        }
    }
}

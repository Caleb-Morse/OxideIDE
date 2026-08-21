using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Snapshots;

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
                snapshot.Documents.Length));
            Interlocked.Exchange(ref currentSnapshot, snapshot);
            SnapshotPublished?.Invoke(snapshot);
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.Complete,
                snapshot.Documents.Length,
                snapshot.Documents.Length));
            return snapshot;
        }
        finally
        {
            loadGate.Release();
        }
    }
}

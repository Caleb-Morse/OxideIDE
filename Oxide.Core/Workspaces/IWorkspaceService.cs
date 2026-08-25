using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Core.Workspaces;

public interface IWorkspaceService
{
    WorkspaceSnapshot? CurrentSnapshot { get; }

    event Action<WorkspaceSnapshot>? SnapshotPublished;

    Task<WorkspaceSnapshot> OpenAsync(
        WorkspaceConfiguration configuration,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSnapshot> ReloadAsync(
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceRefreshResult> RefreshAsync(
        IncrementalRefreshRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Workspaces.Refresh;

internal sealed record WorkspaceRefreshLoadResult(
    WorkspaceSnapshot Snapshot,
    WorkspaceRefreshMetrics Metrics);

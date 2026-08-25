namespace Oxide.Core.Workspaces.Refresh;

public enum WorkspaceRefreshCoordinatorState
{
    Stopped,
    Watching,
    ChangesPending,
    Refreshing,
    UpToDate,
    RefreshFailed,
    WatcherUnavailable,
}

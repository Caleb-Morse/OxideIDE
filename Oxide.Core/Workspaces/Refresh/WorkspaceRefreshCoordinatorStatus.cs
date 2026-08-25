namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceRefreshCoordinatorStatus(
    WorkspaceRefreshCoordinatorState State,
    string Message,
    int PendingCommandCount = 0,
    WorkspaceRefreshResult? LastRefresh = null,
    WorkspaceChangeSourceError? LastSourceError = null);

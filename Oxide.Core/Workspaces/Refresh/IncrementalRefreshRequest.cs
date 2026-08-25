namespace Oxide.Core.Workspaces.Refresh;

public sealed record IncrementalRefreshRequest(
    long BaseSnapshotVersion,
    WorkspaceRefreshTrigger Trigger,
    WorkspaceChangeBatch Changes)
{
    public bool RequiresFullRescan =>
        Trigger is WorkspaceRefreshTrigger.ConfigurationChanged or WorkspaceRefreshTrigger.RecoveryFullRescan
        || Changes.RequiresFullRescan;
}

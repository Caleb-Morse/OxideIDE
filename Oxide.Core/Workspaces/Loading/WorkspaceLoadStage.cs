namespace Oxide.Core.Workspaces.Loading;

public enum WorkspaceLoadStage
{
    Discovering,
    LoadingDocuments,
    BuildingSemantics,
    Publishing,
    Complete,
}

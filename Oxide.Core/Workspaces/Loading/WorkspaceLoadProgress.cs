namespace Oxide.Core.Workspaces.Loading;

public sealed record WorkspaceLoadProgress(
    WorkspaceLoadStage Stage,
    int ProcessedDocuments,
    int TotalDocuments,
    string? CurrentPath = null);

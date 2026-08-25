using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceChangePathResult(
    WorkspaceChangePathStatus Status,
    SourceIdentity? Source,
    SourceDocumentKind? DocumentKind,
    ContentCategory? Category)
{
    public bool IsSupported => Status is WorkspaceChangePathStatus.Supported;
}

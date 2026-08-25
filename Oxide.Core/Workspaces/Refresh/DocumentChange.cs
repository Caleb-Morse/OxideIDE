using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record DocumentChange(
    WorkspaceChange Change,
    SourceDocumentKind DocumentKind,
    ContentCategory Category);

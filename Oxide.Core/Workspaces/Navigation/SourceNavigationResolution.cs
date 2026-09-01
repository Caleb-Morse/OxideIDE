using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceNavigationResolution(
    SourceNavigationStatus Status,
    SourceNavigationTarget Target,
    string Message,
    SourceDocument? Document = null,
    SourceViewerLocation? Location = null)
{
    public bool IsResolved => Status is SourceNavigationStatus.Resolved;
}

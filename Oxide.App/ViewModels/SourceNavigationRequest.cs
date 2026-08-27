using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Syntax.Text;

namespace Oxide.App.ViewModels;

public sealed record SourceNavigationRequest(
    long SnapshotVersion,
    DocumentId DocumentId,
    string PhysicalPath,
    string VirtualPath,
    string LayerId,
    string LayerName,
    int SpanStart,
    int SpanLength,
    string SemanticIdentity,
    string Location,
    string Reason)
{
    public SourceNavigationTarget Target => new(
        SnapshotVersion,
        DocumentId,
        new ContentLayerId(LayerId),
        new VirtualPath(VirtualPath),
        new TextSpan(SpanStart, SpanLength),
        SemanticIdentity,
        Reason);
}

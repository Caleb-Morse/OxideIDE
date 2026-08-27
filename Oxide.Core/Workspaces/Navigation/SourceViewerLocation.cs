using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerLocation(
    long SnapshotVersion,
    DocumentId DocumentId,
    string PhysicalPath,
    VirtualPath VirtualPath,
    ContentLayer Layer,
    SourceDocumentKind DocumentKind,
    DocumentLoadStatus LoadStatus,
    DocumentParticipation Participation,
    TextSpan Span,
    TextPosition Start,
    TextPosition End,
    string SemanticIdentity,
    string Reason)
{
    public int StartLine => Start.Line + 1;

    public int StartColumn => Start.Character + 1;

    public int EndLine => End.Line + 1;

    public int EndColumn => End.Character + 1;
}

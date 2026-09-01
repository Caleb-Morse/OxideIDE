using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerDocument(
    SourceViewerLocation Location,
    TextSpan FocusSpan,
    string Text,
    SourceEncoding Encoding,
    NewlineKind Newlines,
    bool HasFinalNewline,
    int LineCount,
    int FirstMaterializedLine,
    int LastMaterializedLine,
    ImmutableArray<SourceViewerLine> Lines,
    ImmutableArray<SourceHighlightSpan> Highlights,
    bool HighlightsTruncated,
    ImmutableArray<SourceViewerDiagnostic> Diagnostics,
    bool DiagnosticsTruncated)
{
    public bool LinesTruncated => Lines.Length < LineCount;

    public DocumentParticipation Participation => Location.Participation;
}

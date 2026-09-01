using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerLine(
    int Number,
    TextSpan Span,
    TextSpan FullSpan,
    string Text,
    TextSpan? Selection);

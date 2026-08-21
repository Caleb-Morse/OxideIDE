using Oxide.Syntax.Text;

namespace Oxide.Syntax.Lexing;

public sealed record SyntaxToken(
    SyntaxKind Kind,
    TextSpan Span,
    string Text,
    bool IsMissing = false);

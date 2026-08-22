using System.Collections.Immutable;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Parsing;

public sealed record DocumentSyntax(
    ImmutableArray<ClausewitzElementSyntax> Elements,
    TextSpan Span) : SyntaxNode(Span);

public abstract record ClausewitzElementSyntax(TextSpan Span) : SyntaxNode(Span);

public sealed record PropertySyntax(
    SyntaxToken Key,
    SyntaxToken OperatorToken,
    ClausewitzValueSyntax Value,
    TextSpan Span) : ClausewitzElementSyntax(Span)
{
    public SyntaxToken EqualsToken => OperatorToken;
}

public sealed record BareValueSyntax(
    ClausewitzValueSyntax Value,
    TextSpan Span) : ClausewitzElementSyntax(Span);

public sealed record UnexpectedTokenSyntax(
    SyntaxToken Token,
    TextSpan Span) : ClausewitzElementSyntax(Span);

public abstract record ClausewitzValueSyntax(TextSpan Span) : SyntaxNode(Span);

public sealed record ScalarValueSyntax(
    SyntaxToken Token,
    TextSpan Span) : ClausewitzValueSyntax(Span);

public sealed record BlockValueSyntax(
    SyntaxToken OpenBraceToken,
    ImmutableArray<ClausewitzElementSyntax> Elements,
    SyntaxToken CloseBraceToken,
    TextSpan Span) : ClausewitzValueSyntax(Span);

public sealed record MissingValueSyntax(TextSpan Span) : ClausewitzValueSyntax(Span);

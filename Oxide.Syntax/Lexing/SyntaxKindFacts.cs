namespace Oxide.Syntax.Lexing;

public static class SyntaxKindFacts
{
    public static bool IsTrivia(this SyntaxKind kind) => kind is
        SyntaxKind.WhitespaceToken or
        SyntaxKind.NewlineToken or
        SyntaxKind.CommentToken;

    public static bool CanBeScalar(this SyntaxKind kind) => kind is
        SyntaxKind.IdentifierToken or
        SyntaxKind.QuotedStringToken or
        SyntaxKind.NumberToken or
        SyntaxKind.DateToken or
        SyntaxKind.BadToken;
}

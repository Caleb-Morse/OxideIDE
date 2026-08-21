namespace Oxide.Syntax.Lexing;

public enum SyntaxKind
{
    BadToken,
    EndOfFileToken,
    WhitespaceToken,
    NewlineToken,
    CommentToken,
    IdentifierToken,
    QuotedStringToken,
    NumberToken,
    DateToken,
    OpenBraceToken,
    CloseBraceToken,
    EqualsToken,
}

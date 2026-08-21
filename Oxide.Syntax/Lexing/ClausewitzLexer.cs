using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Lexing;

public sealed class ClausewitzLexer
{
    private readonly SourceText source;
    private readonly ImmutableArray<SyntaxToken>.Builder tokens = ImmutableArray.CreateBuilder<SyntaxToken>();
    private readonly ImmutableArray<SyntaxDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<SyntaxDiagnostic>();
    private int position;

    private ClausewitzLexer(SourceText source)
    {
        this.source = source;
    }

    public static LexerResult Lex(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ClausewitzLexer(source).LexCore();
    }

    private LexerResult LexCore()
    {
        while (position < source.Length)
        {
            LexToken();
        }

        tokens.Add(new SyntaxToken(
            SyntaxKind.EndOfFileToken,
            new TextSpan(source.Length, 0),
            string.Empty));

        return new LexerResult(tokens.ToImmutable(), diagnostics.ToImmutable());
    }

    private void LexToken()
    {
        var character = source[position];
        switch (character)
        {
            case ' ':
            case '\t':
            case '\f':
                LexWhitespace();
                return;
            case '\r':
            case '\n':
                LexNewline();
                return;
            case '#':
                LexComment();
                return;
            case '"':
                LexQuotedString();
                return;
            case '{':
                AddSingleCharacterToken(SyntaxKind.OpenBraceToken);
                return;
            case '}':
                AddSingleCharacterToken(SyntaxKind.CloseBraceToken);
                return;
            case '=':
                AddSingleCharacterToken(SyntaxKind.EqualsToken);
                return;
            default:
                if (char.IsControl(character))
                {
                    LexBadCharacter();
                }
                else
                {
                    LexAtom();
                }

                return;
        }
    }

    private void LexWhitespace()
    {
        var start = position;
        while (position < source.Length && source[position] is ' ' or '\t' or '\f')
        {
            position++;
        }

        AddToken(SyntaxKind.WhitespaceToken, start, position);
    }

    private void LexNewline()
    {
        var start = position++;
        if (source[start] == '\r' && position < source.Length && source[position] == '\n')
        {
            position++;
        }

        AddToken(SyntaxKind.NewlineToken, start, position);
    }

    private void LexComment()
    {
        var start = position++;
        while (position < source.Length && source[position] is not '\r' and not '\n')
        {
            position++;
        }

        AddToken(SyntaxKind.CommentToken, start, position);
    }

    private void LexQuotedString()
    {
        var start = position++;
        var terminated = false;

        while (position < source.Length)
        {
            var character = source[position++];
            if (character == '\\'
                && position < source.Length
                && source[position] is not '\r' and not '\n')
            {
                position++;
                continue;
            }

            if (character == '"')
            {
                terminated = true;
                break;
            }

            if (character is '\r' or '\n')
            {
                position--;
                break;
            }
        }

        AddToken(SyntaxKind.QuotedStringToken, start, position);
        if (!terminated)
        {
            diagnostics.Add(new SyntaxDiagnostic(
                "OXIDE1001",
                DiagnosticSeverity.Error,
                "Unterminated quoted string.",
                TextSpan.FromBounds(start, position)));
        }
    }

    private void LexAtom()
    {
        var start = position;
        while (position < source.Length && !IsAtomBoundary(source[position]))
        {
            position++;
        }

        var text = source.Text.AsSpan(start, position - start);
        var kind = IsDate(text)
            ? SyntaxKind.DateToken
            : IsNumber(text)
                ? SyntaxKind.NumberToken
                : SyntaxKind.IdentifierToken;

        AddToken(kind, start, position);
    }

    private void LexBadCharacter()
    {
        var start = position++;
        AddToken(SyntaxKind.BadToken, start, position);
        diagnostics.Add(new SyntaxDiagnostic(
            "OXIDE1002",
            DiagnosticSeverity.Error,
            $"Unexpected control character U+{(int)source[start]:X4}.",
            new TextSpan(start, 1)));
    }

    private void AddSingleCharacterToken(SyntaxKind kind)
    {
        var start = position++;
        AddToken(kind, start, position);
    }

    private void AddToken(SyntaxKind kind, int start, int end)
    {
        var span = TextSpan.FromBounds(start, end);
        tokens.Add(new SyntaxToken(kind, span, source.GetText(span)));
    }

    private static bool IsAtomBoundary(char character) =>
        char.IsWhiteSpace(character) || char.IsControl(character) || character is '#' or '"' or '{' or '}' or '=';

    private static bool IsDate(ReadOnlySpan<char> text)
    {
        var firstDot = text.IndexOf('.');
        if (firstDot <= 0)
        {
            return false;
        }

        var remainder = text[(firstDot + 1)..];
        var secondDot = remainder.IndexOf('.');
        if (secondDot <= 0 || remainder[(secondDot + 1)..].Contains('.'))
        {
            return false;
        }

        return IsUnsignedInteger(text[..firstDot])
            && IsUnsignedInteger(remainder[..secondDot])
            && IsUnsignedInteger(remainder[(secondDot + 1)..]);
    }

    private static bool IsNumber(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        if (text[0] is '+' or '-')
        {
            text = text[1..];
        }

        if (text.IsEmpty)
        {
            return false;
        }

        var dotSeen = false;
        var digitSeen = false;
        foreach (var character in text)
        {
            if (character == '.' && !dotSeen)
            {
                dotSeen = true;
                continue;
            }

            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            digitSeen = true;
        }

        return digitSeen;
    }

    private static bool IsUnsignedInteger(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

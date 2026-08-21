using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Parsing;

public sealed class ClausewitzParser
{
    private readonly SourceText source;
    private readonly ImmutableArray<SyntaxToken> tokens;
    private readonly ImmutableArray<SyntaxDiagnostic>.Builder diagnostics;
    private int position;

    private ClausewitzParser(SourceText source, LexerResult lexerResult)
    {
        this.source = source;
        tokens = lexerResult.Tokens;
        diagnostics = lexerResult.Diagnostics.ToBuilder();
    }

    public static SyntaxTree Parse(string text) => Parse(SourceText.From(text));

    public static SyntaxTree Parse(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lexerResult = ClausewitzLexer.Lex(source);
        return new ClausewitzParser(source, lexerResult).ParseCore();
    }

    private SyntaxTree ParseCore()
    {
        var elements = ParseElements(stopAtCloseBrace: false);
        var root = new DocumentSyntax(elements, new TextSpan(0, source.Length));
        return new SyntaxTree(source, tokens, root, diagnostics.ToImmutable());
    }

    private ImmutableArray<ClausewitzElementSyntax> ParseElements(bool stopAtCloseBrace)
    {
        var elements = ImmutableArray.CreateBuilder<ClausewitzElementSyntax>();
        while (true)
        {
            SkipTrivia();
            var current = Current;

            if (current.Kind is SyntaxKind.EndOfFileToken)
            {
                break;
            }

            if (current.Kind is SyntaxKind.CloseBraceToken)
            {
                if (stopAtCloseBrace)
                {
                    break;
                }

                elements.Add(ParseUnexpectedToken("Unexpected closing brace."));
                continue;
            }

            elements.Add(ParseElement());
        }

        return elements.ToImmutable();
    }

    private ClausewitzElementSyntax ParseElement()
    {
        if (Current.Kind.CanBeScalar() && PeekSignificant(1).Kind is SyntaxKind.EqualsToken)
        {
            return ParseProperty();
        }

        if (Current.Kind.CanBeScalar() || Current.Kind is SyntaxKind.OpenBraceToken)
        {
            var value = ParseValue();
            return new BareValueSyntax(value, value.Span);
        }

        return ParseUnexpectedToken($"Unexpected token '{Current.Text}'.");
    }

    private PropertySyntax ParseProperty()
    {
        var key = ConsumeCurrent();
        SkipTrivia();
        var equalsToken = Match(SyntaxKind.EqualsToken);
        SkipTrivia();

        ClausewitzValueSyntax value;
        if (Current.Kind is SyntaxKind.EndOfFileToken or SyntaxKind.CloseBraceToken)
        {
            var span = new TextSpan(Current.Span.Start, 0);
            diagnostics.Add(new SyntaxDiagnostic(
                "OXIDE2001",
                DiagnosticSeverity.Error,
                $"Property '{key.Text}' requires a value.",
                span));
            value = new MissingValueSyntax(span);
        }
        else
        {
            value = ParseValue();
        }

        return new PropertySyntax(
            key,
            equalsToken,
            value,
            TextSpan.FromBounds(key.Span.Start, value.Span.End));
    }

    private ClausewitzValueSyntax ParseValue()
    {
        if (Current.Kind is SyntaxKind.OpenBraceToken)
        {
            return ParseBlock();
        }

        if (Current.Kind.CanBeScalar())
        {
            var token = ConsumeCurrent();
            return new ScalarValueSyntax(token, token.Span);
        }

        var missingSpan = new TextSpan(Current.Span.Start, 0);
        diagnostics.Add(new SyntaxDiagnostic(
            "OXIDE2002",
            DiagnosticSeverity.Error,
            "Expected a scalar value or block.",
            missingSpan));
        return new MissingValueSyntax(missingSpan);
    }

    private BlockValueSyntax ParseBlock()
    {
        var openBrace = Match(SyntaxKind.OpenBraceToken);
        var elements = ParseElements(stopAtCloseBrace: true);
        SkipTrivia();

        SyntaxToken closeBrace;
        if (Current.Kind is SyntaxKind.CloseBraceToken)
        {
            closeBrace = ConsumeCurrent();
        }
        else
        {
            closeBrace = new SyntaxToken(
                SyntaxKind.CloseBraceToken,
                new TextSpan(Current.Span.Start, 0),
                string.Empty,
                IsMissing: true);
            diagnostics.Add(new SyntaxDiagnostic(
                "OXIDE2003",
                DiagnosticSeverity.Error,
                "Block is missing a closing brace.",
                closeBrace.Span));
        }

        return new BlockValueSyntax(
            openBrace,
            elements,
            closeBrace,
            TextSpan.FromBounds(openBrace.Span.Start, closeBrace.Span.End));
    }

    private UnexpectedTokenSyntax ParseUnexpectedToken(string message)
    {
        var token = ConsumeCurrent();
        diagnostics.Add(new SyntaxDiagnostic(
            "OXIDE2004",
            DiagnosticSeverity.Error,
            message,
            token.Span));
        return new UnexpectedTokenSyntax(token, token.Span);
    }

    private SyntaxToken Match(SyntaxKind kind)
    {
        if (Current.Kind == kind)
        {
            return ConsumeCurrent();
        }

        var token = new SyntaxToken(kind, new TextSpan(Current.Span.Start, 0), string.Empty, IsMissing: true);
        diagnostics.Add(new SyntaxDiagnostic(
            "OXIDE2005",
            DiagnosticSeverity.Error,
            $"Expected {kind}.",
            token.Span));
        return token;
    }

    private void SkipTrivia()
    {
        while (Current.Kind.IsTrivia())
        {
            position++;
        }
    }

    private SyntaxToken PeekSignificant(int offset)
    {
        var index = position;
        var significantOffset = 0;
        while (index < tokens.Length)
        {
            var token = tokens[index];
            if (!token.Kind.IsTrivia())
            {
                if (significantOffset == offset)
                {
                    return token;
                }

                significantOffset++;
            }

            index++;
        }

        return tokens[^1];
    }

    private SyntaxToken Current => tokens[Math.Min(position, tokens.Length - 1)];

    private SyntaxToken ConsumeCurrent()
    {
        var current = Current;
        if (current.Kind is not SyntaxKind.EndOfFileToken)
        {
            position++;
        }

        return current;
    }
}

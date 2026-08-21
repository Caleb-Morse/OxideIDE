using Oxide.Syntax.Lexing;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Syntax;

public sealed class ClausewitzLexerTests
{
    [Fact]
    public void Lexer_retains_all_trivia_and_punctuation()
    {
        const string text = "owner = GER # retained\r\nprovinces={ 1 2 }";

        var result = ClausewitzLexer.Lex(SourceText.From(text));

        Assert.Equal(text, string.Concat(result.Tokens.Select(token => token.Text)));
        Assert.Contains(result.Tokens, token => token.Kind is SyntaxKind.CommentToken);
        Assert.Contains(result.Tokens, token => token.Kind is SyntaxKind.NewlineToken && token.Text == "\r\n");
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("1936.1.1", SyntaxKind.DateToken)]
    [InlineData("-12.50", SyntaxKind.NumberToken)]
    [InlineData("+7", SyntaxKind.NumberToken)]
    [InlineData("political.4", SyntaxKind.IdentifierToken)]
    [InlineData("gfx/interface/icon.dds", SyntaxKind.IdentifierToken)]
    public void Lexer_classifies_complete_atoms(string text, SyntaxKind expectedKind)
    {
        var result = ClausewitzLexer.Lex(SourceText.From(text));

        Assert.Equal(expectedKind, result.Tokens[0].Kind);
        Assert.Equal(text, result.Tokens[0].Text);
    }

    [Fact]
    public void Lexer_keeps_escaped_quotes_inside_quoted_strings()
    {
        const string text = "\"A \\\"quoted\\\" value\"";

        var result = ClausewitzLexer.Lex(SourceText.From(text));

        Assert.Equal(SyntaxKind.QuotedStringToken, result.Tokens[0].Kind);
        Assert.Equal(text, result.Tokens[0].Text);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Lexer_recovers_after_unterminated_string_at_newline()
    {
        var result = ClausewitzLexer.Lex(SourceText.From("\"broken\nnext"));

        Assert.Equal("OXIDE1001", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(SyntaxKind.NewlineToken, result.Tokens[1].Kind);
        Assert.Equal(SyntaxKind.IdentifierToken, result.Tokens[2].Kind);
    }

    [Fact]
    public void Lexer_reports_unexpected_control_characters_without_losing_them()
    {
        const string text = "a\0b";

        var result = ClausewitzLexer.Lex(SourceText.From(text));

        Assert.Contains(result.Tokens, token => token.Kind is SyntaxKind.BadToken);
        Assert.Equal(text, string.Concat(result.Tokens.Select(token => token.Text)));
        Assert.Equal("OXIDE1002", Assert.Single(result.Diagnostics).Code);
    }
}

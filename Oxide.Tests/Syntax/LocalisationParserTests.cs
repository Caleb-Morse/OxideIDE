using System.Text;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Syntax;

public sealed class LocalisationParserTests
{
    [Fact]
    public void Parses_language_entries_versions_escapes_comments_and_trivia()
    {
        const string text = "# retained\r\nl_english: # language\r\n STATE_1:0 \"Cor\\\"sica\" # name\r\n EMPTY: \"\"\r\n\r\n";

        var tree = LocalisationParser.Parse(text);

        Assert.Equal(text, tree.ToFullString());
        var header = Assert.Single(tree.LanguageHeaders);
        Assert.Equal("english", header.Language);
        Assert.Equal("english", tree.Source.GetText(header.LanguageSpan));
        Assert.Equal(2, tree.Entries.Length);
        Assert.Equal("STATE_1", tree.Entries[0].Key);
        Assert.Equal(0, tree.Entries[0].Version);
        Assert.Equal("Cor\"sica", tree.Entries[0].Value);
        Assert.Equal("\"Cor\\\"sica\"", tree.Source.GetText(tree.Entries[0].QuotedValueSpan));
        Assert.Equal("", tree.Entries[1].Value);
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Original_utf8_bom_and_bytes_are_preserved()
    {
        var content = Encoding.UTF8.GetBytes("l_english:\n STATE_1: \"Corsica\"\n");
        var bytes = new byte[content.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        content.CopyTo(bytes, 3);

        var tree = LocalisationParser.Parse(SourceText.FromBytes(bytes));

        Assert.Equal(SourceEncoding.Utf8WithBom, tree.Source.Encoding);
        Assert.Equal(bytes, tree.GetOriginalBytes().ToArray());
    }

    [Fact]
    public void Embedded_unescaped_quotes_are_retained_until_the_line_closing_quote()
    {
        const string text = "l_english:\n DESCRIPTION: \"A so-called \"minor incident\" became a crisis.\"\n";

        var tree = LocalisationParser.Parse(text);

        var entry = Assert.Single(tree.Entries);
        Assert.Equal("A so-called \"minor incident\" became a crisis.", entry.Value);
        Assert.Equal(text, tree.ToFullString());
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("spanish", "STATE_1", "Costa del Sol")]
    [InlineData("russian", "STATE_1", "Северный берег")]
    [InlineData("simp_chinese", "STATE_1", "北方海岸")]
    public void Parses_multilingual_utf8_values(string language, string key, string value)
    {
        var text = $"l_{language}:\n {key}: \"{value}\"\n";

        var tree = LocalisationParser.Parse(text);

        Assert.Equal(language, Assert.Single(tree.LanguageHeaders).Language);
        Assert.Equal(value, Assert.Single(tree.Entries).Value);
        Assert.Equal(text, tree.ToFullString());
        Assert.Empty(tree.Diagnostics);
    }

    [Theory]
    [InlineData("STATE_1: \"Before header\"\nl_english:\n", "OXIDE1202")]
    [InlineData("l_english\n STATE_1: \"Corsica\"\n", "OXIDE1201")]
    [InlineData("l_english:\n STATE_1: \"unterminated\n", "OXIDE1203")]
    [InlineData("# comments only\n", "OXIDE1204")]
    [InlineData("l_english: trailing\n", "OXIDE1201")]
    public void Malformed_input_is_lossless_and_diagnostic(string text, string code)
    {
        var tree = LocalisationParser.Parse(text);

        Assert.Equal(text, tree.ToFullString());
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.All(tree.Diagnostics, diagnostic =>
        {
            Assert.InRange(diagnostic.Span.Start, 0, text.Length);
            Assert.InRange(diagnostic.Span.End, diagnostic.Span.Start, text.Length);
        });
    }
}

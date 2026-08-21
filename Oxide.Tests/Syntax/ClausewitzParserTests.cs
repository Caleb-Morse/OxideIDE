using Oxide.Syntax.Lexing;
using Oxide.Syntax.Parsing;

namespace Oxide.Tests.Syntax;

public sealed class ClausewitzParserTests
{
    [Fact]
    public void Parser_builds_nested_properties_blocks_and_bare_values()
    {
        const string text = "state={ id=42 history={ owner=GER add_core_of = GER } provinces={ 1 2 3 } }";

        var tree = ClausewitzParser.Parse(text);

        var state = Assert.IsType<PropertySyntax>(Assert.Single(tree.Root.Elements));
        Assert.Equal("state", state.Key.Text);
        var stateBlock = Assert.IsType<BlockValueSyntax>(state.Value);
        Assert.Collection(
            stateBlock.Elements,
            element => Assert.Equal("id", Assert.IsType<PropertySyntax>(element).Key.Text),
            element => Assert.Equal("history", Assert.IsType<PropertySyntax>(element).Key.Text),
            element => Assert.Equal("provinces", Assert.IsType<PropertySyntax>(element).Key.Text));

        var provinces = Assert.IsType<BlockValueSyntax>(
            Assert.IsType<PropertySyntax>(stateBlock.Elements[2]).Value);
        Assert.All(provinces.Elements, element => Assert.IsType<BareValueSyntax>(element));
        Assert.Empty(tree.Diagnostics);
    }

    [Fact]
    public void Parser_preserves_duplicate_properties_and_original_order()
    {
        var tree = ClausewitzParser.Parse("a=1 a=2 a=3");

        Assert.Equal(["1", "2", "3"], tree.Root.Elements
            .Cast<PropertySyntax>()
            .Select(property => Assert.IsType<ScalarValueSyntax>(property.Value).Token.Text));
    }

    [Fact]
    public void Parser_reports_missing_property_value_and_continues_at_close_brace()
    {
        var tree = ClausewitzParser.Parse("outer={ owner= }");

        var outer = Assert.IsType<PropertySyntax>(Assert.Single(tree.Root.Elements));
        var block = Assert.IsType<BlockValueSyntax>(outer.Value);
        var owner = Assert.IsType<PropertySyntax>(Assert.Single(block.Elements));
        Assert.IsType<MissingValueSyntax>(owner.Value);
        Assert.False(block.CloseBraceToken.IsMissing);
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == "OXIDE2001");
    }

    [Fact]
    public void Parser_inserts_missing_close_brace_without_changing_full_text()
    {
        const string text = "outer = { inner = yes";

        var tree = ClausewitzParser.Parse(text);

        var outer = Assert.IsType<PropertySyntax>(Assert.Single(tree.Root.Elements));
        var block = Assert.IsType<BlockValueSyntax>(outer.Value);
        Assert.True(block.CloseBraceToken.IsMissing);
        Assert.Equal(text, tree.ToFullString());
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == "OXIDE2003");
    }

    [Fact]
    public void Parser_reports_stray_tokens_and_keeps_parsing_following_properties()
    {
        var tree = ClausewitzParser.Parse("} = valid=yes");

        Assert.IsType<UnexpectedTokenSyntax>(tree.Root.Elements[0]);
        Assert.IsType<UnexpectedTokenSyntax>(tree.Root.Elements[1]);
        Assert.Equal("valid", Assert.IsType<PropertySyntax>(tree.Root.Elements[2]).Key.Text);
        Assert.Equal(2, tree.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE2004"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# only a comment\n")]
    [InlineData("a = { b = \"text\" # comment\r\n c = 1936.1.1 }")]
    [InlineData("broken = { x = \"unterminated\n y = 2")]
    public void Parse_and_emit_is_character_exact(string text)
    {
        var tree = ClausewitzParser.Parse(text);

        Assert.Equal(text, tree.ToFullString());
    }

    [Fact]
    public void Node_spans_point_to_exact_source_ranges()
    {
        const string text = "  owner = GER  ";

        var tree = ClausewitzParser.Parse(text);
        var property = Assert.IsType<PropertySyntax>(Assert.Single(tree.Root.Elements));

        Assert.Equal("owner = GER", tree.Source.GetText(property.Span));
        Assert.Equal("owner", tree.Source.GetText(property.Key.Span));
        Assert.Equal("GER", tree.Source.GetText(property.Value.Span));
    }
}

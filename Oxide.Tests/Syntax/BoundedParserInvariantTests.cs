using Oxide.Syntax.Parsing;

namespace Oxide.Tests.Syntax;

public sealed class BoundedParserInvariantTests
{
    private const int SampleCount = 256;
    private const int MaximumDepth = 4;
    private const int MaximumLength = 2_048;

    [Fact]
    public void Bounded_generated_inputs_are_lossless_and_have_valid_spans()
    {
        var random = new Random(0x0_51DE);

        for (var sample = 0; sample < SampleCount; sample++)
        {
            var text = GenerateDocument(random);
            Assert.InRange(text.Length, 0, MaximumLength);

            var tree = ClausewitzParser.Parse(text);

            Assert.Equal(text, tree.ToFullString());
            Assert.Equal(text.Length, tree.Root.Span.Length);
            Assert.All(tree.Tokens, token => AssertBounded(token.Span.Start, token.Span.End, text.Length));
            Assert.All(tree.Diagnostics, diagnostic => AssertBounded(
                diagnostic.Span.Start,
                diagnostic.Span.End,
                text.Length));
        }
    }

    [Theory]
    [InlineData("=")]
    [InlineData("{{{{")]
    [InlineData("}}}}")]
    [InlineData("key = = value")]
    [InlineData("key = { nested = \"unterminated\n next = yes")]
    [InlineData("left\u00A0=\u2003right")]
    [InlineData("a != { b <= 2 c ?= yes }")]
    public void Curated_malformed_inputs_are_lossless(string text)
    {
        var tree = ClausewitzParser.Parse(text);

        Assert.Equal(text, tree.ToFullString());
        Assert.All(tree.Diagnostics, diagnostic => AssertBounded(
            diagnostic.Span.Start,
            diagnostic.Span.End,
            text.Length));
    }

    private static string GenerateDocument(Random random)
    {
        var elements = Enumerable.Range(0, random.Next(1, 9))
            .Select(_ => GenerateElement(random, 0));
        var text = string.Join(RandomTrivia(random), elements);
        return text.Length <= MaximumLength ? text : text[..MaximumLength];
    }

    private static string GenerateElement(Random random, int depth)
    {
        if (depth < MaximumDepth && random.Next(4) == 0)
        {
            var children = Enumerable.Range(0, random.Next(0, 6))
                .Select(_ => GenerateElement(random, depth + 1));
            var closingBrace = random.Next(8) == 0 ? string.Empty : "}";
            return $"{RandomKey(random)} {RandomOperator(random)} {{ {string.Join(RandomTrivia(random), children)} {closingBrace}";
        }

        return random.Next(6) switch
        {
            0 => RandomScalar(random),
            1 => "}",
            2 => $"{RandomKey(random)} =",
            _ => $"{RandomKey(random)} {RandomOperator(random)} {RandomScalar(random)}",
        };
    }

    private static string RandomKey(Random random) =>
        new[] { "state", "id", "owner", "value", "has_flag", "political.4" }[random.Next(6)];

    private static string RandomOperator(Random random) =>
        new[] { "=", "==", "!=", "<", "<=", ">", ">=", "?=" }[random.Next(8)];

    private static string RandomScalar(Random random) =>
        new[] { "yes", "no", "GER", "42", "-12.5", "1936.1.1", "\"quoted value\"" }[random.Next(7)];

    private static string RandomTrivia(Random random) =>
        new[] { " ", "\n", "\r\n", " # retained\n", "\t", "\u00A0" }[random.Next(6)];

    private static void AssertBounded(int start, int end, int sourceLength)
    {
        Assert.InRange(start, 0, sourceLength);
        Assert.InRange(end, start, sourceLength);
    }
}

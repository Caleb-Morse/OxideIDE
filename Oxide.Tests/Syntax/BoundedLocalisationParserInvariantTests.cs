using Oxide.Syntax.Localisation;

namespace Oxide.Tests.Syntax;

public sealed class BoundedLocalisationParserInvariantTests
{
    private const int SampleCount = 256;
    private const int MaximumLength = 2_048;

    [Fact]
    public void Bounded_generated_inputs_are_lossless_and_have_valid_spans()
    {
        var random = new Random(0x06_1001);

        for (var sample = 0; sample < SampleCount; sample++)
        {
            var text = GenerateDocument(random);
            Assert.InRange(text.Length, 0, MaximumLength);

            var tree = LocalisationParser.Parse(text);

            Assert.Equal(text, tree.ToFullString());
            Assert.All(tree.Lines, line => AssertBounded(line.FullSpan.Start, line.FullSpan.End, text.Length));
            Assert.All(tree.Diagnostics, diagnostic =>
                AssertBounded(diagnostic.Span.Start, diagnostic.Span.End, text.Length));
            Assert.All(tree.Entries, entry =>
            {
                AssertBounded(entry.KeySpan.Start, entry.KeySpan.End, text.Length);
                AssertBounded(entry.QuotedValueSpan.Start, entry.QuotedValueSpan.End, text.Length);
                AssertBounded(entry.ValueSpan.Start, entry.ValueSpan.End, text.Length);
            });
        }
    }

    private static string GenerateDocument(Random random)
    {
        var lines = Enumerable.Range(0, random.Next(0, 25))
            .Select(_ => random.Next(8) switch
            {
                0 => "l_english:",
                1 => " l_french: # language",
                2 => $" KEY_{random.Next(20)}:{random.Next(3)} \"Value {random.Next()}\"",
                3 => $" KEY_{random.Next(20)}: \"escaped \\\" value\" # retained",
                4 => " # comment",
                5 => " BROKEN: \"unterminated",
                6 => "l_bad-language:",
                _ => RandomCharacters(random),
            });
        var text = string.Join(random.Next(2) == 0 ? "\n" : "\r\n", lines);
        return text.Length <= MaximumLength ? text : text[..MaximumLength];
    }

    private static string RandomCharacters(Random random)
    {
        const string characters = "abcXYZ_:# \\\"0123{}[]=\t$£é";
        return new string(Enumerable.Range(0, random.Next(0, 80))
            .Select(_ => characters[random.Next(characters.Length)])
            .ToArray());
    }

    private static void AssertBounded(int start, int end, int sourceLength)
    {
        Assert.InRange(start, 0, sourceLength);
        Assert.InRange(end, start, sourceLength);
    }
}

using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Syntax;

public sealed class RealCorpusSmokeTests
{
    [Fact]
    [Trait("Category", "ExternalCorpus")]
    public void Configured_hoi4_state_and_country_tag_files_round_trip_exactly()
    {
        var root = Environment.GetEnvironmentVariable("OXIDE_HOI4_CORPUS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var patterns = new[]
        {
            Path.Combine(root, "history", "states", "*.txt"),
            Path.Combine(root, "common", "country_tags", "*.txt"),
        };
        var files = patterns
            .SelectMany(pattern => Directory.EnumerateFiles(
                Path.GetDirectoryName(pattern)!,
                Path.GetFileName(pattern),
                SearchOption.TopDirectoryOnly))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var source = SourceText.Load(file);
            var tree = ClausewitzParser.Parse(source);

            Assert.Equal(source.Text, tree.ToFullString());
            Assert.Equal(source.GetOriginalBytes().ToArray(), tree.GetOriginalBytes().ToArray());
        }
    }
}

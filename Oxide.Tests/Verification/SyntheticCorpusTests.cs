using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Verification;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Tests.Verification;

public sealed class SyntheticCorpusTests
{
    [Fact]
    [Trait("Category", "SyntheticCorpus")]
    public async Task Repository_corpus_produces_the_expected_deterministic_summary()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Corpus");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(
            Path.Combine(root, "game"),
            Path.Combine(root, "mod"),
            "Synthetic corpus"));

        var summary = CorpusSummaryBuilder.Build(snapshot, TimeSpan.FromMilliseconds(123));

        Assert.Equal(8, summary.FilesDiscovered);
        Assert.Equal(8, summary.DocumentsLoaded);
        Assert.Equal(0, summary.DocumentsFailed);
        Assert.Equal(1, summary.SyntaxDiagnosticCount);
        Assert.Equal(1, summary.SyntaxDiagnosticsByCode["OXIDE2003"]);
        Assert.Equal(6, summary.StateDeclarationCount);
        Assert.Equal(4, summary.StateEntityCount);
        Assert.Equal(3, summary.CountryDeclarationCount);
        Assert.Equal(2, summary.CountryEntityCount);
        Assert.Equal(7, summary.SemanticDiagnosticCount);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4002"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4003"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4004"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4006"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4007"]);
        Assert.Equal(2, summary.CountryReferences.Total);
        Assert.Equal(0, summary.CountryReferences.Resolved);
        Assert.Equal(1, summary.CountryReferences.Missing);
        Assert.Equal(1, summary.CountryReferences.Ambiguous);
        Assert.Equal(0, summary.CountryReferences.Invalid);
        Assert.Equal(2, summary.CountryReferences.Unresolved);
        Assert.Equal(8, summary.WorkspacePerformance.DocumentCount);
        Assert.Equal(8, summary.WorkspacePerformance.LoadedDocumentCount);
        Assert.Equal(0, summary.WorkspacePerformance.FailedDocumentCount);
        Assert.True(summary.WorkspacePerformance.TotalMilliseconds >= 0);
        Assert.Equal(123, summary.TotalLoadMilliseconds);
        Assert.IsType<MissingCountry>(snapshot.Semantics.States[2].Owner!.Resolution);
        Assert.IsType<AmbiguousCountry>(snapshot.Semantics.States[5].Owner!.Resolution);
    }
}

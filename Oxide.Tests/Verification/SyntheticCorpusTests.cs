using System.Text.Json;
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

        var summary = CorpusSummaryBuilder.Build(
            snapshot,
            TimeSpan.FromMilliseconds(123),
            new CorpusSummaryOptions("spanish", EnglishFallbackEnabled: true));

        Assert.Equal(14, summary.FilesDiscovered);
        Assert.Equal(14, summary.DocumentsLoaded);
        Assert.Equal(0, summary.DocumentsFailed);
        Assert.Equal(2, summary.SyntaxDiagnosticCount);
        Assert.Equal(1, summary.SyntaxDiagnosticsByCode["OXIDE1204"]);
        Assert.Equal(1, summary.SyntaxDiagnosticsByCode["OXIDE2003"]);
        Assert.Equal(6, summary.StateDeclarationCount);
        Assert.Equal(4, summary.StateEntityCount);
        Assert.Equal(3, summary.CountryDeclarationCount);
        Assert.Equal(2, summary.CountryEntityCount);
        Assert.Equal(9, summary.SemanticDiagnosticCount);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4002"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4003"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4004"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4006"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4007"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4009"]);
        Assert.Equal(2, summary.CountryReferences.Total);
        Assert.Equal(0, summary.CountryReferences.Resolved);
        Assert.Equal(1, summary.CountryReferences.Missing);
        Assert.Equal(1, summary.CountryReferences.Ambiguous);
        Assert.Equal(0, summary.CountryReferences.Invalid);
        Assert.Equal(2, summary.CountryReferences.Unresolved);
        Assert.Equal(14, summary.WorkspacePerformance.DocumentCount);
        Assert.Equal(14, summary.WorkspacePerformance.LoadedDocumentCount);
        Assert.Equal(0, summary.WorkspacePerformance.FailedDocumentCount);
        Assert.True(summary.WorkspacePerformance.TotalMilliseconds >= 0);
        Assert.Equal(123, summary.TotalLoadMilliseconds);
        Assert.IsType<MissingCountry>(snapshot.Semantics.States[2].Owner!.Resolution);
        Assert.IsType<AmbiguousCountry>(snapshot.Semantics.States[5].Owner!.Resolution);

        var localisation = summary.Localisation;
        Assert.Equal(6, localisation.FilesDiscovered);
        Assert.Equal(6, localisation.DocumentsLoaded);
        Assert.Equal(0, localisation.DocumentsFailed);
        Assert.Equal(["english", "russian", "simp_chinese", "spanish"], localisation.LanguagesDiscovered.ToArray());
        Assert.Equal(8, localisation.DeclarationsByLanguage["english"]);
        Assert.Equal(1, localisation.DeclarationsByLanguage["russian"]);
        Assert.Equal(1, localisation.DeclarationsByLanguage["simp_chinese"]);
        Assert.Equal(2, localisation.DeclarationsByLanguage["spanish"]);
        Assert.Equal(12, localisation.DeclarationCount);
        Assert.Equal(10, localisation.UniqueIdentityCount);
        Assert.Equal(2, localisation.DuplicateIdentityCount);
        Assert.Equal(2, localisation.AmbiguousEntryCount);
        Assert.Equal(12, localisation.DeclarationsWithValidProvenance);
        Assert.Equal(1, localisation.SyntaxDiagnosticCount);
        Assert.Equal(1, localisation.SyntaxDiagnosticsByCode["OXIDE1204"]);
        Assert.Equal(2, localisation.SemanticDiagnosticCount);
        Assert.Equal(2, localisation.SemanticDiagnosticsByCode["OXIDE4009"]);
        Assert.Equal("spanish", localisation.RequestedLanguage);
        Assert.Equal("spanish", localisation.EffectiveLanguage);
        Assert.True(localisation.EnglishFallbackEnabled);
        Assert.Equal(new LocalisationResolutionCounts(4, 1, 1, 1, 0, 0, 1), localisation.StateNames);
        Assert.Equal(new LocalisationResolutionCounts(2, 1, 0, 0, 1, 0, 0), localisation.CountryNames);
        Assert.True(localisation.NameProjectionMilliseconds >= 0);
        Assert.True(localisation.NameProjectionsPerSecond >= 0);
        Assert.True(localisation.ManagedMemoryBytesAtReport > 0);
        Assert.True(summary.WorkspacePerformance.ClausewitzDocumentLoadingMilliseconds >= 0);
        Assert.True(summary.WorkspacePerformance.LocalisationDocumentLoadingMilliseconds > 0);
        Assert.True(summary.WorkspacePerformance.LocalisationIndexingMilliseconds >= 0);
    }

    [Fact]
    [Trait("Category", "SyntheticCorpus")]
    public async Task Repository_corpus_preserves_localisation_contributions_and_provenance()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Corpus");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(
            Path.Combine(root, "game"),
            Path.Combine(root, "mod"),
            "Synthetic corpus"));

        Assert.Equal(
            snapshot.Semantics.LocalisationDeclarations.Length,
            snapshot.Semantics.Localisations.Sum(entry => entry.Value.Contributions.Length));
        Assert.All(snapshot.Semantics.Localisations.Values.Where(entry => entry.IsAmbiguous), entry =>
            Assert.True(entry.Contributions.Length > 1));
        Assert.All(snapshot.Semantics.LocalisationDeclarations, declaration =>
        {
            var document = snapshot.DocumentsById[declaration.Value.Provenance.DocumentId];
            Assert.NotNull(document.Text);
            Assert.Equal(declaration.Value.OriginalText, document.Text!.GetText(declaration.Value.Provenance.Span));
            Assert.InRange(declaration.Provenance.Span.End, declaration.Provenance.Span.Start, document.Text.Length);
        });
        var resolvedNames = snapshot.Semantics.States.Values.Cast<Oxide.Core.Semantics.Model.ISemanticEntity>()
            .Concat(snapshot.Semantics.Countries.Values)
            .Select(entity => snapshot.Semantics.LocalisationResolver.ResolveName(entity, "spanish"))
            .Select(name => name.Resolution)
            .OfType<ResolvedLocalisation>()
            .ToArray();
        Assert.NotEmpty(resolvedNames);
        Assert.All(resolvedNames, resolution =>
        {
            var document = snapshot.DocumentsById[resolution.Provenance.DocumentId];
            Assert.NotNull(document.Text);
            Assert.Equal(resolution.Declaration.Value.OriginalText,
                document.Text!.GetText(resolution.Provenance.Span));
        });

        var withoutFallback = CorpusSummaryBuilder.Build(
            snapshot,
            TimeSpan.Zero,
            new CorpusSummaryOptions("spanish", EnglishFallbackEnabled: false));
        Assert.Equal(0, withoutFallback.Localisation.StateNames.EnglishFallback);
        Assert.Equal(2, withoutFallback.Localisation.StateNames.Missing);

        var unavailableLanguage = CorpusSummaryBuilder.Build(
            snapshot,
            TimeSpan.Zero,
            new CorpusSummaryOptions("german"));
        Assert.Equal("german", unavailableLanguage.Localisation.RequestedLanguage);
        Assert.Equal("english", unavailableLanguage.Localisation.EffectiveLanguage);
    }

    [Fact]
    [Trait("Category", "SyntheticCorpus")]
    public async Task Equivalent_reports_have_deterministic_structural_values()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Corpus");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(
            Path.Combine(root, "game"),
            Path.Combine(root, "mod"),
            "Synthetic corpus"));

        var first = CorpusSummaryBuilder.Build(snapshot, TimeSpan.FromMilliseconds(10));
        var second = CorpusSummaryBuilder.Build(snapshot, TimeSpan.FromMilliseconds(10));

        var normalizedFirst = first.Localisation with
        {
            NameProjectionMilliseconds = 0,
            NameProjectionsPerSecond = 0,
            ManagedMemoryBytesAtReport = 0,
        };
        var normalizedSecond = second.Localisation with
        {
            NameProjectionMilliseconds = 0,
            NameProjectionsPerSecond = 0,
            ManagedMemoryBytesAtReport = 0,
        };
        Assert.Equal(JsonSerializer.Serialize(normalizedFirst), JsonSerializer.Serialize(normalizedSecond));
        Assert.Equal(first.SyntaxDiagnosticsByCode.ToArray(), second.SyntaxDiagnosticsByCode.ToArray());
        Assert.Equal(first.SemanticDiagnosticsByCode.ToArray(), second.SemanticDiagnosticsByCode.ToArray());
        Assert.Equal(first.CountryReferences, second.CountryReferences);
    }
}

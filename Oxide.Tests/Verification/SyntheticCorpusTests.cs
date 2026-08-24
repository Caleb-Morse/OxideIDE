using System.Text.Json;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Verification;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;

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

        Assert.Equal(24, summary.FilesDiscovered);
        Assert.Equal(24, summary.DocumentsLoaded);
        Assert.Equal(0, summary.DocumentsFailed);
        Assert.Equal(2, summary.SyntaxDiagnosticCount);
        Assert.Equal(1, summary.SyntaxDiagnosticsByCode["OXIDE1204"]);
        Assert.Equal(1, summary.SyntaxDiagnosticsByCode["OXIDE2003"]);
        Assert.Equal(10, summary.StateDeclarationCount);
        Assert.Equal(9, summary.StateEntityCount);
        Assert.Equal(3, summary.CountryDeclarationCount);
        Assert.Equal(2, summary.CountryEntityCount);
        Assert.Equal(19, summary.SemanticDiagnosticCount);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4002"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4003"]);
        Assert.Equal(2, summary.SemanticDiagnosticsByCode["OXIDE4004"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4006"]);
        Assert.DoesNotContain("OXIDE4007", summary.SemanticDiagnosticsByCode.Keys);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4009"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4011"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4014"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4015"]);
        Assert.Equal(3, summary.SemanticDiagnosticsByCode["OXIDE4016"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4017"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4018"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4019"]);
        Assert.Equal(1, summary.SemanticDiagnosticsByCode["OXIDE4020"]);
        Assert.Equal(3, summary.SemanticDiagnosticsByCode["OXIDE4021"]);
        Assert.Equal(3, summary.CountryReferences.Total);
        Assert.Equal(2, summary.CountryReferences.Resolved);
        Assert.Equal(1, summary.CountryReferences.Missing);
        Assert.Equal(0, summary.CountryReferences.Ambiguous);
        Assert.Equal(0, summary.CountryReferences.Invalid);
        Assert.Equal(1, summary.CountryReferences.Unresolved);
        Assert.Equal(24, summary.WorkspacePerformance.DocumentCount);
        Assert.Equal(24, summary.WorkspacePerformance.LoadedDocumentCount);
        Assert.Equal(0, summary.WorkspacePerformance.FailedDocumentCount);
        Assert.True(summary.WorkspacePerformance.TotalMilliseconds >= 0);
        Assert.Equal(123, summary.TotalLoadMilliseconds);
        Assert.IsType<MissingCountry>(snapshot.Semantics.States[2].Owner!.Resolution);
        var resolvedOwner = Assert.IsType<ResolvedCountry>(snapshot.Semantics.States[5].Owner!.Resolution);
        Assert.Equal("AAA", resolvedOwner.Target.Id.LocalKey);

        var localisation = summary.Localisation;
        Assert.Equal(6, localisation.FilesDiscovered);
        Assert.Equal(6, localisation.DocumentsLoaded);
        Assert.Equal(0, localisation.DocumentsFailed);
        Assert.Equal(["english", "russian", "simp_chinese", "spanish"], localisation.LanguagesDiscovered.ToArray());
        Assert.Equal(11, localisation.DeclarationsByLanguage["english"]);
        Assert.Equal(1, localisation.DeclarationsByLanguage["russian"]);
        Assert.Equal(1, localisation.DeclarationsByLanguage["simp_chinese"]);
        Assert.Equal(3, localisation.DeclarationsByLanguage["spanish"]);
        Assert.Equal(16, localisation.DeclarationCount);
        Assert.Equal(14, localisation.UniqueIdentityCount);
        Assert.Equal(2, localisation.DuplicateIdentityCount);
        Assert.Equal(1, localisation.AmbiguousEntryCount);
        Assert.Equal(16, localisation.DeclarationsWithValidProvenance);
        Assert.Equal(1, localisation.SyntaxDiagnosticCount);
        Assert.Equal(1, localisation.SyntaxDiagnosticsByCode["OXIDE1204"]);
        Assert.Equal(1, localisation.SemanticDiagnosticCount);
        Assert.Equal(1, localisation.SemanticDiagnosticsByCode["OXIDE4009"]);
        Assert.Equal("spanish", localisation.RequestedLanguage);
        Assert.Equal("spanish", localisation.EffectiveLanguage);
        Assert.True(localisation.EnglishFallbackEnabled);
        Assert.Equal(new LocalisationResolutionCounts(9, 1, 1, 7, 0, 0, 0), localisation.StateNames);
        Assert.Equal(new LocalisationResolutionCounts(2, 1, 0, 0, 1, 0, 0), localisation.CountryNames);
        Assert.Equal(new LocalisationResolutionCounts(3, 1, 1, 0, 0, 0, 1), localisation.StrategicRegionNames);
        var strategicRegions = summary.StrategicRegions;
        Assert.Equal(5, strategicRegions.FilesDiscovered);
        Assert.Equal(5, strategicRegions.DocumentsLoaded);
        Assert.Equal(0, strategicRegions.DocumentsFailed);
        Assert.Equal(5, strategicRegions.DeclarationCount);
        Assert.Equal(3, strategicRegions.EntityCount);
        Assert.Equal(2, strategicRegions.EffectiveEntityCount);
        Assert.Equal(1, strategicRegions.AmbiguousEntityCount);
        Assert.Equal(11, strategicRegions.ProvinceCandidateCount);
        Assert.Equal(1, strategicRegions.RepeatedProvinceCandidateCount);
        Assert.Equal(9, strategicRegions.IndexedProvinceCount);
        Assert.Equal(3, strategicRegions.AmbiguousProvinceCount);
        Assert.Equal(5, strategicRegions.DeclarationsWithValidProvenance);
        Assert.Equal(11, strategicRegions.ProvinceCandidatesWithValidProvenance);
        Assert.Equal(new StrategicRegionMembershipCounts(9, 2, 1, 1, 1, 1, 3),
            strategicRegions.StateMemberships);

        var alphaPath = new VirtualPath("history/states/1-Alpha.txt");
        var alphaDocuments = snapshot.DocumentsByVirtualPath[alphaPath];
        Assert.Equal(2, alphaDocuments.Length);
        Assert.Equal(DocumentParticipationKind.ShadowedByHigherLayerPath, alphaDocuments[0].Participation.Kind);
        Assert.True(alphaDocuments[1].Participates);
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
        Assert.All(snapshot.Semantics.StateStrategicRegionMemberships.Values
            .SelectMany(membership => membership.Provinces), reference =>
        {
            var stateDocument = snapshot.DocumentsById[reference.StateProvince.Provenance.DocumentId];
            Assert.NotNull(stateDocument.Text);
            Assert.Equal(reference.StateProvince.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                stateDocument.Text!.GetText(reference.StateProvince.Provenance.Span));
            var candidates = reference.Resolution switch
            {
                ResolvedProvinceStrategicRegion resolved => resolved.Candidates,
                AmbiguousProvinceStrategicRegion ambiguous => ambiguous.Candidates,
                _ => [],
            };
            Assert.All(candidates, candidate =>
            {
                var regionDocument = snapshot.DocumentsById[candidate.Provenance.DocumentId];
                Assert.NotNull(regionDocument.Text);
                Assert.Equal(candidate.ProvinceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    regionDocument.Text!.GetText(candidate.Provenance.Span));
            });
        });

        var withoutFallback = CorpusSummaryBuilder.Build(
            snapshot,
            TimeSpan.Zero,
            new CorpusSummaryOptions("spanish", EnglishFallbackEnabled: false));
        Assert.Equal(0, withoutFallback.Localisation.StateNames.EnglishFallback);
        Assert.Equal(8, withoutFallback.Localisation.StateNames.Missing);
        Assert.Equal(0, withoutFallback.Localisation.StrategicRegionNames.EnglishFallback);
        Assert.Equal(1, withoutFallback.Localisation.StrategicRegionNames.Missing);

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

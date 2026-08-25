using Oxide.App.ViewModels;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Application;

public sealed class ContributionPresentationTests
{
    [Fact]
    public async Task Supported_domains_share_one_effective_and_shadowed_presentation()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/1-Base.txt",
            "state={ id=1 name=SAME manpower=10 provinces={ 1 } }");
        fixture.WriteModFile(
            "history/states/1-Mod.txt",
            "state={ id=1 name=SAME state_category=city provinces={ 2 } }");
        fixture.WriteGameFile("common/country_tags/00_base.txt", "AAA=\"countries/Base.txt\"");
        fixture.WriteModFile("common/country_tags/00_mod.txt", "AAA=\"countries/Mod.txt\"");
        fixture.WriteGameFile(
            "map/strategicregions/1-Base.txt",
            "strategic_region={ id=1 name=REGION provinces={ 1 } }");
        fixture.WriteModFile(
            "map/strategicregions/1-Mod.txt",
            "strategic_region={ id=1 name=REGION provinces={ 2 } }");
        fixture.WriteGameFile(
            "localisation/english/base_l_english.yml",
            "l_english:\n TEST_KEY:0 \"Base\"\n");
        fixture.WriteModFile(
            "localisation/english/mod_l_english.yml",
            "l_english:\n TEST_KEY:0 \"Mod\"\n");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var presentations = new[]
        {
            ContributionSetPresentation.Create(snapshot.Semantics.States[1], snapshot),
            ContributionSetPresentation.Create(snapshot.Semantics.Countries["AAA"], snapshot),
            ContributionSetPresentation.Create(snapshot.Semantics.StrategicRegions[1], snapshot),
            ContributionSetPresentation.Create(
                snapshot.Semantics.Localisations[
                    new LocalisationIdentity(new LocalisationLanguage("english"), new LocalisationKey("TEST_KEY"))],
                snapshot),
        };

        Assert.All(presentations, presentation =>
        {
            Assert.Equal(ContributionOutcomePresentation.Effective, presentation.Outcome);
            Assert.Equal("Effective contribution selected", presentation.OutcomeLabel);
            Assert.Contains("highest precedence", presentation.ResolutionReason, StringComparison.OrdinalIgnoreCase);
            Assert.True(presentation.HasCompetingContributions);
            Assert.Equal(2, presentation.ContributionCount);
            Assert.Equal(1, presentation.EffectiveSource!.LayerPosition);
            Assert.Contains(presentation.Contributions,
                contribution => contribution.Disposition is ContributionDisposition.Effective);
            Assert.Contains(presentation.Contributions,
                contribution => contribution.Disposition is ContributionDisposition.Shadowed);
            Assert.All(presentation.Contributions, contribution =>
            {
                Assert.NotEmpty(contribution.ContributionId);
                Assert.NotEmpty(contribution.Summary);
                Assert.NotEmpty(contribution.Explanation);
                Assert.NotEmpty(contribution.Source.PhysicalPath);
                Assert.NotEmpty(contribution.Source.VirtualPath);
                Assert.True(contribution.Source.SpanLength > 0);
                Assert.StartsWith("Line ", contribution.Source.Location);
                Assert.Equal(
                    contribution.Source.PhysicalPath,
                    snapshot.DocumentsById[contribution.Source.DocumentId].PhysicalPath);
                Assert.Equal(contribution.Source.DocumentId, contribution.NavigationRequest.DocumentId);
                Assert.Equal(contribution.Source.PhysicalPath, contribution.NavigationRequest.PhysicalPath);
                Assert.Equal(contribution.Source.VirtualPath, contribution.NavigationRequest.VirtualPath);
                Assert.Equal(contribution.Source.LayerId, contribution.NavigationRequest.LayerId);
                Assert.Equal(contribution.Source.SpanStart, contribution.NavigationRequest.SpanStart);
                Assert.Equal(contribution.Source.SpanLength, contribution.NavigationRequest.SpanLength);
                Assert.Equal(presentation.SemanticIdentity, contribution.NavigationRequest.SemanticIdentity);
            });
            Assert.Equal(
                presentation.EffectiveSource?.DocumentId,
                presentation.EffectiveNavigationRequest?.DocumentId);
            var comparison = Assert.Single(presentation.Comparisons);
            Assert.Equal(comparison.ShadowedSource.DocumentId, comparison.ShadowedNavigationRequest.DocumentId);
            Assert.Equal(presentation.SemanticIdentity, comparison.ShadowedNavigationRequest.SemanticIdentity);
        });

        var stateFields = Assert.Single(presentations[0].Comparisons).Fields;
        Assert.Equal(ContributionFieldDifference.Unchanged,
            Assert.Single(stateFields, field => field.FieldName == "Name key").Difference);
        Assert.Equal(ContributionFieldDifference.ShadowedOnly,
            Assert.Single(stateFields, field => field.FieldName == "Manpower").Difference);
        Assert.Equal(ContributionFieldDifference.EffectiveOnly,
            Assert.Single(stateFields, field => field.FieldName == "Category").Difference);
        Assert.Equal(ContributionFieldDifference.Changed,
            Assert.Single(stateFields, field => field.FieldName == "Provinces").Difference);
        Assert.Equal(ContributionFieldDifference.Changed,
            Assert.Single(Assert.Single(presentations[1].Comparisons).Fields,
                field => field.FieldName == "History path").Difference);
        Assert.Equal(ContributionFieldDifference.Changed,
            Assert.Single(Assert.Single(presentations[2].Comparisons).Fields,
                field => field.FieldName == "Provinces").Difference);
        Assert.Equal(ContributionFieldDifference.Changed,
            Assert.Single(Assert.Single(presentations[3].Comparisons).Fields,
                field => field.FieldName == "Text").Difference);
    }

    [Fact]
    public async Task Excluded_file_replacement_and_same_layer_ambiguity_remain_distinct()
    {
        using var excludedFixture = new TemporaryWorkspace();
        const string collision = "history/states/1-Collision.txt";
        excludedFixture.WriteGameFile(collision, "state={ id=1 }");
        excludedFixture.WriteModFile(collision, "state={ id=1 }");
        using var excludedService = new WorkspaceService();
        var excludedSnapshot = await excludedService.OpenAsync(
            new WorkspaceConfiguration(excludedFixture.GameRoot, excludedFixture.ModRoot));
        var excluded = ContributionSetPresentation.Create(excludedSnapshot.Semantics.States[1], excludedSnapshot);

        Assert.Equal(ContributionOutcomePresentation.Effective, excluded.Outcome);
        Assert.Contains(excluded.Contributions,
            contribution => contribution.Disposition is ContributionDisposition.Excluded);
        Assert.Contains(excluded.Contributions,
            contribution => contribution.Disposition is ContributionDisposition.Effective);

        using var ambiguousFixture = new TemporaryWorkspace();
        ambiguousFixture.WriteGameFile("history/states/1-First.txt", "state={ id=1 }");
        ambiguousFixture.WriteGameFile("history/states/1-Second.txt", "state={ id=1 }");
        using var ambiguousService = new WorkspaceService();
        var ambiguousSnapshot = await ambiguousService.OpenAsync(
            new WorkspaceConfiguration(ambiguousFixture.GameRoot));
        var ambiguous = ContributionSetPresentation.Create(
            ambiguousSnapshot.Semantics.States[1],
            ambiguousSnapshot);

        Assert.Equal(ContributionOutcomePresentation.Ambiguous, ambiguous.Outcome);
        Assert.Null(ambiguous.EffectiveSource);
        Assert.All(ambiguous.Contributions,
            contribution => Assert.Equal(ContributionDisposition.Ambiguous, contribution.Disposition));
        Assert.Contains("ambiguous", ambiguous.OutcomeLabel, StringComparison.OrdinalIgnoreCase);
    }
}

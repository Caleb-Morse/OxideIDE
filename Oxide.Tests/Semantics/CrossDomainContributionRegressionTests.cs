using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class CrossDomainContributionRegressionTests
{
    [Fact]
    public async Task Layered_reload_publishes_one_coherent_cross_domain_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/1-Base.txt",
            "state={ id=1 name=STATE_1 provinces={ 10 } history={ owner=BBB } }");
        fixture.WriteGameFile(
            "common/country_tags/00_base.txt",
            "BBB=\"countries/Base.txt\"");
        fixture.WriteGameFile(
            "map/strategicregions/1-Base.txt",
            "strategic_region={ id=1 name=REGION_BASE provinces={ 10 } }");
        fixture.WriteGameFile(
            "localisation/english/base_l_english.yml",
            "l_english:\n STATE_1:0 \"Base State\"\n BBB:0 \"Base Country\"\n REGION_BASE:0 \"Base Region\"\n");
        using var service = new WorkspaceService();
        var configuration = new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot);

        var original = await service.OpenAsync(configuration);

        fixture.WriteModFile(
            "history/states/1-Mod.txt",
            "state={ id=1 name=STATE_1 provinces={ 20 } history={ owner=BBB } }");
        fixture.WriteModFile(
            "common/country_tags/00_mod.txt",
            "BBB=\"countries/Mod.txt\"");
        fixture.WriteModFile(
            "map/strategicregions/1-Mod.txt",
            "strategic_region={ id=1 name=REGION_MOD provinces={ 20 } }");
        fixture.WriteModFile(
            "localisation/english/mod_l_english.yml",
            "l_english:\n STATE_1:0 \"Mod State\"\n BBB:0 \"Mod Country\"\n REGION_MOD:0 \"Mod Region\"\n");

        var reloaded = await service.ReloadAsync();
        var state = reloaded.Semantics.States[1];
        var country = reloaded.Semantics.Countries["BBB"];
        var region = reloaded.Semantics.StrategicRegions[1];
        var membership = reloaded.Semantics.StateStrategicRegionMemberships[1];
        var resolver = reloaded.Semantics.LocalisationResolver;

        Assert.Equal("Base State", original.Semantics.LocalisationResolver
            .ResolveName(original.Semantics.States[1], "english").DisplayText);
        Assert.Equal([10], original.Semantics.States[1].Provinces.Select(province => province.Value));
        Assert.Equal("Base Region", original.Semantics.LocalisationResolver
            .ResolveName(original.Semantics.StrategicRegions[1], "english").DisplayText);

        Assert.Equal("Mod State", resolver.ResolveName(state, "english").DisplayText);
        Assert.Equal("Mod Country", resolver.ResolveName(country, "english").DisplayText);
        Assert.Equal("Mod Region", resolver.ResolveName(region, "english").DisplayText);
        Assert.Equal("countries/Mod.txt", country.DefinitionPath?.Value);
        Assert.Equal([20], state.Provinces.Select(province => province.Value));
        Assert.Equal([20], region.Provinces.Select(province => province.Value));
        Assert.Equal(StateStrategicRegionMembershipStatus.SingleRegion, membership.Status);
        Assert.Same(region, Assert.Single(membership.Regions));
        Assert.Same(country, Assert.IsType<ResolvedCountry>(state.Owner!.Resolution).Target);

        Assert.Single(state.ContributionResolution.ShadowedContributions);
        Assert.Single(country.ContributionResolution.ShadowedContributions);
        Assert.Single(region.ContributionResolution.ShadowedContributions);
        Assert.Equal(1, state.EffectiveDeclaration!.Provenance.Layer.Position);
        Assert.Equal(1, country.EffectiveDeclaration!.Provenance.Layer.Position);
        Assert.Equal(1, region.EffectiveDeclaration!.Provenance.Layer.Position);
        Assert.Equal(1, state.Provinces[0].Provenance.Layer.Position);
        var regionCandidate = Assert.Single(Assert.IsType<ResolvedProvinceStrategicRegion>(
            Assert.Single(membership.Provinces).Resolution).Candidates);
        Assert.Equal(1, regionCandidate.Provenance.Layer.Position);

        foreach (var entity in new ISemanticEntity[] { state, country, region })
        {
            var name = Assert.IsType<ResolvedLocalisation>(resolver.ResolveName(entity, "english").Resolution);
            Assert.Equal(1, name.Provenance.Layer.Position);
            Assert.Contains(
                name.ContributionResolution.Reason.Kind,
                new[]
                {
                    ContributionResolutionReasonKind.HigherLayerPrecedence,
                    ContributionResolutionReasonKind.SingleCandidate,
                });
        }

        var progress = new InlineProgress<WorkspaceLoadProgress>(report =>
        {
            if (report.Stage is WorkspaceLoadStage.LoadingDocuments)
            {
                throw new ExpectedLoadFailureException();
            }
        });
        await Assert.ThrowsAsync<ExpectedLoadFailureException>(() => service.ReloadAsync(progress));
        Assert.Same(reloaded, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Same_layer_cross_domain_duplicates_remain_explicit_and_diagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-First.txt", "state={ id=1 provinces={ 10 } }");
        fixture.WriteGameFile("history/states/1-Second.txt", "state={ id=1 provinces={ 11 } }");
        fixture.WriteGameFile(
            "common/country_tags/00_duplicates.txt",
            "AAA=\"countries/First.txt\"\nAAA=\"countries/Second.txt\"");
        fixture.WriteGameFile("map/strategicregions/1-First.txt", "strategic_region={ id=1 provinces={ 10 } }");
        fixture.WriteGameFile("map/strategicregions/1-Second.txt", "strategic_region={ id=1 provinces={ 11 } }");
        fixture.WriteGameFile(
            "localisation/english/duplicates_l_english.yml",
            "l_english:\n AAA:0 \"First\"\n AAA:0 \"Second\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(SemanticEntityStatus.Ambiguous, snapshot.Semantics.States[1].Status);
        Assert.Equal(SemanticEntityStatus.Ambiguous, snapshot.Semantics.Countries["AAA"].Status);
        Assert.Equal(SemanticEntityStatus.Ambiguous, snapshot.Semantics.StrategicRegions[1].Status);
        Assert.IsType<AmbiguousLocalisation>(snapshot.Semantics.LocalisationResolver.Resolve("english", "AAA"));
        Assert.True(snapshot.Semantics.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE4003") >= 3);
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4009");
        Assert.Equal(
            StateStrategicRegionMembershipStatus.Ambiguous,
            snapshot.Semantics.StateStrategicRegionMemberships[1].Status);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ExpectedLoadFailureException : Exception;
}

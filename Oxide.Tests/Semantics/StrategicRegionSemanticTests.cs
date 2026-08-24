using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class StrategicRegionSemanticTests
{
    [Fact]
    public async Task Unique_declaration_builds_an_indexed_entity_with_effective_provenance()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/7-Coast.txt",
            "strategic_region={ id=7 name=STRATEGICREGION_7 provinces={ 11 12 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var region = snapshot.Semantics.StrategicRegions[7];

        Assert.Equal(SemanticEntityStatus.Effective, region.Status);
        Assert.Equal("STRATEGICREGION_7", region.Name?.Value);
        Assert.Equal([11, 12], region.Provinces.Select(province => province.Value));
        Assert.Same(region, snapshot.Semantics.Entities[EntityId.StrategicRegion(7)]);
        Assert.Equal("STRATEGICREGION_7", snapshot.DocumentsById[region.Name!.Provenance.DocumentId].Text!
            .GetText(region.Name.Provenance.Span));
        Assert.All(region.Provinces, province =>
            Assert.Equal(province.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                snapshot.DocumentsById[province.Provenance.DocumentId].Text!.GetText(province.Provenance.Span)));
    }

    [Fact]
    public async Task Higher_layer_region_is_effective_and_retains_the_shadowed_declaration()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/1-Base.txt",
            "strategic_region={ id=1 name=BASE provinces={ 1 } }");
        fixture.WriteModFile(
            "map/strategicregions/1-Mod.txt",
            "strategic_region={ id=1 name=MOD provinces={ 2 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var region = snapshot.Semantics.StrategicRegions[1];

        Assert.Equal(SemanticEntityStatus.Effective, region.Status);
        Assert.Equal(2, region.Contributions.Length);
        Assert.Equal("MOD", region.Name?.Value);
        Assert.Equal([2], region.Provinces.Select(province => province.Value));
        Assert.Equal(ContributionResolutionKind.Effective, region.ContributionResolution.Kind);
        Assert.Equal(
            ContributionDisposition.Shadowed,
            Assert.Single(region.ContributionResolution.ShadowedContributions).Disposition);
        Assert.IsType<MissingProvinceStrategicRegion>(snapshot.Semantics.ProvinceStrategicRegionIndex.Resolve(1));
        Assert.IsType<ResolvedProvinceStrategicRegion>(snapshot.Semantics.ProvinceStrategicRegionIndex.Resolve(2));
    }

    [Fact]
    public async Task Same_layer_duplicate_region_identity_remains_ambiguous()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/1-First.txt",
            "strategic_region={ id=1 name=FIRST provinces={ 1 } }");
        fixture.WriteGameFile(
            "map/strategicregions/1-Second.txt",
            "strategic_region={ id=1 name=SECOND provinces={ 2 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var region = snapshot.Semantics.StrategicRegions[1];

        Assert.Equal(SemanticEntityStatus.Ambiguous, region.Status);
        Assert.Equal(2, region.Contributions.Length);
        Assert.Null(region.Name);
        Assert.Empty(region.Provinces);
        Assert.Contains(region.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4003");
    }

    [Fact]
    public async Task Duplicate_properties_retain_candidates_but_select_no_name()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/2-Duplicates.txt",
            "strategic_region={ id=2 name=ONE name=TWO provinces={ 20 20 21 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var region = snapshot.Semantics.StrategicRegions[2];

        Assert.Equal(SemanticEntityStatus.Effective, region.Status);
        Assert.Null(region.Name);
        Assert.Equal([20, 20, 21], region.Provinces.Select(province => province.Value));
        Assert.Contains(region.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4013");
        Assert.Contains(region.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4015");
    }

    [Fact]
    public async Task Regions_use_the_shared_language_and_english_fallback_resolver()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/3-Localised.txt",
            "strategic_region={ id=3 name=STRATEGICREGION_3 provinces={ 30 } }");
        fixture.WriteGameFile(
            "map/strategicregions/4-Unnamed.txt",
            "strategic_region={ id=4 provinces={ 40 } }");
        fixture.WriteGameFile(
            "localisation/english/regions_l_english.yml",
            "l_english:\n STRATEGICREGION_3:0 \"English Region\"\n");
        fixture.WriteGameFile(
            "localisation/spanish/regions_l_spanish.yml",
            "l_spanish:\n STRATEGICREGION_3:0 \"Región Española\"\n");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var resolver = snapshot.Semantics.LocalisationResolver;
        var exact = resolver.ResolveName(snapshot.Semantics.StrategicRegions[3], "spanish");
        var fallback = resolver.ResolveName(snapshot.Semantics.StrategicRegions[3], "russian");
        var unnamed = resolver.ResolveName(snapshot.Semantics.StrategicRegions[4], "spanish");

        Assert.Equal("Región Española", exact.DisplayText);
        Assert.IsType<ResolvedLocalisation>(exact.Resolution);
        Assert.Equal("English Region", fallback.DisplayText);
        Assert.True(Assert.IsType<ResolvedLocalisation>(fallback.Resolution).IsFallback);
        Assert.Equal("Strategic region 4", unnamed.DisplayText);
        Assert.Null(unnamed.Resolution);
    }

    [Fact]
    public async Task Reload_replaces_regions_atomically_without_mutating_the_previous_snapshot()
    {
        using var fixture = new TemporaryWorkspace();
        const string path = "map/strategicregions/5-Reload.txt";
        fixture.WriteGameFile(path, "strategic_region={ id=5 name=BEFORE provinces={ 50 } }");
        using var service = new WorkspaceService();
        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        fixture.WriteGameFile(path, "strategic_region={ id=5 name=AFTER provinces={ 51 } }");
        var second = await service.ReloadAsync();

        Assert.Equal("BEFORE", first.Semantics.StrategicRegions[5].Name?.Value);
        Assert.Equal([50], first.Semantics.StrategicRegions[5].Provinces.Select(province => province.Value));
        Assert.Equal("AFTER", second.Semantics.StrategicRegions[5].Name?.Value);
        Assert.Equal([51], second.Semantics.StrategicRegions[5].Provinces.Select(province => province.Value));
        Assert.Same(second, service.CurrentSnapshot);
    }
}

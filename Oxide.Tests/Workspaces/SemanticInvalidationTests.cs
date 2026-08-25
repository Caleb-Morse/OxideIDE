using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Tests.Workspaces;

public sealed class SemanticInvalidationTests
{
    [Fact]
    public async Task Localisation_change_rebuilds_only_localisation_semantics()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.WriteAllText(paths.Localisation, "l_english:\n STATE_1:0 \"Changed state\"\n TST:0 \"Test country\"\n REGION_1:0 \"Test region\"");

        var result = await Refresh(service, original, paths.Localisation);
        var refreshed = service.CurrentSnapshot!;

        Assert.Equal([SemanticRefreshDomain.Localisations], result.Metrics.RebuiltSemanticDomains.ToArray());
        Assert.Equal(5, result.Metrics.ReusedSemanticDomains.Length);
        Assert.Same(original.Semantics.States, refreshed.Semantics.States);
        Assert.Same(original.Semantics.Countries, refreshed.Semantics.Countries);
        Assert.Same(original.Semantics.StrategicRegions, refreshed.Semantics.StrategicRegions);
        Assert.Same(original.Semantics.ProvinceStrategicRegionIndex, refreshed.Semantics.ProvinceStrategicRegionIndex);
        Assert.Same(original.Semantics.StateStrategicRegionMemberships, refreshed.Semantics.StateStrategicRegionMemberships);
        Assert.NotSame(original.Semantics.Localisations, refreshed.Semantics.Localisations);
        Assert.Same(
            original.Semantics.DeclarationInventory.States[0],
            refreshed.Semantics.DeclarationInventory.States[0]);
        var name = Assert.IsType<ResolvedLocalisation>(
            refreshed.Semantics.LocalisationResolver.Resolve("english", "STATE_1"));
        Assert.Equal("Changed state", name.Value);
    }

    [Fact]
    public async Task State_change_rebuilds_states_and_memberships_only()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.WriteAllText(paths.State, "state={ id=1 name=STATE_1 manpower=25 provinces={ 10 } history={ owner=TST } }");

        var result = await Refresh(service, original, paths.State);
        var refreshed = service.CurrentSnapshot!;

        Assert.Equal(
            [SemanticRefreshDomain.States, SemanticRefreshDomain.StateStrategicRegionMemberships],
            result.Metrics.RebuiltSemanticDomains.ToArray());
        Assert.NotSame(original.Semantics.States, refreshed.Semantics.States);
        Assert.NotSame(original.Semantics.StateStrategicRegionMemberships, refreshed.Semantics.StateStrategicRegionMemberships);
        Assert.Same(original.Semantics.Countries, refreshed.Semantics.Countries);
        Assert.Same(original.Semantics.StrategicRegions, refreshed.Semantics.StrategicRegions);
        Assert.Same(original.Semantics.ProvinceStrategicRegionIndex, refreshed.Semantics.ProvinceStrategicRegionIndex);
        Assert.Same(original.Semantics.Localisations, refreshed.Semantics.Localisations);
        Assert.Equal(25, refreshed.Semantics.States[1].Manpower?.Value);
    }

    [Fact]
    public async Task Country_change_rebuilds_country_references_and_dependent_memberships()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.WriteAllText(paths.Country, "TST=\"countries/Changed.txt\"");

        var result = await Refresh(service, original, paths.Country);
        var refreshed = service.CurrentSnapshot!;

        Assert.Equal(
            [
                SemanticRefreshDomain.Countries,
                SemanticRefreshDomain.States,
                SemanticRefreshDomain.StateStrategicRegionMemberships,
            ],
            result.Metrics.RebuiltSemanticDomains.ToArray());
        Assert.NotSame(original.Semantics.Countries, refreshed.Semantics.Countries);
        Assert.NotSame(original.Semantics.States, refreshed.Semantics.States);
        Assert.Same(original.Semantics.StrategicRegions, refreshed.Semantics.StrategicRegions);
        Assert.Same(original.Semantics.ProvinceStrategicRegionIndex, refreshed.Semantics.ProvinceStrategicRegionIndex);
        Assert.Same(original.Semantics.Localisations, refreshed.Semantics.Localisations);
        Assert.Equal("countries/Changed.txt", refreshed.Semantics.Countries["TST"].DefinitionPath?.Value);
        var owner = Assert.IsType<ResolvedCountry>(refreshed.Semantics.States[1].Owner!.Resolution);
        Assert.Same(refreshed.Semantics.Countries["TST"], owner.Target);
    }

    [Fact]
    public async Task Strategic_region_change_rebuilds_index_and_memberships_from_effective_inputs()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.WriteAllText(paths.Region, "strategic_region={ id=1 name=REGION_1 provinces={ 11 } }");

        var result = await Refresh(service, original, paths.Region);
        var refreshed = service.CurrentSnapshot!;

        Assert.Equal(
            [
                SemanticRefreshDomain.StrategicRegions,
                SemanticRefreshDomain.ProvinceStrategicRegionIndex,
                SemanticRefreshDomain.StateStrategicRegionMemberships,
            ],
            result.Metrics.RebuiltSemanticDomains.ToArray());
        Assert.Same(original.Semantics.States, refreshed.Semantics.States);
        Assert.Same(original.Semantics.Countries, refreshed.Semantics.Countries);
        Assert.NotSame(original.Semantics.StrategicRegions, refreshed.Semantics.StrategicRegions);
        Assert.NotSame(original.Semantics.ProvinceStrategicRegionIndex, refreshed.Semantics.ProvinceStrategicRegionIndex);
        Assert.NotSame(original.Semantics.StateStrategicRegionMemberships, refreshed.Semantics.StateStrategicRegionMemberships);
        Assert.Same(original.Semantics.Localisations, refreshed.Semantics.Localisations);
        Assert.Equal(
            StateStrategicRegionMembershipStatus.Missing,
            refreshed.Semantics.StateStrategicRegionMemberships[1].Status);
        Assert.Same(
            refreshed.Semantics.States[1].Provinces[0],
            refreshed.Semantics.StateStrategicRegionMemberships[1].Provinces[0].StateProvince);
    }

    [Fact]
    public async Task Reused_domains_preserve_non_simple_diagnostics()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        fixture.WriteGameFile(
            "map/strategicregions/2-Conflict.txt",
            "strategic_region={ id=2 name=REGION_2 provinces={ 10 } }");
        File.WriteAllText(
            paths.Localisation,
            "l_english:\n STATE_1:0 \"First\"\n STATE_1:0 \"Second\"\n TST:0 \"Country\"");
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        Assert.Contains(original.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4009");
        Assert.Contains(original.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4016");
        File.WriteAllText(paths.State, "state={ id=1 name=STATE_1 manpower=30 provinces={ 10 } history={ owner=TST } }");

        await Refresh(service, original, paths.State);
        var refreshed = service.CurrentSnapshot!;

        Assert.Contains(refreshed.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4009");
        Assert.Contains(refreshed.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4016");
        Assert.Equal(
            original.Semantics.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE4009"),
            refreshed.Semantics.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE4009"));
        Assert.Equal(
            original.Semantics.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE4016"),
            refreshed.Semantics.Diagnostics.Count(diagnostic => diagnostic.Code == "OXIDE4016"));
    }

    [Fact]
    public void Invalidation_plan_unions_dependencies_without_rebuilding_unrelated_domains()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        var layer = ContentLayer.BaseGame(fixture.GameRoot);
        var state = ClassifiedChange(layer, paths.State, ContentCategory.StateHistory);
        var localisation = ClassifiedChange(layer, paths.Localisation, ContentCategory.Localisation);

        var plan = SemanticInvalidationPlan.Create([state, localisation]);

        Assert.Equal(
            [
                SemanticRefreshDomain.States,
                SemanticRefreshDomain.StateStrategicRegionMemberships,
                SemanticRefreshDomain.Localisations,
            ],
            plan.RebuiltDomains.ToArray());
        Assert.Contains(SemanticRefreshDomain.Countries, plan.ReusedDomains);
        Assert.Contains(SemanticRefreshDomain.StrategicRegions, plan.ReusedDomains);
        Assert.Contains(SemanticRefreshDomain.ProvinceStrategicRegionIndex, plan.ReusedDomains);
    }

    [Fact]
    public async Task Incremental_semantics_match_a_clean_full_reload()
    {
        using var fixture = new TemporaryWorkspace();
        var paths = WriteSupportedFixture(fixture);
        using var service = new WorkspaceService();
        var original = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        File.WriteAllText(paths.Country, "TST=\"countries/Changed.txt\"");
        File.WriteAllText(paths.State, "state={ id=1 name=STATE_1 manpower=40 provinces={ 10 } history={ owner=TST } }");
        var changes = new[]
        {
            ChangeFor(original, paths.Country),
            ChangeFor(original, paths.State),
        };

        await service.RefreshAsync(new IncrementalRefreshRequest(
            original.Version,
            WorkspaceRefreshTrigger.Automatic,
            new WorkspaceChangeBatch(changes)));
        var incremental = service.CurrentSnapshot!;
        var full = await service.ReloadAsync();

        Assert.Equal(SemanticFingerprint(full), SemanticFingerprint(incremental));
        Assert.Equal(
            full.Semantics.Diagnostics.GroupBy(diagnostic => diagnostic.Code).ToDictionary(group => group.Key, group => group.Count()),
            incremental.Semantics.Diagnostics.GroupBy(diagnostic => diagnostic.Code).ToDictionary(group => group.Key, group => group.Count()));
    }

    private static async Task<WorkspaceRefreshResult> Refresh(
        WorkspaceService service,
        WorkspaceSnapshot snapshot,
        string path) =>
        await service.RefreshAsync(new IncrementalRefreshRequest(
            snapshot.Version,
            WorkspaceRefreshTrigger.Automatic,
            new WorkspaceChangeBatch([ChangeFor(snapshot, path)])));

    private static DocumentChange ChangeFor(WorkspaceSnapshot snapshot, string path)
    {
        var document = snapshot.Documents.Single(candidate =>
            string.Equals(candidate.PhysicalPath, path, StringComparison.Ordinal));
        var classified = WorkspaceChangeClassifier.Classify(document.Layer, path);
        return new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Changed,
                classified.Source,
                classified.Source,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            classified.DocumentKind!.Value,
            classified.Category!.Value);
    }

    private static DocumentChange ClassifiedChange(
        ContentLayer layer,
        string path,
        ContentCategory category)
    {
        var classified = WorkspaceChangeClassifier.Classify(layer, path);
        Assert.Equal(category, classified.Category);
        return new DocumentChange(
            new WorkspaceChange(
                WorkspaceChangeKind.Changed,
                classified.Source,
                classified.Source,
                DateTimeOffset.UnixEpoch,
                WorkspaceChangeOrigin.Watcher),
            classified.DocumentKind!.Value,
            classified.Category!.Value);
    }

    private static string SemanticFingerprint(WorkspaceSnapshot snapshot)
    {
        var entries = snapshot.Semantics.States.Values
            .OrderBy(state => state.Id.LocalKey)
            .Select(state => $"S:{state.Id.LocalKey}:{state.Name?.Value}:{state.Manpower?.Value}:{state.Owner?.OriginalTag}")
            .Concat(snapshot.Semantics.Countries.Values.OrderBy(country => country.Id.LocalKey).Select(country =>
                $"C:{country.Id.LocalKey}:{country.DefinitionPath?.Value}"))
            .Concat(snapshot.Semantics.StrategicRegions.Values.OrderBy(region => region.Id.LocalKey).Select(region =>
                $"R:{region.Id.LocalKey}:{string.Join(',', region.Provinces.Select(province => province.Value))}"))
            .Concat(snapshot.Semantics.StateStrategicRegionMemberships.Values
                .OrderBy(membership => membership.StateId.LocalKey)
                .Select(membership => $"M:{membership.StateId.LocalKey}:{membership.Status}"));
        return string.Join('|', entries);
    }

    private static FixturePaths WriteSupportedFixture(TemporaryWorkspace fixture) => new(
        fixture.WriteGameFile(
            "history/states/1-Test.txt",
            "state={ id=1 name=STATE_1 manpower=10 provinces={ 10 } history={ owner=TST } }"),
        fixture.WriteGameFile("common/country_tags/00_tags.txt", "TST=\"countries/Test.txt\""),
        fixture.WriteGameFile(
            "map/strategicregions/1-Test.txt",
            "strategic_region={ id=1 name=REGION_1 provinces={ 10 } }"),
        fixture.WriteGameFile(
            "localisation/english/test_l_english.yml",
            "l_english:\n STATE_1:0 \"Test state\"\n TST:0 \"Test country\"\n REGION_1:0 \"Test region\""));

    private sealed record FixturePaths(
        string State,
        string Country,
        string Region,
        string Localisation);
}

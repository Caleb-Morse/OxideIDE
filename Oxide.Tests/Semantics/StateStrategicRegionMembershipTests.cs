using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class StateStrategicRegionMembershipTests
{
    [Fact]
    public async Task State_membership_outcomes_are_distinct_and_retain_both_sides_of_provenance()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/1-One.txt",
            "strategic_region={ id=1 name=REGION_1 provinces={ 10 11 12 50 70 70 } }");
        fixture.WriteGameFile(
            "map/strategicregions/2-Two.txt",
            "strategic_region={ id=2 name=REGION_2 provinces={ 20 50 } }");
        fixture.WriteGameFile(
            "map/strategicregions/3-Base.txt",
            "strategic_region={ id=3 name=REGION_3_BASE provinces={ 60 } }");
        fixture.WriteModFile(
            "map/strategicregions/3-Mod.txt",
            "strategic_region={ id=3 name=REGION_3_MOD provinces={ 61 } }");
        fixture.WriteGameFile("history/states/1-Single.txt", "state={ id=1 provinces={ 10 11 } }");
        fixture.WriteGameFile("history/states/2-Partial.txt", "state={ id=2 provinces={ 10 99 } }");
        fixture.WriteGameFile("history/states/3-Missing.txt", "state={ id=3 provinces={ 99 } }");
        fixture.WriteGameFile("history/states/4-Split.txt", "state={ id=4 provinces={ 10 20 } }");
        fixture.WriteGameFile("history/states/5-Conflict.txt", "state={ id=5 provinces={ 50 } }");
        fixture.WriteGameFile("history/states/6-AmbiguousRegion.txt", "state={ id=6 provinces={ 60 } }");
        fixture.WriteGameFile("history/states/7-None.txt", "state={ id=7 }");
        fixture.WriteGameFile("history/states/8-Repeated.txt", "state={ id=8 provinces={ 70 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var memberships = snapshot.Semantics.StateStrategicRegionMemberships;

        Assert.Equal(StateStrategicRegionMembershipStatus.SingleRegion, memberships[1].Status);
        Assert.Equal([1], memberships[1].Regions.Select(region => int.Parse(region.Id.LocalKey)));
        Assert.Equal(StateStrategicRegionMembershipStatus.Partial, memberships[2].Status);
        Assert.Equal(1, memberships[2].ResolvedProvinceCount);
        Assert.Equal(1, memberships[2].MissingProvinceCount);
        Assert.Equal(StateStrategicRegionMembershipStatus.Missing, memberships[3].Status);
        Assert.Equal(StateStrategicRegionMembershipStatus.Split, memberships[4].Status);
        Assert.Equal([1, 2], memberships[4].Regions.Select(region => int.Parse(region.Id.LocalKey)));
        Assert.Equal(StateStrategicRegionMembershipStatus.Ambiguous, memberships[5].Status);
        Assert.Equal(StateStrategicRegionMembershipStatus.Ambiguous, memberships[6].Status);
        Assert.Equal(StateStrategicRegionMembershipStatus.NoProvinces, memberships[7].Status);
        Assert.Equal(StateStrategicRegionMembershipStatus.SingleRegion, memberships[8].Status);

        var repeated = Assert.IsType<ResolvedProvinceStrategicRegion>(
            Assert.Single(memberships[8].Provinces).Resolution);
        Assert.Equal(2, repeated.Candidates.Length);

        var stateReference = Assert.Single(memberships[1].Provinces, reference => reference.ProvinceId == 10);
        var resolved = Assert.IsType<ResolvedProvinceStrategicRegion>(stateReference.Resolution);
        var regionCandidate = Assert.Single(resolved.Candidates);
        Assert.Equal("10", snapshot.DocumentsById[stateReference.StateProvince.Provenance.DocumentId].Text!
            .GetText(stateReference.StateProvince.Provenance.Span));
        Assert.Equal("10", snapshot.DocumentsById[regionCandidate.Provenance.DocumentId].Text!
            .GetText(regionCandidate.Provenance.Span));

        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4016");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4017");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4018");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4019");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4020");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4021");
    }

    [Fact]
    public async Task Province_index_retains_all_candidates_and_resolves_deterministically()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "map/strategicregions/z-Second.txt",
            "strategic_region={ id=2 provinces={ 100 } }");
        fixture.WriteGameFile(
            "map/strategicregions/a-First.txt",
            "strategic_region={ id=1 provinces={ 100 101 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var index = snapshot.Semantics.ProvinceStrategicRegionIndex;
        var ambiguous = Assert.IsType<AmbiguousProvinceStrategicRegion>(index.Resolve(100));

        Assert.Equal(["1", "2"], ambiguous.Candidates.Select(candidate => candidate.Region.Id.LocalKey));
        Assert.Equal("1", Assert.IsType<ResolvedProvinceStrategicRegion>(index.Resolve(101)).Region.Id.LocalKey);
        Assert.IsType<MissingProvinceStrategicRegion>(index.Resolve(999));

        var reloaded = await service.ReloadAsync();
        var reloadedCandidates = Assert.IsType<AmbiguousProvinceStrategicRegion>(
            reloaded.Semantics.ProvinceStrategicRegionIndex.Resolve(100)).Candidates;
        Assert.Equal(
            ambiguous.Candidates.Select(candidate => candidate.Provenance.PhysicalPath),
            reloadedCandidates.Select(candidate => candidate.Provenance.PhysicalPath));
    }

    [Fact]
    public async Task Workspace_without_region_documents_does_not_add_membership_diagnostics()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-State.txt", "state={ id=1 provinces={ 1 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(StateStrategicRegionMembershipStatus.Missing,
            snapshot.Semantics.StateStrategicRegionMemberships[1].Status);
        Assert.DoesNotContain(snapshot.Semantics.Diagnostics,
            diagnostic => diagnostic.Code is "OXIDE4017" or "OXIDE4018" or "OXIDE4019" or "OXIDE4020" or "OXIDE4021");
    }
}

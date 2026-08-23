using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Tests.Semantics;

public sealed class RealSemanticCorpusTests
{
    [Fact]
    [Trait("Category", "ExternalCorpus")]
    public async Task Configured_hoi4_installation_builds_state_and_country_indexes()
    {
        var root = Environment.GetEnvironmentVariable("OXIDE_HOI4_CORPUS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(root));

        Assert.Equal(1_081, snapshot.Semantics.StateDeclarations.Length);
        Assert.Equal(1_081, snapshot.Semantics.States.Count);
        Assert.Equal(304, snapshot.Semantics.StrategicRegionDeclarations.Length);
        Assert.Equal(304, snapshot.Semantics.StrategicRegions.Count);
        Assert.Equal(13_413, snapshot.Semantics.ProvinceStrategicRegionIndex.CandidatesByProvince.Count);
        Assert.Equal(1_081, snapshot.Semantics.StateStrategicRegionMemberships.Count);
        Assert.All(snapshot.Semantics.StateStrategicRegionMemberships.Values, membership =>
            Assert.Equal(Oxide.Core.Semantics.Resolution.StateStrategicRegionMembershipStatus.SingleRegion,
                membership.Status));
        Assert.True(snapshot.Semantics.Countries.Count > 200);
        Assert.All(snapshot.Semantics.States.Values, state =>
            Assert.Equal(SemanticEntityStatus.Effective, state.Status));
        Assert.All(snapshot.Semantics.StrategicRegions.Values, region =>
            Assert.Equal(SemanticEntityStatus.Effective, region.Status));
    }
}

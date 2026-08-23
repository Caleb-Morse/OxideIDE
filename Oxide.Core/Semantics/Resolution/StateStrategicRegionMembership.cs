using System.Collections.Immutable;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Resolution;

public enum StateStrategicRegionMembershipStatus
{
    SingleRegion,
    Split,
    Partial,
    Missing,
    Ambiguous,
    NoProvinces,
}

public sealed record StateStrategicRegionMembership(
    EntityId StateId,
    StateStrategicRegionMembershipStatus Status,
    ImmutableArray<ProvinceStrategicRegionReference> Provinces,
    ImmutableArray<StrategicRegionEntity> Regions,
    ImmutableArray<SemanticDiagnostic> Diagnostics)
{
    public int ResolvedProvinceCount => Provinces.Count(province =>
        province.Resolution is ResolvedProvinceStrategicRegion);

    public int MissingProvinceCount => Provinces.Count(province =>
        province.Resolution is MissingProvinceStrategicRegion);

    public int AmbiguousProvinceCount => Provinces.Count(province =>
        province.Resolution is AmbiguousProvinceStrategicRegion);
}

using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Resolution;

public sealed record ProvinceStrategicRegionCandidate(
    int ProvinceId,
    StrategicRegionEntity Region,
    StrategicRegionDeclaration Declaration,
    SourceProvenance Provenance);

public abstract record ProvinceStrategicRegionResolution(int ProvinceId);

public sealed record ResolvedProvinceStrategicRegion(
    int ProvinceId,
    StrategicRegionEntity Region,
    ImmutableArray<ProvinceStrategicRegionCandidate> Candidates)
    : ProvinceStrategicRegionResolution(ProvinceId);

public sealed record MissingProvinceStrategicRegion(int ProvinceId)
    : ProvinceStrategicRegionResolution(ProvinceId);

public sealed record AmbiguousProvinceStrategicRegion(
    int ProvinceId,
    ImmutableArray<ProvinceStrategicRegionCandidate> Candidates,
    string Reason)
    : ProvinceStrategicRegionResolution(ProvinceId);

public sealed record ProvinceStrategicRegionReference(
    EffectiveValue<int> StateProvince,
    ProvinceStrategicRegionResolution Resolution)
{
    public int ProvinceId => StateProvince.Value;
}

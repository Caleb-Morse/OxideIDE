namespace Oxide.Core.Verification;

public sealed record StrategicRegionMembershipCounts(
    int Total,
    int SingleRegion,
    int Split,
    int Partial,
    int Missing,
    int Ambiguous,
    int NoProvinces);

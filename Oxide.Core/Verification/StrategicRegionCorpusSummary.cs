namespace Oxide.Core.Verification;

public sealed record StrategicRegionCorpusSummary(
    int FilesDiscovered,
    int DocumentsLoaded,
    int DocumentsFailed,
    int DeclarationCount,
    int EntityCount,
    int EffectiveEntityCount,
    int AmbiguousEntityCount,
    int ProvinceCandidateCount,
    int RepeatedProvinceCandidateCount,
    int IndexedProvinceCount,
    int AmbiguousProvinceCount,
    int DeclarationsWithValidProvenance,
    int ProvinceCandidatesWithValidProvenance,
    StrategicRegionMembershipCounts StateMemberships);

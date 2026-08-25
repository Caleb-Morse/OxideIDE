using System.Collections.Immutable;

namespace Oxide.Core.Verification;

public sealed record ContributionDispositionCounts(
    int Total,
    int Effective,
    int Shadowed,
    int Ambiguous,
    int Invalid,
    int Excluded);

public sealed record ContributionDomainSummary(
    int IdentityCount,
    int MultiContributionIdentityCount,
    int CrossLayerOverrideCount,
    int SameLayerDuplicateIdentityCount,
    int InvalidWinnerIdentityCount,
    int MissingIdentityCount,
    ContributionDispositionCounts Dispositions,
    ImmutableSortedDictionary<string, int> ContributionsByLayer);

public sealed record ContributionCorpusSummary(
    ContributionDomainSummary States,
    ContributionDomainSummary Countries,
    ContributionDomainSummary StrategicRegions,
    ContributionDomainSummary Localisations,
    ContributionDomainSummary AllDomains);

namespace Oxide.Core.Semantics.Contributions;

public sealed record ContributionResolutionReason(
    ContributionResolutionReasonKind Kind,
    string Explanation);

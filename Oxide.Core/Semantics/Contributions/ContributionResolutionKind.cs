namespace Oxide.Core.Semantics.Contributions;

public enum ContributionResolutionKind
{
    Missing,
    Effective,
    DuplicateWithinLayer,
    Ambiguous,
    InvalidWinner,
    UnsupportedPolicy,
}

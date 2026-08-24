namespace Oxide.Core.Semantics.Contributions;

public enum ContributionResolutionReasonKind
{
    NoCandidates,
    SingleCandidate,
    HigherLayerPrecedence,
    LaterDeclarationInOrderedScope,
    FileReplacement,
    LanguageFallback,
    SameLayerDuplicate,
    PolicyDoesNotSelectAcrossLayers,
    HighestPrecedenceCandidateInvalid,
    UnsupportedPolicy,
}

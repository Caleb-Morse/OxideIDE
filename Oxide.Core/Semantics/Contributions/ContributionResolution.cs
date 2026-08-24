using System.Collections.Immutable;

namespace Oxide.Core.Semantics.Contributions;

public sealed record ContributionResolution<TIdentity, TDeclaration>(
    TIdentity Identity,
    ContributionResolutionKind Kind,
    Contribution<TIdentity, TDeclaration>? EffectiveContribution,
    ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> Contributions,
    ContributionResolutionReason Reason)
    where TIdentity : notnull
{
    public ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> ShadowedContributions =>
        Contributions.Where(contribution => contribution.Disposition is ContributionDisposition.Shadowed)
            .ToImmutableArray();

    public ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> InvalidContributions =>
        Contributions.Where(contribution => contribution.Disposition is ContributionDisposition.Invalid)
            .ToImmutableArray();

    public ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> ExcludedContributions =>
        Contributions.Where(contribution => contribution.Disposition is ContributionDisposition.Excluded)
            .ToImmutableArray();
}

using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record LocalisationEntry(
    LocalisationIdentity Identity,
    ContributionResolution<LocalisationIdentity, LocalisationDeclaration> Resolution)
{
    public ImmutableArray<LocalisationDeclaration> Contributions =>
        Resolution.Contributions.Select(contribution => contribution.Contribution.Declaration).ToImmutableArray();

    public ImmutableArray<LocalisationDeclaration> EligibleContributions =>
        Resolution.Contributions
            .Where(contribution => contribution.Contribution.Eligibility is ContributionEligibility.Eligible)
            .Select(contribution => contribution.Contribution.Declaration)
            .ToImmutableArray();

    public LocalisationDeclaration? EffectiveDeclaration => Resolution.EffectiveContribution?.Declaration;

    public bool IsAmbiguous => Resolution.Kind is
        ContributionResolutionKind.Ambiguous or ContributionResolutionKind.DuplicateWithinLayer;
}

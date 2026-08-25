using System.Collections.Immutable;

namespace Oxide.Core.Semantics.Contributions;

public static class ContributionResolver
{
    public static ContributionResolution<TIdentity, TDeclaration> Resolve<TIdentity, TDeclaration>(
        ContributionSet<TIdentity, TDeclaration> set,
        ContributionResolutionPolicy policy)
        where TIdentity : notnull
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(policy);
        if (set.Contributions.IsEmpty)
        {
            return Result(
                ContributionResolutionKind.Missing,
                null,
                [],
                ContributionResolutionReasonKind.NoCandidates,
                "No contributions were supplied for this semantic identity.");
        }

        var eligible = set.Contributions
            .Where(contribution => contribution.Eligibility is ContributionEligibility.Eligible)
            .ToImmutableArray();
        if (eligible.IsEmpty)
        {
            return Result(
                ContributionResolutionKind.Missing,
                null,
                ResolveExcluded(set.Contributions),
                ContributionResolutionReasonKind.NoEligibleCandidates,
                "Contributions exist, but their source documents do not participate.");
        }

        var highestPosition = eligible.Max(contribution => contribution.Provenance.Layer.Position);
        var highest = eligible
            .Where(contribution => contribution.Provenance.Layer.Position == highestPosition)
            .ToImmutableArray();
        var lower = eligible
            .Where(contribution => contribution.Provenance.Layer.Position < highestPosition)
            .ToImmutableArray();

        if (highest.Length > 1)
        {
            var resolved = set.Contributions.Select(contribution => new ResolvedContribution<TIdentity, TDeclaration>(
                contribution,
                contribution.Eligibility is not ContributionEligibility.Eligible
                    ? ContributionDisposition.Excluded
                    : contribution.Validity is ContributionValidity.Invalid
                    ? ContributionDisposition.Invalid
                    : contribution.Provenance.Layer.Position == highestPosition
                        ? ContributionDisposition.Ambiguous
                        : ContributionDisposition.Shadowed,
                contribution.Eligibility is not ContributionEligibility.Eligible
                    ? contribution.IneligibilityReason ?? "The source document does not participate."
                    : contribution.Validity is ContributionValidity.Invalid
                    ? contribution.InvalidReason ?? "The contribution is invalid."
                    : contribution.Provenance.Layer.Position == highestPosition
                        ? "Another contribution in the same highest-precedence layer has this identity."
                        : "A higher-precedence layer contains duplicate contributions for this identity."))
                .ToImmutableArray();
            return Result(
                ContributionResolutionKind.DuplicateWithinLayer,
                null,
                resolved,
                ContributionResolutionReasonKind.SameLayerDuplicate,
                $"Layer '{highest[0].Provenance.Layer.DisplayName}' contains multiple contributions for this identity.");
        }

        var winner = highest[0];
        if (winner.Validity is ContributionValidity.Invalid)
        {
            var resolved = ResolveDispositions(set.Contributions, winner, ContributionDisposition.Invalid);
            return Result(
                ContributionResolutionKind.InvalidWinner,
                null,
                resolved,
                ContributionResolutionReasonKind.HighestPrecedenceCandidateInvalid,
                $"The highest-precedence contribution is invalid: {winner.InvalidReason}");
        }

        if (lower.Length > 0 && !policy.SelectHigherLayer)
        {
            var resolved = set.Contributions.Select(contribution => new ResolvedContribution<TIdentity, TDeclaration>(
                contribution,
                contribution.Eligibility is not ContributionEligibility.Eligible
                    ? ContributionDisposition.Excluded
                    : contribution.Validity is ContributionValidity.Invalid
                    ? ContributionDisposition.Invalid
                    : ContributionDisposition.Ambiguous,
                contribution.Eligibility is not ContributionEligibility.Eligible
                    ? contribution.IneligibilityReason ?? "The source document does not participate."
                    : contribution.Validity is ContributionValidity.Invalid
                    ? contribution.InvalidReason ?? "The contribution is invalid."
                    : $"Policy '{policy.Name}' does not select between contributing layers."))
                .ToImmutableArray();
            return Result(
                ContributionResolutionKind.Ambiguous,
                null,
                resolved,
                ContributionResolutionReasonKind.PolicyDoesNotSelectAcrossLayers,
                $"Policy '{policy.Name}' does not select a higher-layer contribution.");
        }

        var dispositions = ResolveDispositions(set.Contributions, winner, ContributionDisposition.Effective);
        return Result(
            ContributionResolutionKind.Effective,
            winner,
            dispositions,
            lower.IsEmpty
                ? ContributionResolutionReasonKind.SingleCandidate
                : ContributionResolutionReasonKind.HigherLayerPrecedence,
            lower.IsEmpty
                ? "A single valid contribution is available."
                : $"Layer '{winner.Provenance.Layer.DisplayName}' has the highest precedence.");

        ContributionResolution<TIdentity, TDeclaration> Result(
            ContributionResolutionKind kind,
            Contribution<TIdentity, TDeclaration>? effective,
            ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> contributions,
            ContributionResolutionReasonKind reasonKind,
            string explanation) =>
            new(set.Identity, kind, effective, contributions, new ContributionResolutionReason(reasonKind, explanation));
    }

    private static ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> ResolveDispositions<TIdentity, TDeclaration>(
        ImmutableArray<Contribution<TIdentity, TDeclaration>> contributions,
        Contribution<TIdentity, TDeclaration> winner,
        ContributionDisposition winnerDisposition)
        where TIdentity : notnull =>
        contributions.Select(contribution =>
        {
            if (contribution.Eligibility is not ContributionEligibility.Eligible)
            {
                return new ResolvedContribution<TIdentity, TDeclaration>(
                    contribution,
                    ContributionDisposition.Excluded,
                    contribution.IneligibilityReason ?? "The source document does not participate.");
            }

            if (contribution.Id == winner.Id)
            {
                return new ResolvedContribution<TIdentity, TDeclaration>(
                    contribution,
                    winnerDisposition,
                    winnerDisposition is ContributionDisposition.Effective
                        ? "This is the selected highest-precedence contribution."
                        : contribution.InvalidReason ?? "The highest-precedence contribution is invalid.");
            }

            if (contribution.Validity is ContributionValidity.Invalid)
            {
                return new ResolvedContribution<TIdentity, TDeclaration>(
                    contribution,
                    ContributionDisposition.Invalid,
                    contribution.InvalidReason ?? "The contribution is invalid.");
            }

            return new ResolvedContribution<TIdentity, TDeclaration>(
                contribution,
                ContributionDisposition.Shadowed,
                "A valid contribution from a higher-precedence layer was selected.");
        }).ToImmutableArray();

    private static ImmutableArray<ResolvedContribution<TIdentity, TDeclaration>> ResolveExcluded<TIdentity, TDeclaration>(
        ImmutableArray<Contribution<TIdentity, TDeclaration>> contributions)
        where TIdentity : notnull =>
        contributions.Select(contribution => new ResolvedContribution<TIdentity, TDeclaration>(
            contribution,
            ContributionDisposition.Excluded,
            contribution.IneligibilityReason ?? "The source document does not participate."))
            .ToImmutableArray();
}

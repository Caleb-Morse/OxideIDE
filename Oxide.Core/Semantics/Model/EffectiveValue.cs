using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;

namespace Oxide.Core.Semantics.Model;

public sealed record EffectiveValue<T>(
    T Value,
    SourceProvenance Provenance,
    string SelectionReason,
    ImmutableArray<SourceProvenance> IgnoredCandidates)
{
    public static EffectiveValue<T> FromSingle(SourcedValue<T> value) =>
        new(value.Value, value.Provenance, "Single unambiguous declaration", []);

    public static EffectiveValue<T> FromContribution<TIdentity, TDeclaration>(
        SourcedValue<T> value,
        ContributionResolution<TIdentity, TDeclaration> resolution)
        where TIdentity : notnull =>
        new(
            value.Value,
            value.Provenance,
            resolution.Reason.Explanation,
            resolution.Contributions
                .Where(contribution => contribution.Contribution.Id != resolution.EffectiveContribution?.Id)
                .Select(contribution => contribution.Contribution.Provenance)
                .ToImmutableArray());
}

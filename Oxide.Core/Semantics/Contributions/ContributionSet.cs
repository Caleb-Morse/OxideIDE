using System.Collections.Immutable;

namespace Oxide.Core.Semantics.Contributions;

public sealed record ContributionSet<TIdentity, TDeclaration>
    where TIdentity : notnull
{
    public ContributionSet(
        TIdentity identity,
        IEnumerable<Contribution<TIdentity, TDeclaration>> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        Identity = identity;
        Contributions = contributions
            .Select((contribution, index) => Validate(contribution, identity, index))
            .OrderBy(contribution => contribution.Provenance.Layer.Position)
            .ThenBy(contribution => contribution.Provenance.PhysicalPath, StringComparer.Ordinal)
            .ThenBy(contribution => contribution.Provenance.Span.Start)
            .ThenBy(contribution => contribution.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        var duplicateId = Contributions
            .GroupBy(contribution => contribution.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new ArgumentException($"Contribution ID '{duplicateId.Key}' is duplicated.", nameof(contributions));
        }
    }

    public TIdentity Identity { get; }

    public ImmutableArray<Contribution<TIdentity, TDeclaration>> Contributions { get; }

    private static Contribution<TIdentity, TDeclaration> Validate(
        Contribution<TIdentity, TDeclaration> contribution,
        TIdentity identity,
        int index)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        if (!EqualityComparer<TIdentity>.Default.Equals(contribution.Identity, identity))
        {
            throw new ArgumentException(
                $"Contribution at index {index} has a different semantic identity.",
                nameof(contribution));
        }

        return contribution;
    }
}

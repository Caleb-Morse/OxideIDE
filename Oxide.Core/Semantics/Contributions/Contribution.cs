using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Snapshots;

namespace Oxide.Core.Semantics.Contributions;

public sealed record Contribution<TIdentity, TDeclaration>(
    ContributionId Id,
    TIdentity Identity,
    TDeclaration Declaration,
    SourceProvenance Provenance,
    ContributionEligibility Eligibility,
    ContributionValidity Validity,
    string? IneligibilityReason,
    string? InvalidReason)
    where TIdentity : notnull
{
    public static Contribution<TIdentity, TDeclaration> FromInventory(
        TIdentity identity,
        DeclarationInventoryItem<TDeclaration> item,
        SourceProvenance provenance,
        ContributionValidity validity = ContributionValidity.Valid,
        string? invalidReason = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.IsEligible)
        {
            return Excluded(
                identity,
                item.Declaration,
                provenance,
                item.Participation.Explanation,
                validity,
                invalidReason);
        }

        return validity is ContributionValidity.Valid
            ? Valid(identity, item.Declaration, provenance)
            : Invalid(identity, item.Declaration, provenance, invalidReason ?? "The declaration is invalid.");
    }

    public static Contribution<TIdentity, TDeclaration> Valid(
        TIdentity identity,
        TDeclaration declaration,
        SourceProvenance provenance) =>
        new(
            ContributionId.Create(provenance),
            identity,
            declaration,
            provenance,
            ContributionEligibility.Eligible,
            ContributionValidity.Valid,
            null,
            null);

    public static Contribution<TIdentity, TDeclaration> Invalid(
        TIdentity identity,
        TDeclaration declaration,
        SourceProvenance provenance,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new Contribution<TIdentity, TDeclaration>(
            ContributionId.Create(provenance),
            identity,
            declaration,
            provenance,
            ContributionEligibility.Eligible,
            ContributionValidity.Invalid,
            null,
            reason.Trim());
    }

    public static Contribution<TIdentity, TDeclaration> Excluded(
        TIdentity identity,
        TDeclaration declaration,
        SourceProvenance provenance,
        string reason,
        ContributionValidity validity = ContributionValidity.Valid,
        string? invalidReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (validity is ContributionValidity.Invalid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(invalidReason);
        }

        return new Contribution<TIdentity, TDeclaration>(
            ContributionId.Create(provenance),
            identity,
            declaration,
            provenance,
            ContributionEligibility.ExcludedByDocumentParticipation,
            validity,
            reason.Trim(),
            invalidReason?.Trim());
    }
}

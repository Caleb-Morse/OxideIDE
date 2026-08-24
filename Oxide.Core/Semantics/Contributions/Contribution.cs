using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Contributions;

public sealed record Contribution<TIdentity, TDeclaration>(
    ContributionId Id,
    TIdentity Identity,
    TDeclaration Declaration,
    SourceProvenance Provenance,
    ContributionValidity Validity,
    string? InvalidReason)
    where TIdentity : notnull
{
    public static Contribution<TIdentity, TDeclaration> Valid(
        TIdentity identity,
        TDeclaration declaration,
        SourceProvenance provenance) =>
        new(ContributionId.Create(provenance), identity, declaration, provenance, ContributionValidity.Valid, null);

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
            ContributionValidity.Invalid,
            reason.Trim());
    }
}

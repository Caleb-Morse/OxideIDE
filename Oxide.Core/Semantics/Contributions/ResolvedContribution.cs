namespace Oxide.Core.Semantics.Contributions;

public sealed record ResolvedContribution<TIdentity, TDeclaration>(
    Contribution<TIdentity, TDeclaration> Contribution,
    ContributionDisposition Disposition,
    string Explanation)
    where TIdentity : notnull;

using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record LocalisationEntry(
    LocalisationIdentity Identity,
    ImmutableArray<LocalisationDeclaration> Contributions)
{
    public bool IsAmbiguous => Contributions.Length > 1;
}

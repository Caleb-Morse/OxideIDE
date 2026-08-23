using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Declarations;

public sealed record LocalisationDeclaration(
    LocalisationLanguage Language,
    LocalisationKey Key,
    int? Version,
    SourcedValue<string> Value,
    SourceProvenance Provenance)
{
    public LocalisationIdentity Identity => new(Language, Key);
}

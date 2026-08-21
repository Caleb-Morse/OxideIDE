using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Declarations;

public sealed record CountryTagDeclaration(
    DocumentId DocumentId,
    string OriginalTag,
    string NormalizedTag,
    SourcedValue<string> DefinitionPath,
    SourceProvenance Provenance)
{
    public EntityId EntityId => Oxide.Core.Semantics.Identity.EntityId.Country(NormalizedTag);
}

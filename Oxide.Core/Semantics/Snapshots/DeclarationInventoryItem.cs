using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Snapshots;

public sealed record DeclarationInventoryItem<TDeclaration>(
    TDeclaration Declaration,
    SourceIdentity Source,
    DocumentParticipation Participation)
{
    public bool IsEligible => Participation.Participates;
}

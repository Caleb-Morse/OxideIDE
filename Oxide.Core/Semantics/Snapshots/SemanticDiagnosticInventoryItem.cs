using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Snapshots;

public sealed record SemanticDiagnosticInventoryItem(
    SemanticDiagnostic Diagnostic,
    SourceIdentity Source,
    DocumentParticipation Participation)
{
    public bool IsActive => Participation.Participates;
}

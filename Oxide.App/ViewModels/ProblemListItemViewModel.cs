using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Workspaces.Loading;
using Oxide.Syntax.Diagnostics;

namespace Oxide.App.ViewModels;

public sealed record ProblemListItemViewModel(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string Location,
    int? StateId)
{
    public string SeverityText => Severity.ToString();

    public static ProblemListItemViewModel FromWorkspace(WorkspaceDiagnostic diagnostic) => new(
        diagnostic.Code,
        diagnostic.Severity,
        diagnostic.Message,
        diagnostic.PhysicalPath ?? "Workspace",
        null);

    public static ProblemListItemViewModel FromSemantic(SemanticDiagnostic diagnostic)
    {
        int? stateId = null;
        if (diagnostic.EntityId is { Kind: EntityKind.State } entityId
            && int.TryParse(entityId.LocalKey, out var parsedId))
        {
            stateId = parsedId;
        }

        return new ProblemListItemViewModel(
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Message,
            diagnostic.Provenance?.PhysicalPath ?? diagnostic.EntityId?.ToString() ?? "Semantic model",
            stateId);
    }
}

using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Loading;

public sealed record WorkspaceDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? PhysicalPath = null,
    DocumentId? DocumentId = null,
    TextSpan? Span = null);

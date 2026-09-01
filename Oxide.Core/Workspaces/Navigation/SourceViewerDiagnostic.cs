using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    TextSpan Span,
    int Line,
    int Column,
    SourceNavigationTarget NavigationTarget);

using Oxide.Syntax.Text;

namespace Oxide.Syntax.Diagnostics;

public sealed record SyntaxDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    TextSpan Span);

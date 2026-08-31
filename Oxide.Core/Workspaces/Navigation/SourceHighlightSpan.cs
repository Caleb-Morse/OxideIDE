using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceHighlightSpan(TextSpan Span, SourceHighlightKind Kind);

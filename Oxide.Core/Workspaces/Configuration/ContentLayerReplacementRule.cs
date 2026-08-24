using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Configuration;

public sealed record ContentLayerReplacementRule(
    VirtualPath Path,
    string? DescriptorPath = null,
    TextSpan? Span = null);

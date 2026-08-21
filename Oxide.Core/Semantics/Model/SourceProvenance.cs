using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Core.Semantics.Model;

public sealed record SourceProvenance(
    DocumentId DocumentId,
    string PhysicalPath,
    ContentLayer Layer,
    TextSpan Span);

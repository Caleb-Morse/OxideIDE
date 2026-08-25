using Oxide.Core.Workspaces.Documents;

namespace Oxide.App.ViewModels;

public sealed record SourceNavigationRequest(
    DocumentId DocumentId,
    string PhysicalPath,
    string VirtualPath,
    string LayerId,
    string LayerName,
    int SpanStart,
    int SpanLength,
    string SemanticIdentity,
    string Location);

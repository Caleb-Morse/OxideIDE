using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Core.Workspaces.Documents;

public sealed record SourceIdentity(
    DocumentId DocumentId,
    ContentLayerId LayerId,
    VirtualPath VirtualPath,
    string PhysicalPath);

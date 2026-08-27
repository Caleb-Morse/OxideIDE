using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceNavigationTarget
{
    public SourceNavigationTarget(
        long snapshotVersion,
        DocumentId documentId,
        ContentLayerId layerId,
        VirtualPath virtualPath,
        TextSpan span,
        string semanticIdentity,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(snapshotVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        SnapshotVersion = snapshotVersion;
        DocumentId = documentId;
        LayerId = layerId;
        VirtualPath = virtualPath;
        Span = span;
        SemanticIdentity = semanticIdentity.Trim();
        Reason = reason.Trim();
    }

    public long SnapshotVersion { get; }

    public DocumentId DocumentId { get; }

    public ContentLayerId LayerId { get; }

    public VirtualPath VirtualPath { get; }

    public TextSpan Span { get; }

    public string SemanticIdentity { get; }

    public string Reason { get; }
}

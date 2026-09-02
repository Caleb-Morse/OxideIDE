using System.Collections.Immutable;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Editing;

public readonly record struct WorkspaceEditId
{
    public WorkspaceEditId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("A workspace edit ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public static WorkspaceEditId Create() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record DocumentEditTarget
{
    public DocumentEditTarget(
        long snapshotVersion,
        DocumentId documentId,
        ContentLayerId layerId,
        VirtualPath virtualPath,
        string physicalPath,
        DocumentContentFingerprint expectedFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(snapshotVersion);
        if (string.IsNullOrWhiteSpace(documentId.Value)) throw new ArgumentException("A document ID is required.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(layerId.Value)) throw new ArgumentException("A layer ID is required.", nameof(layerId));
        if (string.IsNullOrWhiteSpace(virtualPath.Value)) throw new ArgumentException("A virtual path is required.", nameof(virtualPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalPath);
        if (string.IsNullOrWhiteSpace(expectedFingerprint.Value))
        {
            throw new ArgumentException("An expected document fingerprint is required.", nameof(expectedFingerprint));
        }
        SnapshotVersion = snapshotVersion;
        DocumentId = documentId;
        LayerId = layerId;
        VirtualPath = virtualPath;
        PhysicalPath = Path.GetFullPath(physicalPath);
        ExpectedFingerprint = expectedFingerprint;
    }

    public long SnapshotVersion { get; }
    public DocumentId DocumentId { get; }
    public ContentLayerId LayerId { get; }
    public VirtualPath VirtualPath { get; }
    public string PhysicalPath { get; }
    public DocumentContentFingerprint ExpectedFingerprint { get; }
}

public sealed record DocumentEdit
{
    public DocumentEdit(DocumentEditTarget target, IEnumerable<TextChange> changes)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(changes);
        var normalized = changes.OrderBy(change => change.Span.Start).ToImmutableArray();
        if (normalized.IsEmpty)
        {
            throw new ArgumentException("A document edit requires at least one text change.", nameof(changes));
        }

        for (var index = 1; index < normalized.Length; index++)
        {
            if (normalized[index - 1].Span.End > normalized[index].Span.Start)
            {
                throw new ArgumentException("Text changes within one document cannot overlap.", nameof(changes));
            }

            if (normalized[index - 1].Span.Start == normalized[index].Span.Start)
            {
                throw new ArgumentException(
                    "Text changes within one document cannot share a starting position.",
                    nameof(changes));
            }
        }

        Target = target;
        Changes = normalized;
    }

    public DocumentEditTarget Target { get; }
    public ImmutableArray<TextChange> Changes { get; }
}

public sealed record WorkspaceEdit
{
    public WorkspaceEdit(
        WorkspaceEditId id,
        long snapshotVersion,
        string description,
        IEnumerable<DocumentEdit> documents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(snapshotVersion);
        if (id.Value == Guid.Empty) throw new ArgumentException("A workspace edit ID is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(documents);
        var normalized = documents.OrderBy(document => document.Target.DocumentId.Value, StringComparer.Ordinal).ToImmutableArray();
        if (normalized.IsEmpty)
        {
            throw new ArgumentException("A workspace edit requires at least one document edit.", nameof(documents));
        }

        if (normalized.Any(document => document.Target.SnapshotVersion != snapshotVersion))
        {
            throw new ArgumentException("Every document edit must target the workspace edit's snapshot version.", nameof(documents));
        }

        if (normalized.Select(document => document.Target.DocumentId).Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException("A workspace edit can contain only one edit per document.", nameof(documents));
        }

        Id = id;
        SnapshotVersion = snapshotVersion;
        Description = description.Trim();
        Documents = normalized;
    }

    public WorkspaceEditId Id { get; }
    public long SnapshotVersion { get; }
    public string Description { get; }
    public ImmutableArray<DocumentEdit> Documents { get; }
}

using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Editing;

public static class EditCapabilityEvaluator
{
    public static EditCapability AssessDocument(
        WorkspaceSnapshot snapshot,
        long expectedSnapshotVersion,
        DocumentId documentId,
        bool hasExactProvenance,
        bool isDeclarationUnambiguous,
        bool operationSupported)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (expectedSnapshotVersion != snapshot.Version)
        {
            return EditCapability.Refused(
                EditRefusalReason.StaleSnapshot,
                $"The edit targets snapshot {expectedSnapshotVersion}, but snapshot {snapshot.Version} is current.");
        }

        if (!snapshot.DocumentsById.TryGetValue(documentId, out var document) || !document.IsLoaded || document.Text is null)
        {
            return EditCapability.Refused(EditRefusalReason.FailedDocument, "The source document is not loaded successfully.");
        }

        if (!hasExactProvenance)
        {
            return EditCapability.Refused(EditRefusalReason.MissingProvenance, "The value has no exact source span.");
        }

        if (!isDeclarationUnambiguous)
        {
            return EditCapability.Refused(
                EditRefusalReason.AmbiguousDeclaration,
                "The semantic declaration is ambiguous, so Oxide cannot choose a source safely.");
        }

        if (!operationSupported)
        {
            return EditCapability.Refused(EditRefusalReason.UnsupportedOperation, "This edit operation is not supported.");
        }

        if (!document.Participates)
        {
            return EditCapability.Refused(
                EditRefusalReason.UnsupportedOperation,
                "The source document does not participate in the effective workspace.");
        }

        if (!document.Layer.IsWritable)
        {
            return EditCapability.Refused(
                EditRefusalReason.ReadOnlyLayer,
                $"The {document.Layer.DisplayName} layer is read-only.");
        }

        if (document.Text.Encoding is not (SourceEncoding.Utf8 or SourceEncoding.Utf8WithBom))
        {
            return EditCapability.Refused(
                EditRefusalReason.UnsupportedEncoding,
                $"The {document.Text.Encoding} encoding is not safely editable.");
        }

        if (document.Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error))
        {
            return EditCapability.Refused(
                EditRefusalReason.MalformedSource,
                "The source document has error diagnostics and remains read-only.");
        }

        return EditCapability.Editable(
            $"The effective declaration has exact provenance in writable layer {document.Layer.DisplayName}.");
    }

    public static DocumentEditTarget CreateTarget(WorkspaceSnapshot snapshot, DocumentId documentId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.DocumentsById.TryGetValue(documentId, out var document) || document.Text is null)
        {
            throw new ArgumentException("A loaded snapshot document with exact source text is required.", nameof(documentId));
        }

        return new DocumentEditTarget(
            snapshot.Version,
            document.Id,
            document.Layer.Id,
            document.VirtualPath,
            document.PhysicalPath,
            DocumentContentFingerprint.Create(document.Text.GetOriginalBytes().Span));
    }
}

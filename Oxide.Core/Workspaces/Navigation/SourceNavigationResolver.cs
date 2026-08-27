using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Workspaces.Navigation;

public static class SourceNavigationResolver
{
    public static SourceNavigationResolution Resolve(
        WorkspaceSnapshot snapshot,
        SourceNavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(target);

        if (target.SnapshotVersion != snapshot.Version)
        {
            return Unresolved(
                SourceNavigationStatus.SnapshotVersionMismatch,
                target,
                $"The source request targets snapshot {target.SnapshotVersion}, but snapshot {snapshot.Version} is current.");
        }

        if (!snapshot.DocumentsById.TryGetValue(target.DocumentId, out var document))
        {
            return Unresolved(
                SourceNavigationStatus.DocumentNotFound,
                target,
                "The requested document is not present in this snapshot.");
        }

        if (document.Layer.Id != target.LayerId || document.VirtualPath != target.VirtualPath)
        {
            return Unresolved(
                SourceNavigationStatus.SourceIdentityMismatch,
                target,
                "The requested layer or virtual path does not match the snapshot document.",
                document);
        }

        if (!document.IsLoaded)
        {
            return Unresolved(
                SourceNavigationStatus.DocumentFailed,
                target,
                "The source document failed to load and has no text to display.",
                document);
        }

        if (document.Text is null)
        {
            return Unresolved(
                SourceNavigationStatus.TextUnavailable,
                target,
                "The source document has no snapshot-backed text.",
                document);
        }

        if (document.Kind is not (SourceDocumentKind.Clausewitz or SourceDocumentKind.Localisation))
        {
            return Unresolved(
                SourceNavigationStatus.UnsupportedDocument,
                target,
                $"Source viewing is not supported for {document.Kind} documents.",
                document);
        }

        int spanEnd;
        try
        {
            spanEnd = target.Span.End;
        }
        catch (OverflowException)
        {
            return Unresolved(
                SourceNavigationStatus.InvalidSpan,
                target,
                "The requested source span exceeds the supported range.",
                document);
        }

        if (spanEnd > document.Text.Length)
        {
            return Unresolved(
                SourceNavigationStatus.InvalidSpan,
                target,
                $"The requested source span ends at {spanEnd}, beyond the document length of {document.Text.Length}.",
                document);
        }

        var location = new SourceViewerLocation(
            snapshot.Version,
            document.Id,
            document.PhysicalPath,
            document.VirtualPath,
            document.Layer,
            document.Kind,
            document.LoadStatus,
            document.Participation,
            target.Span,
            document.Text.GetPosition(target.Span.Start),
            document.Text.GetPosition(spanEnd),
            target.SemanticIdentity,
            target.Reason);
        return new SourceNavigationResolution(
            SourceNavigationStatus.Resolved,
            target,
            $"Resolved to {document.VirtualPath.Value} at line {location.StartLine}, column {location.StartColumn}.",
            document,
            location);
    }

    private static SourceNavigationResolution Unresolved(
        SourceNavigationStatus status,
        SourceNavigationTarget target,
        string message,
        SourceDocument? document = null) =>
        new(status, target, message, document);
}

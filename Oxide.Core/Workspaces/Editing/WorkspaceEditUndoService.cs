using System.Collections.Immutable;
using System.Text;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Editing;

public sealed class WorkspaceEditUndoService
{
    private readonly WorkspaceEditWriter writer;

    public WorkspaceEditUndoService(WorkspaceEditWriter? writer = null)
    {
        this.writer = writer ?? new WorkspaceEditWriter();
    }

    public async Task<WorkspaceEditUndoResult> RestoreAsync(
        WorkspaceSnapshot snapshot,
        WorkspaceEditUndoRecord undoRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(undoRecord);
        if (cancellationToken.IsCancellationRequested)
        {
            return new WorkspaceEditUndoResult(WorkspaceEditUndoStatus.Cancelled, "Undo was cancelled.");
        }

        var edits = ImmutableArray.CreateBuilder<DocumentEdit>(undoRecord.Documents.Length);
        foreach (var entry in undoRecord.Documents)
        {
            if (!snapshot.DocumentsById.TryGetValue(entry.Target.DocumentId, out var document) || document.Text is null)
            {
                return Conflict($"'{entry.Target.VirtualPath}' is not loaded in the current snapshot.");
            }

            if (document.Layer.Id != entry.Target.LayerId ||
                document.VirtualPath != entry.Target.VirtualPath ||
                !string.Equals(document.PhysicalPath, entry.Target.PhysicalPath, StringComparison.Ordinal))
            {
                return Conflict($"'{entry.Target.VirtualPath}' no longer identifies the source that was edited.");
            }

            var currentFingerprint = DocumentContentFingerprint.Create(document.Text.GetOriginalBytes().Span);
            if (currentFingerprint != entry.AppliedFingerprint)
            {
                return Conflict($"'{entry.Target.VirtualPath}' changed after the edit and cannot be undone safely.");
            }

            SourceText originalSource;
            try
            {
                originalSource = SourceText.FromBytes(entry.OriginalBytes.AsSpan());
            }
            catch (Exception exception) when (exception is DecoderFallbackException or ArgumentException)
            {
                return new WorkspaceEditUndoResult(
                    WorkspaceEditUndoStatus.Failed,
                    "The retained original bytes could not be decoded safely.",
                    [Error("OXIDE5025", exception.Message)]);
            }

            edits.Add(new DocumentEdit(
                EditCapabilityEvaluator.CreateTarget(snapshot, document.Id),
                [new TextChange(new TextSpan(0, document.Text.Length), originalSource.Text)]));
        }

        var edit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            $"Undo workspace edit {undoRecord.EditId}",
            edits);
        var application = await writer.ApplyAsync(snapshot, edit, cancellationToken).ConfigureAwait(false);
        var status = application.Status switch
        {
            WorkspaceEditApplicationStatus.Applied => WorkspaceEditUndoStatus.Restored,
            WorkspaceEditApplicationStatus.Conflict => WorkspaceEditUndoStatus.Conflict,
            WorkspaceEditApplicationStatus.Cancelled => WorkspaceEditUndoStatus.Cancelled,
            WorkspaceEditApplicationStatus.Rejected => WorkspaceEditUndoStatus.Rejected,
            _ => WorkspaceEditUndoStatus.Failed,
        };
        return new WorkspaceEditUndoResult(
            status,
            application.IsApplied ? "Restored the exact source bytes from before the edit." : application.Message,
            application.Issues,
            application.RecoveryArtifacts);
    }

    private static WorkspaceEditUndoResult Conflict(string message) => new(
        WorkspaceEditUndoStatus.Conflict,
        message,
        [Error("OXIDE5024", message)]);

    private static EditValidationIssue Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}

using System.Collections.Immutable;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Workspaces.Editing;

public sealed record EditValidationIssue
{
    public EditValidationIssue(string code, DiagnosticSeverity severity, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code.Trim();
        Severity = severity;
        Message = message.Trim();
    }

    public string Code { get; }
    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
}

public sealed record DocumentEditPreview(
    DocumentEditTarget Target,
    string OriginalText,
    string UpdatedText,
    ImmutableArray<TextChange> Changes,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);
}

public sealed record WorkspaceEditPreview(
    WorkspaceEdit Edit,
    ImmutableArray<DocumentEditPreview> Documents,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsValid =>
        Documents.Length == Edit.Documents.Length &&
        Documents.All(document => document.IsValid) &&
        Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);
}

public enum WorkspaceEditApplicationStatus
{
    Applied,
    Rejected,
    Conflict,
    Failed,
    Cancelled,
}

public sealed record DocumentUndoEntry
{
    public DocumentUndoEntry(
        DocumentEditTarget target,
        ImmutableArray<byte> originalBytes,
        DocumentContentFingerprint appliedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (originalBytes.IsDefault) throw new ArgumentException("Original bytes must be initialized.", nameof(originalBytes));
        if (string.IsNullOrWhiteSpace(appliedFingerprint.Value))
        {
            throw new ArgumentException("An applied-content fingerprint is required.", nameof(appliedFingerprint));
        }

        Target = target;
        OriginalBytes = originalBytes;
        AppliedFingerprint = appliedFingerprint;
    }

    public DocumentEditTarget Target { get; }
    public ImmutableArray<byte> OriginalBytes { get; }
    public DocumentContentFingerprint AppliedFingerprint { get; }
}

public sealed record WorkspaceEditUndoRecord
{
    public WorkspaceEditUndoRecord(WorkspaceEditId editId, ImmutableArray<DocumentUndoEntry> documents)
    {
        if (editId.Value == Guid.Empty) throw new ArgumentException("An edit ID is required.", nameof(editId));
        if (documents.IsDefaultOrEmpty) throw new ArgumentException("At least one undo entry is required.", nameof(documents));
        EditId = editId;
        Documents = documents;
    }

    public WorkspaceEditId EditId { get; }
    public ImmutableArray<DocumentUndoEntry> Documents { get; }
}

public sealed record WorkspaceEditApplicationResult
{
    public WorkspaceEditApplicationResult(
        WorkspaceEditApplicationStatus status,
        string message,
        WorkspaceEditUndoRecord? undoRecord = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (status is WorkspaceEditApplicationStatus.Applied && undoRecord is null)
        {
            throw new ArgumentException("An applied workspace edit requires an undo record.", nameof(undoRecord));
        }

        Status = status;
        Message = message.Trim();
        UndoRecord = undoRecord;
    }

    public WorkspaceEditApplicationStatus Status { get; }
    public string Message { get; }
    public WorkspaceEditUndoRecord? UndoRecord { get; }
    public bool IsApplied => Status is WorkspaceEditApplicationStatus.Applied;
}

public enum WorkspaceEditUndoStatus
{
    Restored,
    Rejected,
    Conflict,
    Failed,
    Cancelled,
}

public sealed record WorkspaceEditUndoResult
{
    public WorkspaceEditUndoResult(WorkspaceEditUndoStatus status, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Status = status;
        Message = message.Trim();
    }

    public WorkspaceEditUndoStatus Status { get; }
    public string Message { get; }
    public bool IsRestored => Status is WorkspaceEditUndoStatus.Restored;
}

using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Workspaces.Editing;

public enum WorkspaceEditPreflightStatus
{
    Ready,
    Rejected,
    Conflict,
    Failed,
    Cancelled,
}

public sealed record WorkspaceEditPreflightResult(
    WorkspaceEditPreflightStatus Status,
    PreparedWorkspaceEdit? PreparedEdit,
    ImmutableDictionary<DocumentId, DocumentContentFingerprint> LiveFingerprints,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsReady =>
        Status is WorkspaceEditPreflightStatus.Ready &&
        PreparedEdit is { IsValid: true } prepared &&
        LiveFingerprints.Count == prepared.Documents.Length &&
        Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);
}

public static class WorkspaceEditPreflightValidator
{
    public static async Task<WorkspaceEditPreflightResult> ValidateAsync(
        WorkspaceSnapshot snapshot,
        WorkspaceEdit edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(edit);
        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        PreparedWorkspaceEdit prepared;
        try
        {
            prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Cancelled();
        }

        if (!prepared.IsValid)
        {
            var issues = prepared.Issues
                .Concat(prepared.Documents.SelectMany(document => document.Issues))
                .ToImmutableArray();
            return new WorkspaceEditPreflightResult(
                WorkspaceEditPreflightStatus.Rejected,
                prepared,
                ImmutableDictionary<DocumentId, DocumentContentFingerprint>.Empty,
                issues);
        }

        var fingerprints = ImmutableDictionary.CreateBuilder<DocumentId, DocumentContentFingerprint>();
        var issuesBuilder = ImmutableArray.CreateBuilder<EditValidationIssue>();
        var status = WorkspaceEditPreflightStatus.Ready;
        foreach (var document in prepared.Documents)
        {
            try
            {
                var liveBytes = await File.ReadAllBytesAsync(
                    document.Edit.Target.PhysicalPath,
                    cancellationToken).ConfigureAwait(false);
                var liveFingerprint = DocumentContentFingerprint.Create(liveBytes);
                fingerprints.Add(document.Edit.Target.DocumentId, liveFingerprint);
                if (liveFingerprint != document.Edit.Target.ExpectedFingerprint)
                {
                    status = Combine(status, WorkspaceEditPreflightStatus.Conflict);
                    issuesBuilder.Add(Error(
                        "OXIDE5015",
                        $"'{document.Edit.Target.VirtualPath}' changed after snapshot {edit.SnapshotVersion}; no files may be written."));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Cancelled();
            }
            catch (FileNotFoundException)
            {
                status = Combine(status, WorkspaceEditPreflightStatus.Conflict);
                issuesBuilder.Add(Error(
                    "OXIDE5016",
                    $"'{document.Edit.Target.VirtualPath}' was deleted after snapshot {edit.SnapshotVersion}; no files may be written."));
            }
            catch (DirectoryNotFoundException)
            {
                status = Combine(status, WorkspaceEditPreflightStatus.Conflict);
                issuesBuilder.Add(Error(
                    "OXIDE5016",
                    $"The directory containing '{document.Edit.Target.VirtualPath}' no longer exists; no files may be written."));
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                status = Combine(status, WorkspaceEditPreflightStatus.Failed);
                issuesBuilder.Add(Error(
                    "OXIDE5017",
                    $"Could not revalidate '{document.Edit.Target.VirtualPath}': {exception.Message}"));
            }
        }

        return new WorkspaceEditPreflightResult(
            status,
            prepared,
            fingerprints.ToImmutable(),
            issuesBuilder.ToImmutable());
    }

    private static bool IsFileSystemException(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        System.Security.SecurityException or
        NotSupportedException;

    private static WorkspaceEditPreflightStatus Combine(
        WorkspaceEditPreflightStatus current,
        WorkspaceEditPreflightStatus next)
    {
        if (current is WorkspaceEditPreflightStatus.Failed || next is WorkspaceEditPreflightStatus.Failed)
        {
            return WorkspaceEditPreflightStatus.Failed;
        }

        return current is WorkspaceEditPreflightStatus.Conflict || next is WorkspaceEditPreflightStatus.Conflict
            ? WorkspaceEditPreflightStatus.Conflict
            : next;
    }

    private static WorkspaceEditPreflightResult Cancelled() => new(
        WorkspaceEditPreflightStatus.Cancelled,
        null,
        ImmutableDictionary<DocumentId, DocumentContentFingerprint>.Empty,
        [new EditValidationIssue("OXIDE5018", DiagnosticSeverity.Warning, "Pre-write validation was cancelled.")]);

    private static EditValidationIssue Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}

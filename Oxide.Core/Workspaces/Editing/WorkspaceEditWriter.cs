using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Workspaces.Editing;

public sealed class WorkspaceEditWriter
{
    private readonly IWorkspaceEditFileSystem fileSystem;

    public WorkspaceEditWriter()
        : this(new PhysicalWorkspaceEditFileSystem())
    {
    }

    internal WorkspaceEditWriter(IWorkspaceEditFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        this.fileSystem = fileSystem;
    }

    public async Task<WorkspaceEditApplicationResult> ApplyAsync(
        WorkspaceSnapshot snapshot,
        WorkspaceEdit edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(edit);

        var preflight = await WorkspaceEditPreflightValidator.ValidateAsync(
            snapshot,
            edit,
            cancellationToken).ConfigureAwait(false);
        if (!preflight.IsReady)
        {
            return FromPreflight(preflight);
        }

        var staged = ImmutableArray.CreateBuilder<StagedDocument>(preflight.PreparedEdit!.Documents.Length);
        try
        {
            foreach (var document in preflight.PreparedEdit.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = document.Edit.Target;
                var stagedPath = ArtifactPath(target.PhysicalPath, "stage", edit.Id);
                var backupPath = ArtifactPath(target.PhysicalPath, "backup", edit.Id);
                var updatedBytes = document.UpdatedSource.GetOriginalBytes().ToArray();
                await fileSystem.WriteStagedAsync(
                    stagedPath,
                    updatedBytes,
                    target.PhysicalPath,
                    cancellationToken).ConfigureAwait(false);
                staged.Add(new StagedDocument(document, stagedPath, backupPath, updatedBytes));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Cleanup(staged, deleteBackups: false);
            return Result(WorkspaceEditApplicationStatus.Cancelled, "The edit was cancelled before replacement.");
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            Cleanup(staged, deleteBackups: false);
            return Result(
                WorkspaceEditApplicationStatus.Failed,
                "Oxide could not stage every edited document.",
                Error("OXIDE5019", exception.Message));
        }

        var secondCheck = await WorkspaceEditPreflightValidator.ValidateAsync(
            snapshot,
            edit,
            cancellationToken).ConfigureAwait(false);
        if (!secondCheck.IsReady)
        {
            Cleanup(staged, deleteBackups: false);
            return FromPreflight(secondCheck);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            Cleanup(staged, deleteBackups: false);
            return Result(WorkspaceEditApplicationStatus.Cancelled, "The edit was cancelled before replacement.");
        }

        try
        {
            foreach (var document in staged)
            {
                var liveBytes = await fileSystem.ReadAllBytesAsync(
                    document.Prepared.Edit.Target.PhysicalPath,
                    CancellationToken.None).ConfigureAwait(false);
                var liveFingerprint = DocumentContentFingerprint.Create(liveBytes);
                if (liveFingerprint != document.Prepared.Edit.Target.ExpectedFingerprint)
                {
                    throw new LiveConflictException(document.Prepared.Edit.Target.VirtualPath);
                }

                fileSystem.Replace(document.StagedPath, document.Prepared.Edit.Target.PhysicalPath, document.BackupPath);
            }
        }
        catch (Exception exception) when (exception is LiveConflictException || IsFileSystemException(exception))
        {
            var rollbackFailures = RollBack(staged);
            var cleanupIssues = Cleanup(staged, deleteBackups: rollbackFailures.IsEmpty);
            var recoveryPaths = ExistingArtifacts(staged);
            var status = exception is LiveConflictException
                ? WorkspaceEditApplicationStatus.Conflict
                : WorkspaceEditApplicationStatus.Failed;
            var message = rollbackFailures.IsEmpty
                ? "The edit was not applied; any replaced documents were restored."
                : "The edit failed and automatic rollback was incomplete. Recovery backups were retained.";
            var issues = ImmutableArray.CreateBuilder<EditValidationIssue>();
            issues.Add(Error(exception is LiveConflictException ? "OXIDE5020" : "OXIDE5021", exception.Message));
            issues.AddRange(rollbackFailures);
            issues.AddRange(cleanupIssues);
            return new WorkspaceEditApplicationResult(status, message, null, issues.ToImmutable(), recoveryPaths);
        }

        var cleanupWarnings = Cleanup(staged, deleteBackups: true);
        var cleanupArtifacts = ExistingArtifacts(staged);
        var undo = new WorkspaceEditUndoRecord(
            edit.Id,
            staged.Select(document => new DocumentUndoEntry(
                document.Prepared.Edit.Target,
                ImmutableArray.Create(document.Prepared.OriginalSource.GetOriginalBytes().ToArray()),
                DocumentContentFingerprint.Create(document.UpdatedBytes))).ToImmutableArray());
        return new WorkspaceEditApplicationResult(
            WorkspaceEditApplicationStatus.Applied,
            $"Applied {staged.Count} document edit{(staged.Count == 1 ? string.Empty : "s")}.",
            undo,
            cleanupWarnings,
            cleanupArtifacts);
    }

    private ImmutableArray<EditValidationIssue> RollBack(IEnumerable<StagedDocument> staged)
    {
        var issues = ImmutableArray.CreateBuilder<EditValidationIssue>();
        foreach (var document in staged.Reverse())
        {
            if (!fileSystem.Exists(document.BackupPath))
            {
                continue;
            }

            try
            {
                fileSystem.Restore(document.BackupPath, document.Prepared.Edit.Target.PhysicalPath);
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                issues.Add(Error(
                    "OXIDE5022",
                    $"Could not restore '{document.Prepared.Edit.Target.VirtualPath}': {exception.Message}"));
            }
        }

        return issues.ToImmutable();
    }

    private ImmutableArray<EditValidationIssue> Cleanup(
        IEnumerable<StagedDocument> staged,
        bool deleteBackups)
    {
        var issues = ImmutableArray.CreateBuilder<EditValidationIssue>();
        foreach (var document in staged)
        {
            TryDelete(document.StagedPath, "staged", issues);
            if (deleteBackups)
            {
                TryDelete(document.BackupPath, "backup", issues);
            }
        }

        return issues.ToImmutable();
    }

    private void TryDelete(
        string path,
        string kind,
        ImmutableArray<EditValidationIssue>.Builder issues)
    {
        try
        {
            fileSystem.DeleteIfExists(path);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            issues.Add(new EditValidationIssue(
                "OXIDE5023",
                DiagnosticSeverity.Warning,
                $"Could not remove {kind} artifact '{path}': {exception.Message}"));
        }
    }

    private ImmutableArray<string> ExistingArtifacts(IEnumerable<StagedDocument> staged) =>
        staged.SelectMany(document => new[] { document.StagedPath, document.BackupPath })
            .Where(fileSystem.Exists)
            .ToImmutableArray();

    private static WorkspaceEditApplicationResult FromPreflight(WorkspaceEditPreflightResult preflight)
    {
        var status = preflight.Status switch
        {
            WorkspaceEditPreflightStatus.Conflict => WorkspaceEditApplicationStatus.Conflict,
            WorkspaceEditPreflightStatus.Failed => WorkspaceEditApplicationStatus.Failed,
            WorkspaceEditPreflightStatus.Cancelled => WorkspaceEditApplicationStatus.Cancelled,
            _ => WorkspaceEditApplicationStatus.Rejected,
        };
        return new WorkspaceEditApplicationResult(
            status,
            "The edit did not pass pre-write validation.",
            null,
            preflight.Issues);
    }

    private static WorkspaceEditApplicationResult Result(
        WorkspaceEditApplicationStatus status,
        string message,
        params EditValidationIssue[] issues) =>
        new(status, message, null, issues.ToImmutableArray());

    private static string ArtifactPath(string targetPath, string kind, WorkspaceEditId editId)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var fileName = Path.GetFileName(targetPath);
        return Path.Combine(directory, $".{fileName}.oxide-{kind}-{editId.Value:N}");
    }

    private static bool IsFileSystemException(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        System.Security.SecurityException or
        NotSupportedException;

    private static EditValidationIssue Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);

    private sealed record StagedDocument(
        PreparedDocumentEdit Prepared,
        string StagedPath,
        string BackupPath,
        byte[] UpdatedBytes);

    private sealed class LiveConflictException(VirtualPath path)
        : IOException($"'{path}' changed immediately before replacement.");
}

internal interface IWorkspaceEditFileSystem
{
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
    Task WriteStagedAsync(string path, byte[] bytes, string sourcePath, CancellationToken cancellationToken);
    void Replace(string stagedPath, string targetPath, string backupPath);
    void Restore(string backupPath, string targetPath);
    bool Exists(string path);
    void DeleteIfExists(string path);
}

internal sealed class PhysicalWorkspaceEditFileSystem : IWorkspaceEditFileSystem
{
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(path, cancellationToken);

    public async Task WriteStagedAsync(
        string path,
        byte[] bytes,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        await using (var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, File.GetUnixFileMode(sourcePath));
        }
    }

    public void Replace(string stagedPath, string targetPath, string backupPath) =>
        File.Replace(stagedPath, targetPath, backupPath, ignoreMetadataErrors: false);

    public void Restore(string backupPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(backupPath, targetPath, null, ignoreMetadataErrors: false);
        }
        else
        {
            File.Move(backupPath, targetPath);
        }
    }

    public bool Exists(string path) => File.Exists(path);

    public void DeleteIfExists(string path) => File.Delete(path);
}

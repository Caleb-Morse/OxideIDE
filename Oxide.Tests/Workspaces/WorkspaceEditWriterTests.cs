using System.Text;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceEditWriterTests
{
    [Fact]
    public async Task Applies_a_staged_atomic_replacement_and_returns_exact_undo_bytes()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }\r\n";
        var originalBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        File.WriteAllBytes(path, originalBytes);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 25));

        var result = await new WorkspaceEditWriter().ApplyAsync(snapshot, plan.Edit!);

        Assert.True(result.IsApplied);
        Assert.Equal("state = { id = 1 manpower = 25 state_category = rural }\r\n", ReadUtf8(path));
        var undo = Assert.Single(result.UndoRecord!.Documents);
        Assert.Equal(originalBytes, undo.OriginalBytes.ToArray());
        Assert.Equal(DocumentContentFingerprint.Create(File.ReadAllBytes(path)), undo.AppliedFingerprint);
        Assert.Empty(Artifacts(path));
    }

    [Fact]
    public async Task Live_conflict_and_cancellation_never_create_or_replace_files()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }";
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 25));
        File.AppendAllText(path, " # external");

        var conflict = await new WorkspaceEditWriter().ApplyAsync(snapshot, plan.Edit!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await new WorkspaceEditWriter().ApplyAsync(snapshot, plan.Edit!, cancellation.Token);

        Assert.Equal(WorkspaceEditApplicationStatus.Conflict, conflict.Status);
        Assert.Equal(WorkspaceEditApplicationStatus.Cancelled, cancelled.Status);
        Assert.Equal(source + " # external", File.ReadAllText(path));
        Assert.Empty(Artifacts(path));
    }

    [Fact]
    public async Task Later_replacement_failure_rolls_back_every_already_replaced_document()
    {
        using var fixture = new TemporaryWorkspace();
        const string firstSource = "state = { id = 1 manpower = 10 state_category = rural }";
        const string secondSource = "state = { id = 2 manpower = 20 state_category = town }";
        var firstPath = fixture.WriteModFile("history/states/1-One.txt", firstSource);
        var secondPath = fixture.WriteModFile("history/states/2-Two.txt", secondSource);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var edit = CombinedEdit(snapshot);
        var fileSystem = new FaultingFileSystem(failReplaceCall: 2, failRestore: false);

        var result = await new WorkspaceEditWriter(fileSystem).ApplyAsync(snapshot, edit);

        Assert.Equal(WorkspaceEditApplicationStatus.Failed, result.Status);
        Assert.Equal(firstSource, File.ReadAllText(firstPath));
        Assert.Equal(secondSource, File.ReadAllText(secondPath));
        Assert.Empty(result.RecoveryArtifacts);
        Assert.Empty(Artifacts(firstPath));
        Assert.Empty(Artifacts(secondPath));
    }

    [Fact]
    public async Task Incomplete_rollback_retains_and_reports_recovery_backup()
    {
        using var fixture = new TemporaryWorkspace();
        const string firstSource = "state = { id = 1 manpower = 10 state_category = rural }";
        const string secondSource = "state = { id = 2 manpower = 20 state_category = town }";
        var firstPath = fixture.WriteModFile("history/states/1-One.txt", firstSource);
        fixture.WriteModFile("history/states/2-Two.txt", secondSource);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var edit = CombinedEdit(snapshot);
        var fileSystem = new FaultingFileSystem(failReplaceCall: 2, failRestore: true);

        var result = await new WorkspaceEditWriter(fileSystem).ApplyAsync(snapshot, edit);

        Assert.Equal(WorkspaceEditApplicationStatus.Failed, result.Status);
        var recoveryPath = Assert.Single(result.RecoveryArtifacts);
        Assert.True(File.Exists(recoveryPath));
        Assert.Equal(firstSource, ReadUtf8(recoveryPath));
        Assert.Contains(result.Issues, issue => issue.Code == "OXIDE5022");
        Assert.NotEqual(firstSource, File.ReadAllText(firstPath));
    }

    [Fact]
    public async Task Undo_restores_exact_original_bytes_through_the_same_safe_writer()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }\r\n";
        var originalBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        File.WriteAllBytes(path, originalBytes);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 25));
        var application = await new WorkspaceEditWriter().ApplyAsync(snapshot, plan.Edit!);
        var appliedSnapshot = await service.ReloadAsync();

        var undo = await new WorkspaceEditUndoService().RestoreAsync(appliedSnapshot, application.UndoRecord!);

        Assert.True(undo.IsRestored);
        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        Assert.Empty(undo.RecoveryArtifacts);
        Assert.Empty(Artifacts(path));
    }

    [Fact]
    public async Task Undo_refuses_to_overwrite_content_changed_after_the_edit()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteModFile(
            "history/states/1-Test.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 25));
        var application = await new WorkspaceEditWriter().ApplyAsync(snapshot, plan.Edit!);
        var appliedSnapshot = await service.ReloadAsync();
        const string external = "state = { id = 1 manpower = 99 state_category = rural }";
        File.WriteAllText(path, external);

        var undo = await new WorkspaceEditUndoService().RestoreAsync(appliedSnapshot, application.UndoRecord!);

        Assert.Equal(WorkspaceEditUndoStatus.Conflict, undo.Status);
        Assert.Equal(external, File.ReadAllText(path));
        Assert.Contains(undo.Issues, issue => issue.Code is "OXIDE5015" or "OXIDE5024");
    }

    private static WorkspaceEdit CombinedEdit(Oxide.Core.Workspaces.Snapshots.WorkspaceSnapshot snapshot)
    {
        var first = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 11));
        var second = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(2, 22));
        return new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Change two states",
            [first.Edit!.Documents[0], second.Edit!.Documents[0]]);
    }

    private static string ReadUtf8(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var preamble = Encoding.UTF8.GetPreamble();
        return Encoding.UTF8.GetString(bytes.AsSpan(bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0));
    }

    private static string[] Artifacts(string targetPath) =>
        Directory.GetFiles(Path.GetDirectoryName(targetPath)!, $".{Path.GetFileName(targetPath)}.oxide-*");

    private sealed class FaultingFileSystem(int failReplaceCall, bool failRestore) : IWorkspaceEditFileSystem
    {
        private readonly PhysicalWorkspaceEditFileSystem physical = new();
        private int replaceCalls;

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
            physical.ReadAllBytesAsync(path, cancellationToken);

        public Task WriteStagedAsync(
            string path,
            byte[] bytes,
            string sourcePath,
            CancellationToken cancellationToken) =>
            physical.WriteStagedAsync(path, bytes, sourcePath, cancellationToken);

        public void Replace(string stagedPath, string targetPath, string backupPath)
        {
            replaceCalls++;
            if (replaceCalls == failReplaceCall)
            {
                throw new IOException("Injected replacement failure.");
            }

            physical.Replace(stagedPath, targetPath, backupPath);
        }

        public void Restore(string backupPath, string targetPath)
        {
            if (failRestore)
            {
                throw new IOException("Injected rollback failure.");
            }

            physical.Restore(backupPath, targetPath);
        }

        public bool Exists(string path) => physical.Exists(path);

        public void DeleteIfExists(string path) => physical.DeleteIfExists(path);
    }
}

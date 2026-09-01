using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;

namespace Oxide.Tests.Workspaces;

public sealed class WorkspaceEditPreflightValidatorTests
{
    [Fact]
    public async Task Ready_preflight_revalidates_live_bytes_without_writing()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }";
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 20));

        var preflight = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, plan.Edit!);

        Assert.True(preflight.IsReady);
        Assert.Equal(WorkspaceEditPreflightStatus.Ready, preflight.Status);
        Assert.Single(preflight.LiveFingerprints);
        Assert.Same(plan.Edit, preflight.PreparedEdit!.Edit);
        Assert.Equal(source, File.ReadAllText(path));
    }

    [Fact]
    public async Task Changed_or_deleted_live_sources_are_explicit_conflicts()
    {
        using var fixture = new TemporaryWorkspace();
        var changedPath = fixture.WriteModFile(
            "history/states/1-Changed.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        var deletedPath = fixture.WriteModFile(
            "history/states/2-Deleted.txt",
            "state = { id = 2 manpower = 10 state_category = rural }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var changedPlan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 20));
        var deletedPlan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(2, 20));
        File.WriteAllText(changedPath, "state = { id = 1 manpower = 999 state_category = rural }");
        File.Delete(deletedPath);

        var changed = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, changedPlan.Edit!);
        var deleted = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, deletedPlan.Edit!);

        Assert.Equal(WorkspaceEditPreflightStatus.Conflict, changed.Status);
        Assert.Equal(WorkspaceEditPreflightStatus.Conflict, deleted.Status);
        Assert.Contains(changed.Issues, issue => issue.Code == "OXIDE5015");
        Assert.Contains(deleted.Issues, issue => issue.Code == "OXIDE5016");
        Assert.False(changed.IsReady);
        Assert.False(deleted.IsReady);
    }

    [Fact]
    public async Task Multi_document_preflight_is_not_ready_when_any_target_conflicts()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteModFile(
            "history/states/1-One.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        var conflictingPath = fixture.WriteModFile(
            "history/states/2-Two.txt",
            "state = { id = 2 manpower = 20 state_category = town }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var first = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 11));
        var second = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(2, 22));
        var combined = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Change two states",
            [first.Edit!.Documents[0], second.Edit!.Documents[0]]);
        File.AppendAllText(conflictingPath, " # external");

        var preflight = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, combined);

        Assert.Equal(WorkspaceEditPreflightStatus.Conflict, preflight.Status);
        Assert.False(preflight.IsReady);
        Assert.Equal(2, preflight.PreparedEdit!.Documents.Length);
        Assert.Equal(2, preflight.LiveFingerprints.Count);
        Assert.Single(preflight.Issues, issue => issue.Code == "OXIDE5015");
    }

    [Fact]
    public async Task Invalid_candidate_and_cancellation_do_not_read_or_write_targets()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }";
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 20));
        var malformed = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Malformed candidate",
            [new DocumentEdit(
                plan.Edit!.Documents[0].Target,
                [new TextChange(new Oxide.Syntax.Text.TextSpan(0, source.Length), "state = {")])]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var rejected = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, malformed);
        var cancelled = await WorkspaceEditPreflightValidator.ValidateAsync(snapshot, plan.Edit!, cancellation.Token);

        Assert.Equal(WorkspaceEditPreflightStatus.Rejected, rejected.Status);
        Assert.Equal(WorkspaceEditPreflightStatus.Cancelled, cancelled.Status);
        Assert.Empty(rejected.LiveFingerprints);
        Assert.Empty(cancelled.LiveFingerprints);
        Assert.Equal(source, File.ReadAllText(path));
    }
}

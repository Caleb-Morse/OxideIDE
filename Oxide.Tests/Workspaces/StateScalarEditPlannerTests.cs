using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;

namespace Oxide.Tests.Workspaces;

public sealed class StateScalarEditPlannerTests
{
    [Fact]
    public async Task Plans_exact_manpower_replacement_in_the_effective_writable_declaration()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state = { id = 1 manpower = 5 state_category = rural }");
        var modPath = fixture.WriteModFile(
            "history/states/1-Mod.txt",
            "state = { id = 1 manpower = 10 state_category = town } # keep me");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var plan = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 125_000));

        Assert.True(plan.IsValid);
        Assert.True(plan.Capability.IsEditable);
        var document = Assert.Single(plan.Edit!.Documents);
        var change = Assert.Single(document.Changes);
        Assert.Equal("10", snapshot.DocumentsById[document.Target.DocumentId].Text!.GetText(change.Span));
        Assert.Equal("125000", change.Replacement);
        Assert.Equal(
            "state = { id = 1 manpower = 125000 state_category = town } # keep me",
            Assert.Single(plan.PreparedEdit!.Documents).UpdatedSource.Text);
        Assert.Equal(
            "state = { id = 1 manpower = 10 state_category = town } # keep me",
            File.ReadAllText(modPath));
    }

    [Fact]
    public async Task Plans_state_category_replacement_and_semantically_validates_the_candidate()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteModFile(
            "history/states/2-Test.txt",
            "state = { id = 2 manpower = 20 state_category = rural }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var plan = StateScalarEditPlanner.Plan(
            snapshot,
            StateScalarEditIntent.SetStateCategory(2, "large_city"));

        Assert.True(plan.IsValid);
        Assert.Empty(plan.Issues);
        Assert.Contains("state_category = large_city", plan.PreparedEdit!.Documents[0].UpdatedSource.Text);
        Assert.Equal("large_city", plan.Intent.DesiredValue);
    }

    [Fact]
    public async Task Refuses_base_game_missing_duplicate_and_invalid_scalar_edits()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/1-Base.txt",
            "state = { id = 1 manpower = 10 state_category = rural }");
        fixture.WriteModFile("history/states/2-Missing.txt", "state = { id = 2 state_category = rural }");
        fixture.WriteModFile(
            "history/states/3-Duplicate.txt",
            "state = { id = 3 manpower = 10 manpower = 20 state_category = rural }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var baseGame = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(1, 20));
        var missing = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(2, 20));
        var duplicate = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(3, 20));
        var negative = StateScalarEditPlanner.Plan(snapshot, StateScalarEditIntent.SetManpower(2, -1));
        var unsafeCategory = StateScalarEditPlanner.Plan(
            snapshot,
            StateScalarEditIntent.SetStateCategory(2, "large city"));
        var unchangedCategory = StateScalarEditPlanner.Plan(
            snapshot,
            StateScalarEditIntent.SetStateCategory(2, "rural"));

        Assert.Equal(EditRefusalReason.ReadOnlyLayer, baseGame.Capability.RefusalReason);
        Assert.Equal(EditRefusalReason.MissingProvenance, missing.Capability.RefusalReason);
        Assert.Equal(EditRefusalReason.AmbiguousDeclaration, duplicate.Capability.RefusalReason);
        Assert.Equal(EditRefusalReason.UnsupportedOperation, negative.Capability.RefusalReason);
        Assert.Equal(EditRefusalReason.UnsupportedOperation, unsafeCategory.Capability.RefusalReason);
        Assert.Equal(EditRefusalReason.NoChangeRequired, unchangedCategory.Capability.RefusalReason);
        Assert.Null(baseGame.Edit);
        Assert.Null(missing.PreparedEdit);
    }
}

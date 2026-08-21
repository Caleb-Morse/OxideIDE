using Oxide.App.ViewModels;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Application;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task Open_workspace_projects_real_states_problems_and_selection()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"");
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=\"STATE_ONE\" history={ owner=USA } provinces={ 10 11 } }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=\"STATE_TWO\" history={ owner=ZZZ } }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service);
        viewModel.GameRootPath = fixture.GameRoot;

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Workspace, viewModel.Screen);
        Assert.Equal(2, viewModel.States.Count);
        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Contains(viewModel.Problems, problem => problem.Code == "OXIDE4006" && problem.StateId == 2);
        Assert.Contains("2 states", viewModel.WorkspaceSummary, StringComparison.Ordinal);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task Search_filters_by_id_name_owner_category_and_source()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"");
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 name=\"ALPHA\" state_category=city history={ owner=USA } }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 name=\"BETA\" state_category=rural }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();

        viewModel.SearchText = "rural";
        Assert.Equal(2, Assert.Single(viewModel.States).Id);

        viewModel.SearchText = "USA";
        Assert.Equal(1, Assert.Single(viewModel.States).Id);

        viewModel.SearchText = "One.txt";
        Assert.Equal(1, Assert.Single(viewModel.States).Id);
    }

    [Fact]
    public async Task Selecting_a_state_problem_reveals_that_state_and_clears_a_hiding_filter()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 }");
        fixture.WriteGameFile("history/states/2-Two.txt", "state={ id=2 history={ owner=ZZZ } }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        viewModel.SearchText = "State 1";

        viewModel.SelectedProblem = Assert.Single(viewModel.Problems, problem => problem.StateId == 2);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(2, viewModel.SelectedState?.Id);
    }

    [Fact]
    public async Task Reload_preserves_selection_and_publishes_new_source_values()
    {
        using var fixture = new TemporaryWorkspace();
        var statePath = fixture.WriteGameFile("history/states/1-One.txt", "state={ id=1 manpower=10 }");
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = fixture.GameRoot };
        await viewModel.OpenWorkspaceAsync();
        File.WriteAllText(statePath, "state={ id=1 manpower=20 }");

        await viewModel.ReloadAsync();

        Assert.Equal(1, viewModel.SelectedState?.Id);
        Assert.Equal("20", viewModel.SelectedState?.Manpower);
        Assert.Contains("Snapshot 2", viewModel.StatusSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_root_is_a_recoverable_workspace_with_a_visible_problem()
    {
        using var fixture = new TemporaryWorkspace();
        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service)
        {
            GameRootPath = Path.Combine(fixture.Root, "missing"),
        };

        await viewModel.OpenWorkspaceAsync();

        Assert.Equal(ApplicationScreen.Workspace, viewModel.Screen);
        Assert.Empty(viewModel.States);
        Assert.Contains(viewModel.Problems, problem => problem.Code == "OXIDE3001");
        Assert.Equal(1, viewModel.ErrorCount);
    }

    [Fact]
    public async Task Cancellation_returns_to_welcome_without_publishing_partial_state()
    {
        var service = new CancellableWorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = "/unused" };

        var opening = viewModel.OpenWorkspaceAsync();
        Assert.Equal(ApplicationScreen.Loading, viewModel.Screen);
        viewModel.CancelLoading();
        await opening;

        Assert.Equal(ApplicationScreen.Welcome, viewModel.Screen);
        Assert.True(viewModel.HasError);
        Assert.Empty(viewModel.States);
    }

    private sealed class CancellableWorkspaceService : IWorkspaceService
    {
        public WorkspaceSnapshot? CurrentSnapshot => null;

        public event Action<WorkspaceSnapshot>? SnapshotPublished
        {
            add { }
            remove { }
        }

        public async Task<WorkspaceSnapshot> OpenAsync(
            WorkspaceConfiguration configuration,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellable test service unexpectedly completed.");
        }

        public Task<WorkspaceSnapshot> ReloadAsync(
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

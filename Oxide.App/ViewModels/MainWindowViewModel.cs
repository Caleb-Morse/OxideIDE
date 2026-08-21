using System.Collections.ObjectModel;
using Oxide.Core;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;

namespace Oxide.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceService workspaceService;
    private readonly bool ownsWorkspaceService;
    private readonly List<StateListItemViewModel> allStates = [];
    private CancellationTokenSource? loadCancellation;
    private ApplicationScreen screen = ApplicationScreen.Welcome;
    private string gameRootPath = string.Empty;
    private string activeModRootPath = string.Empty;
    private string searchText = string.Empty;
    private string loadingMessage = "Preparing workspace…";
    private double loadingProgress;
    private string? errorMessage;
    private StateListItemViewModel? selectedState;
    private ProblemListItemViewModel? selectedProblem;
    private WorkspaceSnapshot? snapshot;

    public MainWindowViewModel()
        : this(new WorkspaceService(), ownsWorkspaceService: true)
    {
    }

    public MainWindowViewModel(IWorkspaceService workspaceService, bool ownsWorkspaceService = false)
    {
        this.workspaceService = workspaceService;
        this.ownsWorkspaceService = ownsWorkspaceService;
        ApplicationName = ApplicationInfo.Oxide.Name;
    }

    public string ApplicationName { get; }

    public ObservableCollection<StateListItemViewModel> States { get; } = [];

    public ObservableCollection<ProblemListItemViewModel> Problems { get; } = [];

    public ApplicationScreen Screen
    {
        get => screen;
        private set
        {
            if (SetProperty(ref screen, value))
            {
                OnPropertyChanged(nameof(IsWelcomeVisible));
                OnPropertyChanged(nameof(IsLoadingVisible));
                OnPropertyChanged(nameof(IsWorkspaceVisible));
            }
        }
    }

    public bool IsWelcomeVisible => Screen is ApplicationScreen.Welcome;

    public bool IsLoadingVisible => Screen is ApplicationScreen.Loading;

    public bool IsWorkspaceVisible => Screen is ApplicationScreen.Workspace;

    public string GameRootPath
    {
        get => gameRootPath;
        set
        {
            if (SetProperty(ref gameRootPath, value))
            {
                OnPropertyChanged(nameof(CanOpenWorkspace));
            }
        }
    }

    public string ActiveModRootPath
    {
        get => activeModRootPath;
        set => SetProperty(ref activeModRootPath, value);
    }

    public bool CanOpenWorkspace => !string.IsNullOrWhiteSpace(GameRootPath);

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string LoadingMessage
    {
        get => loadingMessage;
        private set => SetProperty(ref loadingMessage, value);
    }

    public double LoadingProgress
    {
        get => loadingProgress;
        private set => SetProperty(ref loadingProgress, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public StateListItemViewModel? SelectedState
    {
        get => selectedState;
        set
        {
            if (SetProperty(ref selectedState, value))
            {
                OnPropertyChanged(nameof(HasSelectedState));
                OnPropertyChanged(nameof(HasNoSelectedState));
            }
        }
    }

    public bool HasSelectedState => SelectedState is not null;

    public bool HasNoSelectedState => SelectedState is null;

    public ProblemListItemViewModel? SelectedProblem
    {
        get => selectedProblem;
        set
        {
            if (SetProperty(ref selectedProblem, value) && value?.StateId is { } stateId)
            {
                SelectState(stateId);
            }
        }
    }

    public string WorkspaceName => snapshot?.Configuration.DisplayName ?? "No workspace";

    public string WorkspaceSummary => snapshot is null
        ? "No workspace loaded"
        : $"{snapshot.Documents.Length:N0} files · {snapshot.Semantics.States.Count:N0} states · {snapshot.Semantics.Countries.Count:N0} countries";

    public string StatusSummary => snapshot is null
        ? "Ready"
        : $"Snapshot {snapshot.Version} · {ErrorCount:N0} errors · {WarningCount:N0} warnings";

    public int ErrorCount => Problems.Count(problem => problem.Severity is DiagnosticSeverity.Error);

    public int WarningCount => Problems.Count(problem => problem.Severity is DiagnosticSeverity.Warning);

    public async Task OpenWorkspaceAsync()
    {
        if (!CanOpenWorkspace)
        {
            ErrorMessage = "Choose a Hearts of Iron IV installation folder first.";
            return;
        }

        var configuration = new WorkspaceConfiguration(
            GameRootPath,
            string.IsNullOrWhiteSpace(ActiveModRootPath) ? null : ActiveModRootPath);
        await LoadAsync((progress, cancellation) =>
            workspaceService.OpenAsync(configuration, progress, cancellation));
    }

    public async Task ReloadAsync()
    {
        if (snapshot is null)
        {
            return;
        }

        await LoadAsync((progress, cancellation) => workspaceService.ReloadAsync(progress, cancellation));
    }

    public void CancelLoading() => loadCancellation?.Cancel();

    public void ShowWelcome()
    {
        CancelLoading();
        Screen = ApplicationScreen.Welcome;
    }

    public void ClearActiveMod() => ActiveModRootPath = string.Empty;

    public void Dispose()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        if (ownsWorkspaceService && workspaceService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task LoadAsync(
        Func<IProgress<WorkspaceLoadProgress>, CancellationToken, Task<WorkspaceSnapshot>> load)
    {
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        ErrorMessage = null;
        LoadingProgress = 0;
        LoadingMessage = "Discovering supported files…";
        Screen = ApplicationScreen.Loading;
        var progress = new Progress<WorkspaceLoadProgress>(UpdateProgress);

        try
        {
            var loadedSnapshot = await load(progress, loadCancellation.Token);
            ApplySnapshot(loadedSnapshot);
            Screen = ApplicationScreen.Workspace;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Workspace loading was cancelled.";
            Screen = snapshot is null ? ApplicationScreen.Welcome : ApplicationScreen.Workspace;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Oxide could not open the workspace: {exception.Message}";
            Screen = snapshot is null ? ApplicationScreen.Welcome : ApplicationScreen.Workspace;
        }
    }

    private void UpdateProgress(WorkspaceLoadProgress progress)
    {
        LoadingProgress = progress.TotalDocuments == 0
            ? 0
            : (double)progress.ProcessedDocuments / progress.TotalDocuments * 100;
        LoadingMessage = progress.Stage switch
        {
            WorkspaceLoadStage.Discovering => "Discovering supported files…",
            WorkspaceLoadStage.LoadingDocuments when progress.CurrentPath is not null =>
                $"Loading {Path.GetFileName(progress.CurrentPath)}",
            WorkspaceLoadStage.LoadingDocuments => "Building source documents…",
            WorkspaceLoadStage.Publishing => "Publishing workspace snapshot…",
            WorkspaceLoadStage.Complete => "Workspace ready",
            _ => "Loading workspace…",
        };
    }

    private void ApplySnapshot(WorkspaceSnapshot loadedSnapshot)
    {
        var previousStateId = SelectedState?.Id;
        snapshot = loadedSnapshot;
        allStates.Clear();
        allStates.AddRange(loadedSnapshot.Semantics.States.Values
            .OrderBy(state => int.Parse(state.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture))
            .Select(state => new StateListItemViewModel(state, loadedSnapshot)));

        Problems.Clear();
        foreach (var problem in loadedSnapshot.Diagnostics.Select(ProblemListItemViewModel.FromWorkspace)
                     .Concat(loadedSnapshot.Semantics.Diagnostics.Select(ProblemListItemViewModel.FromSemantic))
                     .OrderByDescending(problem => problem.Severity)
                     .ThenBy(problem => problem.Code, StringComparer.Ordinal))
        {
            Problems.Add(problem);
        }

        ApplyFilter();
        SelectedState = previousStateId is { } id
            ? States.FirstOrDefault(state => state.Id == id)
            : States.FirstOrDefault();
        OnPropertyChanged(nameof(WorkspaceName));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
    }

    private void ApplyFilter()
    {
        var selectedId = SelectedState?.Id;
        var query = SearchText.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? allStates
            : allStates.Where(state => state.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        States.Clear();
        foreach (var state in matches)
        {
            States.Add(state);
        }

        if (selectedId is { } id)
        {
            SelectedState = States.FirstOrDefault(state => state.Id == id);
        }
    }

    private void SelectState(int stateId)
    {
        var state = allStates.FirstOrDefault(candidate => candidate.Id == stateId);
        if (state is null)
        {
            return;
        }

        if (!States.Contains(state))
        {
            SearchText = string.Empty;
        }

        SelectedState = state;
    }
}

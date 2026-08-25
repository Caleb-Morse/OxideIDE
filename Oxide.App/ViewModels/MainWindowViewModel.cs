using System.Collections.ObjectModel;
using System.Collections.Immutable;
using Oxide.App.Settings;
using Oxide.Core;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;

namespace Oxide.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceService workspaceService;
    private readonly bool ownsWorkspaceService;
    private readonly IApplicationSettingsStore? settingsStore;
    private readonly WorkspaceRefreshCoordinator? refreshCoordinator;
    private readonly Func<WorkspaceConfiguration, IWorkspaceChangeSource>? changeSourceFactory;
    private readonly bool ownsRefreshCoordinator;
    private readonly SynchronizationContext? presentationContext;
    private readonly List<StateListItemViewModel> allStates = [];
    private readonly List<CountryListItemViewModel> allCountries = [];
    private CancellationTokenSource? loadCancellation;
    private ApplicationScreen screen = ApplicationScreen.Welcome;
    private string gameRootPath = string.Empty;
    private string activeModRootPath = string.Empty;
    private string searchText = string.Empty;
    private string countrySearchText = string.Empty;
    private string preferredLanguage = "english";
    private string selectedLanguage = "english";
    private bool englishFallbackEnabled = true;
    private string loadingMessage = "Preparing workspace…";
    private double loadingProgress;
    private string? errorMessage;
    private StateListItemViewModel? selectedState;
    private CountryListItemViewModel? selectedCountry;
    private ProblemListItemViewModel? selectedProblem;
    private WorkspaceSnapshot? snapshot;
    private OxideTheme theme = OxideTheme.IronRustDark;
    private bool showingCountryDetails;
    private SourceNavigationRequest? lastSourceNavigationRequest;
    private bool automaticRefreshEnabled = true;
    private bool explicitLoadActive;
    private WorkspaceRefreshCoordinatorStatus refreshStatus = new(
        WorkspaceRefreshCoordinatorState.Stopped,
        "Automatic refresh is not active.");

    public MainWindowViewModel()
        : this(new WorkspaceService(), new JsonApplicationSettingsStore(), ownsWorkspaceService: true)
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IApplicationSettingsStore? settingsStore = null,
        bool ownsWorkspaceService = false,
        WorkspaceRefreshCoordinator? refreshCoordinator = null,
        Func<WorkspaceConfiguration, IWorkspaceChangeSource>? changeSourceFactory = null)
    {
        this.workspaceService = workspaceService;
        this.settingsStore = settingsStore;
        this.ownsWorkspaceService = ownsWorkspaceService;
        presentationContext = SynchronizationContext.Current;
        this.refreshCoordinator = refreshCoordinator ?? (ownsWorkspaceService
            ? new WorkspaceRefreshCoordinator(workspaceService)
            : null);
        this.changeSourceFactory = changeSourceFactory ?? (ownsWorkspaceService
            ? configuration => new FileSystemWorkspaceChangeSource(configuration)
            : null);
        ownsRefreshCoordinator = refreshCoordinator is null && this.refreshCoordinator is not null;
        if (this.refreshCoordinator is not null)
        {
            this.refreshCoordinator.StatusChanged += OnRefreshStatusChanged;
            workspaceService.SnapshotPublished += OnSnapshotPublished;
        }
        ApplicationName = ApplicationInfo.Oxide.Name;
    }

    public event Action<OxideTheme>? ThemeChanged;

    public event Action<SourceNavigationRequest>? SourceNavigationRequested;

    public string ApplicationName { get; }

    public ObservableCollection<StateListItemViewModel> States { get; } = [];

    public ObservableCollection<CountryListItemViewModel> Countries { get; } = [];

    public ObservableCollection<LanguageOptionViewModel> AvailableLanguages { get; } = [];

    public ObservableCollection<ProblemListItemViewModel> Problems { get; } = [];

    public SourceNavigationRequest? LastSourceNavigationRequest
    {
        get => lastSourceNavigationRequest;
        private set
        {
            if (SetProperty(ref lastSourceNavigationRequest, value))
            {
                OnPropertyChanged(nameof(HasSourceNavigationRequest));
                OnPropertyChanged(nameof(SourceNavigationSummary));
            }
        }
    }

    public bool HasSourceNavigationRequest => LastSourceNavigationRequest is not null;

    public string SourceNavigationSummary => LastSourceNavigationRequest is null
        ? string.Empty
        : $"Source target: {LastSourceNavigationRequest.VirtualPath} · {LastSourceNavigationRequest.Location}";

    public void RequestSourceNavigation(SourceNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        LastSourceNavigationRequest = request;
        SourceNavigationRequested?.Invoke(request);
    }

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

    public string CountrySearchText
    {
        get => countrySearchText;
        set
        {
            if (SetProperty(ref countrySearchText, value))
            {
                ApplyCountryFilter();
            }
        }
    }

    public string SelectedLanguage
    {
        get => selectedLanguage;
        private set
        {
            if (SetProperty(ref selectedLanguage, value))
            {
                OnPropertyChanged(nameof(LanguageSummary));
                OnPropertyChanged(nameof(SelectedLanguageOption));
            }
        }
    }

    public string PreferredLanguage
    {
        get => preferredLanguage;
        private set
        {
            if (SetProperty(ref preferredLanguage, value))
            {
                OnPropertyChanged(nameof(LanguageSummary));
            }
        }
    }

    public LanguageOptionViewModel? SelectedLanguageOption =>
        AvailableLanguages.FirstOrDefault(language => language.Id == SelectedLanguage);

    public bool HasAvailableLanguages => AvailableLanguages.Count > 0;

    public bool EnglishFallbackEnabled
    {
        get => englishFallbackEnabled;
        private set
        {
            if (SetProperty(ref englishFallbackEnabled, value))
            {
                OnPropertyChanged(nameof(LanguageSummary));
            }
        }
    }

    public string LanguageSummary => AvailableLanguages.Count == 0
        ? "No localisation languages discovered"
        : SelectedLanguage == PreferredLanguage
            ? $"Displaying {SelectedLanguage}"
            : $"Displaying {SelectedLanguage}; {PreferredLanguage} is unavailable";

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

    public OxideTheme Theme
    {
        get => theme;
        private set
        {
            if (SetProperty(ref theme, value))
            {
                OnPropertyChanged(nameof(ThemeName));
                OnPropertyChanged(nameof(IsDarkMode));
                ThemeChanged?.Invoke(value);
            }
        }
    }

    public string ThemeName => Theme switch
    {
        OxideTheme.IronRustDark => "Iron Rust Dark",
        OxideTheme.CopperVerdigrisLight => "Copper Verdigris Light",
        _ => "Oxide theme",
    };

    public bool IsDarkMode => Theme is OxideTheme.IronRustDark;

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

    public CountryListItemViewModel? SelectedCountry
    {
        get => selectedCountry;
        set
        {
            if (SetProperty(ref selectedCountry, value))
            {
                OnPropertyChanged(nameof(HasSelectedCountry));
                OnPropertyChanged(nameof(HasNoSelectedCountry));
            }
        }
    }

    public bool HasSelectedCountry => SelectedCountry is not null;

    public bool HasNoSelectedCountry => SelectedCountry is null;

    public bool IsStateDetailsVisible => !showingCountryDetails;

    public bool IsCountryDetailsVisible => showingCountryDetails;

    public void ShowStateDetails()
    {
        if (!showingCountryDetails)
        {
            return;
        }

        showingCountryDetails = false;
        OnPropertyChanged(nameof(IsStateDetailsVisible));
        OnPropertyChanged(nameof(IsCountryDetailsVisible));
    }

    public void ShowCountryDetails()
    {
        if (showingCountryDetails)
        {
            return;
        }

        showingCountryDetails = true;
        OnPropertyChanged(nameof(IsStateDetailsVisible));
        OnPropertyChanged(nameof(IsCountryDetailsVisible));
    }

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
        : $"Snapshot {snapshot.Version} · {snapshot.LoadMetrics.TotalMilliseconds:N0} ms · {ErrorCount:N0} errors · {WarningCount:N0} warnings";

    public int ErrorCount => Problems.Count(problem => problem.Severity is DiagnosticSeverity.Error);

    public int WarningCount => Problems.Count(problem => problem.Severity is DiagnosticSeverity.Warning);

    public bool AutomaticRefreshEnabled
    {
        get => automaticRefreshEnabled;
        private set
        {
            if (SetProperty(ref automaticRefreshEnabled, value))
            {
                OnPropertyChanged(nameof(AutomaticRefreshSummary));
                OnPropertyChanged(nameof(IsAutomaticRefreshAvailable));
            }
        }
    }

    public bool IsAutomaticRefreshAvailable => refreshCoordinator is not null && changeSourceFactory is not null;

    public string AutomaticRefreshSummary => !AutomaticRefreshEnabled
        ? "Automatic refresh is off"
        : refreshStatus.State switch
        {
            WorkspaceRefreshCoordinatorState.Watching or WorkspaceRefreshCoordinatorState.UpToDate =>
                "Watching for changes",
            WorkspaceRefreshCoordinatorState.ChangesPending => "Changes pending…",
            WorkspaceRefreshCoordinatorState.Refreshing => "Refreshing…",
            WorkspaceRefreshCoordinatorState.RefreshFailed => "Automatic refresh failed",
            WorkspaceRefreshCoordinatorState.WatcherUnavailable => "File watching unavailable",
            _ => "Automatic refresh is waiting",
        };

    public string AutomaticRefreshDetail => refreshStatus.Message;

    public bool IsRefreshBusy => AutomaticRefreshEnabled && refreshStatus.State is
        WorkspaceRefreshCoordinatorState.ChangesPending or WorkspaceRefreshCoordinatorState.Refreshing;

    public bool RefreshNeedsAttention => AutomaticRefreshEnabled && refreshStatus.State is
        WorkspaceRefreshCoordinatorState.RefreshFailed or WorkspaceRefreshCoordinatorState.WatcherUnavailable;

    public async Task InitializeAsync()
    {
        if (settingsStore is null)
        {
            ThemeChanged?.Invoke(Theme);
            return;
        }

        try
        {
            var result = await settingsStore.LoadAsync();
            GameRootPath = result.Settings.LastGameRoot ?? string.Empty;
            ActiveModRootPath = result.Settings.LastActiveModRoot ?? string.Empty;
            Theme = result.Settings.Theme;
            PreferredLanguage = LanguageSelectionPolicy.NormalizePreference(result.Settings.PreferredLanguage);
            EnglishFallbackEnabled = result.Settings.EnglishFallbackEnabled;
            AutomaticRefreshEnabled = result.Settings.AutomaticRefreshEnabled;
            ThemeChanged?.Invoke(Theme);
            if (result.Warning is not null)
            {
                ErrorMessage = result.Warning + " Defaults were restored.";
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Oxide could not initialize saved settings: {exception.Message}";
            ThemeChanged?.Invoke(Theme);
        }
    }

    public async Task ToggleThemeAsync()
    {
        Theme = Theme is OxideTheme.IronRustDark
            ? OxideTheme.CopperVerdigrisLight
            : OxideTheme.IronRustDark;
        await SaveSettingsAsync();
    }

    public async Task ChangeLanguageAsync(string? language)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        var normalized = LanguageSelectionPolicy.NormalizePreference(language);
        if (AvailableLanguages.All(option => option.Id != normalized))
        {
            return;
        }

        PreferredLanguage = normalized;
        if (SelectedLanguage != normalized)
        {
            SelectedLanguage = normalized;
            RebuildPresentation();
        }

        await SaveSettingsAsync();
    }

    public async Task SetEnglishFallbackAsync(bool enabled)
    {
        if (EnglishFallbackEnabled == enabled)
        {
            return;
        }

        EnglishFallbackEnabled = enabled;
        RebuildPresentation();
        await SaveSettingsAsync();
    }

    public async Task SetAutomaticRefreshAsync(bool enabled)
    {
        if (!IsAutomaticRefreshAvailable || AutomaticRefreshEnabled == enabled)
        {
            return;
        }

        AutomaticRefreshEnabled = enabled;
        if (enabled && snapshot is not null)
        {
            await StartAutomaticRefreshAsync(snapshot.Configuration);
        }
        else if (!enabled && refreshCoordinator is not null)
        {
            await refreshCoordinator.StopAsync();
            ApplyRefreshStatus(refreshCoordinator.Status);
        }

        await SaveSettingsAsync();
    }

    public void DismissError() => ErrorMessage = null;

    public void ReportError(string message) => ErrorMessage = message;

    public async Task OpenWorkspaceAsync()
    {
        if (!CanOpenWorkspace)
        {
            ErrorMessage = "Choose a Hearts of Iron IV installation folder first.";
            return;
        }

        WorkspaceConfiguration configuration;
        try
        {
            configuration = new WorkspaceConfiguration(
                GameRootPath,
                string.IsNullOrWhiteSpace(ActiveModRootPath) ? null : ActiveModRootPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            ErrorMessage = $"Oxide could not use the selected workspace paths: {exception.Message}";
            Screen = snapshot is null ? ApplicationScreen.Welcome : ApplicationScreen.Workspace;
            return;
        }

        if (refreshCoordinator is not null)
        {
            await refreshCoordinator.StopAsync();
        }

        await LoadAsync(
            (progress, cancellation) => workspaceService.OpenAsync(configuration, progress, cancellation),
            startAutomaticRefresh: true);
    }

    public async Task ReloadAsync()
    {
        if (snapshot is null)
        {
            return;
        }

        if (AutomaticRefreshEnabled && refreshCoordinator is not null)
        {
            await LoadAsync(
                (_, cancellation) => refreshCoordinator.ReloadAsync(cancellation),
                startAutomaticRefresh: false);
            return;
        }

        await LoadAsync(
            (progress, cancellation) => workspaceService.ReloadAsync(progress, cancellation),
            startAutomaticRefresh: false);
    }

    public void CancelLoading() => loadCancellation?.Cancel();

    public async Task ShowWelcomeAsync()
    {
        CancelLoading();
        if (refreshCoordinator is not null)
        {
            await refreshCoordinator.StopAsync();
            ApplyRefreshStatus(refreshCoordinator.Status);
        }

        Screen = ApplicationScreen.Welcome;
    }

    public void ClearActiveMod() => ActiveModRootPath = string.Empty;

    public void Dispose()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        if (refreshCoordinator is not null)
        {
            refreshCoordinator.StatusChanged -= OnRefreshStatusChanged;
            workspaceService.SnapshotPublished -= OnSnapshotPublished;
            if (ownsRefreshCoordinator)
            {
                refreshCoordinator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        if (ownsWorkspaceService && workspaceService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task LoadAsync(
        Func<IProgress<WorkspaceLoadProgress>, CancellationToken, Task<WorkspaceSnapshot>> load,
        bool startAutomaticRefresh)
    {
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        ErrorMessage = null;
        LoadingProgress = 0;
        LoadingMessage = "Discovering supported files…";
        Screen = ApplicationScreen.Loading;
        var progress = new Progress<WorkspaceLoadProgress>(UpdateProgress);
        explicitLoadActive = true;

        try
        {
            var loadedSnapshot = await load(progress, loadCancellation.Token);
            ApplySnapshot(loadedSnapshot);
            Screen = ApplicationScreen.Workspace;
            if (startAutomaticRefresh && AutomaticRefreshEnabled)
            {
                await StartAutomaticRefreshAsync(loadedSnapshot.Configuration);
            }

            await SaveSettingsAsync();
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
        finally
        {
            explicitLoadActive = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (settingsStore is null)
        {
            return;
        }

        try
        {
            await settingsStore.SaveAsync(new ApplicationSettings(
                LastGameRoot: string.IsNullOrWhiteSpace(GameRootPath) ? null : GameRootPath,
                LastActiveModRoot: string.IsNullOrWhiteSpace(ActiveModRootPath) ? null : ActiveModRootPath,
                Theme: Theme,
                PreferredLanguage: PreferredLanguage,
                EnglishFallbackEnabled: EnglishFallbackEnabled,
                AutomaticRefreshEnabled: AutomaticRefreshEnabled));
        }
        catch (Exception exception)
        {
            ErrorMessage = $"The workspace opened, but Oxide could not save its settings: {exception.Message}";
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
            WorkspaceLoadStage.BuildingSemantics => "Building semantic model…",
            WorkspaceLoadStage.Publishing => "Publishing workspace snapshot…",
            WorkspaceLoadStage.Complete => "Workspace ready",
            _ => "Loading workspace…",
        };
    }

    private void ApplySnapshot(WorkspaceSnapshot loadedSnapshot)
    {
        var previousStateId = SelectedState?.Id;
        var previousCountryTag = SelectedCountry?.Tag;
        LastSourceNavigationRequest = null;
        snapshot = loadedSnapshot;
        AvailableLanguages.Clear();
        foreach (var language in loadedSnapshot.Semantics.LocalisationResolver.AvailableLanguages)
        {
            AvailableLanguages.Add(LanguageOptionViewModel.Create(language.Value));
        }

        OnPropertyChanged(nameof(HasAvailableLanguages));
        SelectedLanguage = LanguageSelectionPolicy.ChooseEffective(
            PreferredLanguage,
            AvailableLanguages.Select(language => language.Id).ToImmutableArray());
        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(LanguageSummary));
        RebuildPresentation();

        Problems.Clear();
        foreach (var problem in loadedSnapshot.Diagnostics.Select(ProblemListItemViewModel.FromWorkspace)
                     .Concat(loadedSnapshot.Semantics.Diagnostics.Select(ProblemListItemViewModel.FromSemantic))
                     .OrderByDescending(problem => problem.Severity)
                     .ThenBy(problem => problem.Code, StringComparer.Ordinal))
        {
            Problems.Add(problem);
        }

        SelectedState = previousStateId is { } id
            ? States.FirstOrDefault(state => state.Id == id)
            : States.FirstOrDefault();
        SelectedCountry = previousCountryTag is { } tag
            ? Countries.FirstOrDefault(country => country.Tag == tag)
            : Countries.FirstOrDefault();
        OnPropertyChanged(nameof(WorkspaceName));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
    }

    private async Task StartAutomaticRefreshAsync(WorkspaceConfiguration configuration)
    {
        if (refreshCoordinator is null || changeSourceFactory is null)
        {
            return;
        }

        try
        {
            await refreshCoordinator.ReplaceChangeSourceAsync(changeSourceFactory(configuration));
            ApplyRefreshStatus(refreshCoordinator.Status);
        }
        catch (Exception exception)
        {
            ApplyRefreshStatus(new WorkspaceRefreshCoordinatorStatus(
                WorkspaceRefreshCoordinatorState.WatcherUnavailable,
                exception.Message));
        }
    }

    private void OnSnapshotPublished(WorkspaceSnapshot published)
    {
        if (explicitLoadActive || !AutomaticRefreshEnabled)
        {
            return;
        }

        DispatchPresentation(() =>
        {
            ApplySnapshot(published);
            Screen = ApplicationScreen.Workspace;
        });
    }

    private void OnRefreshStatusChanged(WorkspaceRefreshCoordinatorStatus updated) =>
        DispatchPresentation(() => ApplyRefreshStatus(updated));

    private void ApplyRefreshStatus(WorkspaceRefreshCoordinatorStatus updated)
    {
        refreshStatus = updated;
        OnPropertyChanged(nameof(AutomaticRefreshSummary));
        OnPropertyChanged(nameof(AutomaticRefreshDetail));
        OnPropertyChanged(nameof(IsRefreshBusy));
        OnPropertyChanged(nameof(RefreshNeedsAttention));
    }

    private void DispatchPresentation(Action action)
    {
        if (presentationContext is null || ReferenceEquals(SynchronizationContext.Current, presentationContext))
        {
            action();
            return;
        }

        presentationContext.Post(_ => action(), null);
    }

    private void RebuildPresentation()
    {
        if (snapshot is null)
        {
            return;
        }

        var stateId = SelectedState?.Id;
        var countryTag = SelectedCountry?.Tag;
        allStates.Clear();
        allStates.AddRange(snapshot.Semantics.States.Values
            .Select(state => new StateListItemViewModel(
                state,
                snapshot,
                SelectedLanguage,
                EnglishFallbackEnabled))
            .OrderBy(state => state.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(state => state.Id));
        allCountries.Clear();
        allCountries.AddRange(snapshot.Semantics.Countries.Values
            .Select(country => new CountryListItemViewModel(
                country,
                snapshot,
                SelectedLanguage,
                EnglishFallbackEnabled))
            .OrderBy(country => country.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(country => country.Tag, StringComparer.Ordinal));
        ApplyFilter();
        ApplyCountryFilter();
        if (stateId is not null && States.All(state => state.Id != stateId))
        {
            SearchText = string.Empty;
        }

        if (countryTag is not null && Countries.All(country => country.Tag != countryTag))
        {
            CountrySearchText = string.Empty;
        }

        SelectedState = stateId is null ? States.FirstOrDefault() : States.FirstOrDefault(state => state.Id == stateId);
        SelectedCountry = countryTag is null
            ? Countries.FirstOrDefault()
            : Countries.FirstOrDefault(country => country.Tag == countryTag);
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

    public void SelectStateFromCountry(int stateId) => SelectState(stateId);

    private void ApplyCountryFilter()
    {
        var selectedTag = SelectedCountry?.Tag;
        var query = CountrySearchText.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? allCountries
            : allCountries.Where(country => country.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));

        Countries.Clear();
        foreach (var country in matches)
        {
            Countries.Add(country);
        }

        if (selectedTag is not null)
        {
            SelectedCountry = Countries.FirstOrDefault(country => country.Tag == selectedTag);
        }
    }

}

using System.Collections.ObjectModel;
using System.Collections.Immutable;
using Oxide.App.Settings;
using Oxide.Core;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;

namespace Oxide.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int MaximumSourceHistoryEntries = 50;
    private const int MaximumStateEditPreviewCharacters = 4_000;
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
    private SourceNavigationResolution? lastSourceNavigationResolution;
    private SourceViewerViewModel? sourceViewer;
    private readonly List<SourceNavigationRequest> sourceNavigationHistory = [];
    private int sourceNavigationHistoryIndex = -1;
    private string? sourceRefreshNotice;
    private bool automaticRefreshEnabled = true;
    private bool explicitLoadActive;
    private WorkspaceRefreshCoordinatorStatus refreshStatus = new(
        WorkspaceRefreshCoordinatorState.Stopped,
        "Automatic refresh is not active.");
    private readonly WorkspaceEditWriter editWriter;
    private readonly WorkspaceEditUndoService undoService;
    private StateScalarProperty? stateEditProperty;
    private string stateEditOriginalValue = string.Empty;
    private string stateEditDraftValue = string.Empty;
    private StateScalarEditPlan? stateEditPlan;
    private bool isApplyingStateEdit;
    private string? stateEditFeedback;
    private WorkspaceEditUndoRecord? lastUndoRecord;

    public MainWindowViewModel()
        : this(new WorkspaceService(), new JsonApplicationSettingsStore(), ownsWorkspaceService: true)
    {
    }

    public MainWindowViewModel(
        IWorkspaceService workspaceService,
        IApplicationSettingsStore? settingsStore = null,
        bool ownsWorkspaceService = false,
        WorkspaceRefreshCoordinator? refreshCoordinator = null,
        Func<WorkspaceConfiguration, IWorkspaceChangeSource>? changeSourceFactory = null,
        WorkspaceEditWriter? editWriter = null)
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
        this.editWriter = editWriter ?? new WorkspaceEditWriter();
        undoService = new WorkspaceEditUndoService(this.editWriter);
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

    public bool HasSourceNavigationRequest => LastSourceNavigationRequest is not null || SourceRefreshNotice is not null;

    public string? SourceRefreshNotice
    {
        get => sourceRefreshNotice;
        private set
        {
            if (SetProperty(ref sourceRefreshNotice, value))
            {
                OnPropertyChanged(nameof(HasSourceNavigationRequest));
                OnPropertyChanged(nameof(SourceNavigationSummary));
            }
        }
    }

    public bool CanNavigateSourceBack => sourceNavigationHistoryIndex > 0;

    public bool CanNavigateSourceForward =>
        sourceNavigationHistoryIndex >= 0 && sourceNavigationHistoryIndex < sourceNavigationHistory.Count - 1;

    public string SourceHistorySummary => sourceNavigationHistoryIndex < 0
        ? "No source history"
        : $"{sourceNavigationHistoryIndex + 1:N0} of {sourceNavigationHistory.Count:N0}";

    public SourceViewerViewModel? SourceViewer
    {
        get => sourceViewer;
        private set
        {
            if (SetProperty(ref sourceViewer, value))
            {
                OnPropertyChanged(nameof(IsConceptWorkspaceVisible));
                OnPropertyChanged(nameof(IsSourceViewerVisible));
            }
        }
    }

    public SourceNavigationResolution? LastSourceNavigationResolution
    {
        get => lastSourceNavigationResolution;
        private set => SetProperty(ref lastSourceNavigationResolution, value);
    }

    public string SourceNavigationSummary => SourceRefreshNotice ?? (LastSourceNavigationRequest is null
        ? string.Empty
        : LastSourceNavigationResolution?.IsResolved is true
            ? $"Source target: {LastSourceNavigationRequest.VirtualPath} · {LastSourceNavigationRequest.Location}"
            : LastSourceNavigationResolution?.Message ?? "The source target could not be resolved.");

    public void RequestSourceNavigation(SourceNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        NavigateSource(request, recordHistory: true);
    }

    public void NavigateSourceBack()
    {
        if (!CanNavigateSourceBack) return;

        sourceNavigationHistoryIndex--;
        NavigateSource(sourceNavigationHistory[sourceNavigationHistoryIndex], recordHistory: false);
        NotifySourceHistoryChanged();
    }

    public void NavigateSourceForward()
    {
        if (!CanNavigateSourceForward) return;

        sourceNavigationHistoryIndex++;
        NavigateSource(sourceNavigationHistory[sourceNavigationHistoryIndex], recordHistory: false);
        NotifySourceHistoryChanged();
    }

    private void NavigateSource(SourceNavigationRequest request, bool recordHistory)
    {
        SourceRefreshNotice = null;
        LastSourceNavigationRequest = request;
        LastSourceNavigationResolution = snapshot is null
            ? null
            : SourceNavigationResolver.Resolve(snapshot, request.Target);
        OnPropertyChanged(nameof(SourceNavigationSummary));
        SourceViewer = snapshot is not null && LastSourceNavigationResolution?.IsResolved is true
            ? new SourceViewerViewModel(snapshot, request, LastSourceNavigationResolution)
            : null;
        if (recordHistory && SourceViewer is not null)
        {
            RecordSourceHistory(request);
        }

        SourceNavigationRequested?.Invoke(request);
    }

    private void RecordSourceHistory(SourceNavigationRequest request)
    {
        if (sourceNavigationHistoryIndex >= 0 &&
            sourceNavigationHistory[sourceNavigationHistoryIndex] == request)
        {
            return;
        }

        if (sourceNavigationHistoryIndex < sourceNavigationHistory.Count - 1)
        {
            sourceNavigationHistory.RemoveRange(
                sourceNavigationHistoryIndex + 1,
                sourceNavigationHistory.Count - sourceNavigationHistoryIndex - 1);
        }

        sourceNavigationHistory.Add(request);
        if (sourceNavigationHistory.Count > MaximumSourceHistoryEntries)
        {
            sourceNavigationHistory.RemoveAt(0);
        }

        sourceNavigationHistoryIndex = sourceNavigationHistory.Count - 1;
        NotifySourceHistoryChanged();
    }

    private void ClearSourceHistory()
    {
        sourceNavigationHistory.Clear();
        sourceNavigationHistoryIndex = -1;
        NotifySourceHistoryChanged();
    }

    private void NotifySourceHistoryChanged()
    {
        OnPropertyChanged(nameof(CanNavigateSourceBack));
        OnPropertyChanged(nameof(CanNavigateSourceForward));
        OnPropertyChanged(nameof(SourceHistorySummary));
    }

    public void CloseSourceViewer() => SourceViewer = null;

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
                OnPropertyChanged(nameof(IsConceptWorkspaceVisible));
                OnPropertyChanged(nameof(IsSourceViewerVisible));
            }
        }
    }

    public bool IsWelcomeVisible => Screen is ApplicationScreen.Welcome;

    public bool IsLoadingVisible => Screen is ApplicationScreen.Loading;

    public bool IsWorkspaceVisible => Screen is ApplicationScreen.Workspace;

    public bool IsConceptWorkspaceVisible => IsWorkspaceVisible && SourceViewer is null;

    public bool IsSourceViewerVisible => IsWorkspaceVisible && SourceViewer is not null;

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
                CloseStateEdit();
                OnPropertyChanged(nameof(HasSelectedState));
                OnPropertyChanged(nameof(HasNoSelectedState));
                NotifyStateEditingCapabilities();
            }
        }
    }

    public bool HasSelectedState => SelectedState is not null;

    public bool HasNoSelectedState => SelectedState is null;

    public bool CanEditSelectedStateManpower =>
        AssessSelectedStateEdit(StateScalarProperty.Manpower).IsEditable;

    public string EditSelectedStateManpowerExplanation =>
        AssessSelectedStateEdit(StateScalarProperty.Manpower).Explanation;

    public string SelectedStateManpowerEditHint => CanEditSelectedStateManpower
        ? "Editable active-mod value"
        : EditSelectedStateManpowerExplanation;

    public bool CanEditSelectedStateCategory =>
        AssessSelectedStateEdit(StateScalarProperty.StateCategory).IsEditable;

    public string EditSelectedStateCategoryExplanation =>
        AssessSelectedStateEdit(StateScalarProperty.StateCategory).Explanation;

    public string SelectedStateCategoryEditHint => CanEditSelectedStateCategory
        ? "Editable active-mod value"
        : EditSelectedStateCategoryExplanation;

    public bool IsStateEditOpen => stateEditProperty.HasValue;

    public string StateEditTitle => stateEditProperty switch
    {
        StateScalarProperty.Manpower => "Edit manpower",
        StateScalarProperty.StateCategory => "Edit state category",
        _ => "Edit state value",
    };

    public string StateEditPropertyLabel => stateEditProperty switch
    {
        StateScalarProperty.Manpower => "MANPOWER",
        StateScalarProperty.StateCategory => "STATE CATEGORY",
        _ => "VALUE",
    };

    public string StateEditOriginalValue => stateEditOriginalValue;

    public string StateEditDraftValue
    {
        get => stateEditDraftValue;
        set
        {
            if (SetProperty(ref stateEditDraftValue, value))
            {
                UpdateStateEditPlan();
            }
        }
    }

    public bool IsApplyingStateEdit
    {
        get => isApplyingStateEdit;
        private set
        {
            if (SetProperty(ref isApplyingStateEdit, value))
            {
                OnPropertyChanged(nameof(CanApplyStateEdit));
                OnPropertyChanged(nameof(CanUndoLastEdit));
            }
        }
    }

    public bool CanApplyStateEdit => stateEditPlan?.IsValid is true && !IsApplyingStateEdit;

    public string StateEditValidationMessage => stateEditPlan is null
        ? string.IsNullOrWhiteSpace(StateEditDraftValue)
            ? "A value is required."
            : "Enter a value to build a source preview."
        : stateEditPlan.IsValid
            ? "Ready to apply after one final live-file conflict check."
            : stateEditPlan.Capability.Explanation;

    public string StateEditSourcePath => stateEditPlan?.Edit?.Documents[0].Target.VirtualPath.Value ?? string.Empty;

    public string StateEditPreviewBefore => PreviewText(updated: false);

    public string StateEditPreviewAfter => PreviewText(updated: true);

    public string? StateEditFeedback
    {
        get => stateEditFeedback;
        private set
        {
            if (SetProperty(ref stateEditFeedback, value))
            {
                OnPropertyChanged(nameof(HasStateEditFeedback));
            }
        }
    }

    public bool HasStateEditFeedback => !string.IsNullOrWhiteSpace(StateEditFeedback);

    public bool CanUndoLastEdit => lastUndoRecord is not null && !IsApplyingStateEdit;

    public string UndoLastEditLabel => lastUndoRecord is null ? "Nothing to undo" : "Undo last edit";

    public void BeginStateEdit(StateScalarProperty property)
    {
        if (snapshot is null || SelectedState is null)
        {
            return;
        }

        var capability = StateScalarEditPlanner.Assess(snapshot, SelectedState.Id, property);
        if (!capability.IsEditable)
        {
            StateEditFeedback = capability.Explanation;
            return;
        }

        stateEditProperty = property;
        stateEditOriginalValue = property switch
        {
            StateScalarProperty.Manpower => SelectedState.Entity.Manpower!.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            StateScalarProperty.StateCategory => SelectedState.Entity.StateCategory!.Value,
            _ => string.Empty,
        };
        stateEditDraftValue = stateEditOriginalValue;
        stateEditPlan = null;
        StateEditFeedback = null;
        NotifyStateEditChanged();
        UpdateStateEditPlan();
    }

    public void CancelStateEdit() => CloseStateEdit();

    public async Task ApplyStateEditAsync()
    {
        if (snapshot is null || stateEditPlan is not { IsValid: true, Edit: { } edit })
        {
            return;
        }

        IsApplyingStateEdit = true;
        var resumeAutomaticRefresh = false;
        try
        {
            resumeAutomaticRefresh = await PauseAutomaticRefreshForMutationAsync();
            var result = await editWriter.ApplyAsync(snapshot, edit);
            if (!result.IsApplied)
            {
                StateEditFeedback = result.Issues.FirstOrDefault()?.Message ?? result.Message;
                UpdateStateEditPlan();
                return;
            }

            var editedProperty = StateEditPropertyLabel.ToLowerInvariant();
            SetUndoRecord(result.UndoRecord);
            CloseStateEdit();
            StateEditFeedback = $"Saved {editedProperty}. Reloading the workspace…";
            await ReloadAfterMutationAsync();
            StateEditFeedback = $"Saved {editedProperty}.";
        }
        catch (Exception exception)
        {
            StateEditFeedback = $"Oxide could not apply the edit: {exception.Message}";
        }
        finally
        {
            if (resumeAutomaticRefresh)
            {
                await ResumeAutomaticRefreshAfterMutationAsync();
            }

            IsApplyingStateEdit = false;
        }
    }

    public async Task UndoLastEditAsync()
    {
        if (snapshot is null || lastUndoRecord is null || IsApplyingStateEdit)
        {
            return;
        }

        IsApplyingStateEdit = true;
        var resumeAutomaticRefresh = false;
        try
        {
            resumeAutomaticRefresh = await PauseAutomaticRefreshForMutationAsync();
            var result = await undoService.RestoreAsync(snapshot, lastUndoRecord);
            if (!result.IsRestored)
            {
                StateEditFeedback = result.Issues.FirstOrDefault()?.Message ?? result.Message;
                return;
            }

            SetUndoRecord(null);
            StateEditFeedback = "Undo restored the exact source bytes. Reloading the workspace…";
            await ReloadAfterMutationAsync();
            StateEditFeedback = "Undid the last edit.";
        }
        catch (Exception exception)
        {
            StateEditFeedback = $"Oxide could not undo the edit: {exception.Message}";
        }
        finally
        {
            if (resumeAutomaticRefresh)
            {
                await ResumeAutomaticRefreshAfterMutationAsync();
            }

            IsApplyingStateEdit = false;
        }
    }

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
        CloseStateEdit();
        SetUndoRecord(null);
        StateEditFeedback = null;
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
        var editPreviewWasOpen = IsStateEditOpen;
        var previousStateId = SelectedState?.Id;
        var previousCountryTag = SelectedCountry?.Tag;
        var previousSnapshot = snapshot;
        var previousHistory = sourceNavigationHistory.ToArray();
        var previousHistoryIndex = sourceNavigationHistoryIndex;
        var previousRequest = LastSourceNavigationRequest;
        var sourceViewerWasOpen = SourceViewer is not null;
        var previousSearch = SourceViewer?.SearchText;
        if (previousSnapshot is not null && !IsSameWorkspace(previousSnapshot, loadedSnapshot))
        {
            SetUndoRecord(null);
        }

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
        if (editPreviewWasOpen)
        {
            StateEditFeedback = "The edit preview was closed because the workspace refreshed. Review the current value before editing again.";
        }
        RestoreSourceNavigation(
            previousSnapshot,
            loadedSnapshot,
            previousHistory,
            previousHistoryIndex,
            previousRequest,
            sourceViewerWasOpen,
            previousSearch);
        OnPropertyChanged(nameof(WorkspaceName));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
    }

    private static bool IsSameWorkspace(WorkspaceSnapshot first, WorkspaceSnapshot second) =>
        string.Equals(first.Configuration.GameRoot, second.Configuration.GameRoot, StringComparison.Ordinal) &&
        string.Equals(first.Configuration.ActiveModRoot, second.Configuration.ActiveModRoot, StringComparison.Ordinal);

    private void RestoreSourceNavigation(
        WorkspaceSnapshot? previousSnapshot,
        WorkspaceSnapshot loadedSnapshot,
        IReadOnlyList<SourceNavigationRequest> previousHistory,
        int previousHistoryIndex,
        SourceNavigationRequest? previousRequest,
        bool sourceViewerWasOpen,
        string? previousSearch)
    {
        LastSourceNavigationRequest = null;
        LastSourceNavigationResolution = null;
        SourceViewer = null;
        SourceRefreshNotice = null;
        ClearSourceHistory();
        if (previousSnapshot is null || !IsSameWorkspace(previousSnapshot.Configuration, loadedSnapshot.Configuration))
        {
            return;
        }

        for (var index = 0; index < previousHistory.Count; index++)
        {
            if (!SourceRelationshipProjector.TryRemap(loadedSnapshot, previousHistory[index], out var remapped, out _))
            {
                continue;
            }

            sourceNavigationHistory.Add(remapped);
            if (index <= previousHistoryIndex)
            {
                sourceNavigationHistoryIndex = sourceNavigationHistory.Count - 1;
            }
        }

        NotifySourceHistoryChanged();
        if (previousRequest is null || !sourceViewerWasOpen)
        {
            return;
        }

        if (!SourceRelationshipProjector.TryRemap(
                loadedSnapshot,
                previousRequest,
                out var current,
                out var failureReason))
        {
            if (sourceNavigationHistoryIndex < sourceNavigationHistory.Count - 1)
            {
                sourceNavigationHistory.RemoveRange(
                    sourceNavigationHistoryIndex + 1,
                    sourceNavigationHistory.Count - sourceNavigationHistoryIndex - 1);
                NotifySourceHistoryChanged();
            }

            SourceRefreshNotice = $"The previous source view became stale after refresh. {failureReason}";
            return;
        }

        LastSourceNavigationRequest = current;
        LastSourceNavigationResolution = SourceNavigationResolver.Resolve(loadedSnapshot, current.Target);
        SourceViewer = new SourceViewerViewModel(loadedSnapshot, current, LastSourceNavigationResolution);
        if (!string.IsNullOrEmpty(previousSearch))
        {
            SourceViewer.SearchText = previousSearch;
        }
    }

    private static bool IsSameWorkspace(WorkspaceConfiguration left, WorkspaceConfiguration right)
    {
        if (left.Layers.Length != right.Layers.Length)
        {
            return false;
        }

        return left.Layers.Zip(right.Layers).All(pair =>
            pair.First.Id == pair.Second.Id &&
            pair.First.Kind == pair.Second.Kind &&
            pair.First.Position == pair.Second.Position &&
            pair.First.IsEnabled == pair.Second.IsEnabled &&
            string.Equals(pair.First.RootPath, pair.Second.RootPath, StringComparison.Ordinal) &&
            pair.First.ReplacementRules.SequenceEqual(pair.Second.ReplacementRules));
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

    private EditCapability AssessSelectedStateEdit(StateScalarProperty property)
    {
        if (snapshot is null || SelectedState is null)
        {
            return EditCapability.Refused(EditRefusalReason.MissingProvenance, "Select a state first.");
        }

        return StateScalarEditPlanner.Assess(snapshot, SelectedState.Id, property);
    }

    private void UpdateStateEditPlan()
    {
        stateEditPlan = null;
        if (snapshot is not null &&
            SelectedState is not null &&
            stateEditProperty is { } property &&
            !string.IsNullOrWhiteSpace(StateEditDraftValue))
        {
            var intent = new StateScalarEditIntent(SelectedState.Id, property, StateEditDraftValue);
            stateEditPlan = StateScalarEditPlanner.Plan(snapshot, intent);
        }

        OnPropertyChanged(nameof(CanApplyStateEdit));
        OnPropertyChanged(nameof(StateEditValidationMessage));
        OnPropertyChanged(nameof(StateEditSourcePath));
        OnPropertyChanged(nameof(StateEditPreviewBefore));
        OnPropertyChanged(nameof(StateEditPreviewAfter));
    }

    private string PreviewText(bool updated)
    {
        if (stateEditPlan?.PreparedEdit?.Documents is not [{ } document])
        {
            return string.Empty;
        }

        var source = updated ? document.UpdatedSource : document.OriginalSource;
        var change = document.Edit.Changes[0];
        var offset = Math.Min(change.Span.Start, source.Length);
        var position = source.GetPosition(offset);
        var firstLine = Math.Max(0, position.Line - 2);
        var lastLine = Math.Min(source.LineCount - 1, position.Line + 2);
        var span = Oxide.Syntax.Text.TextSpan.FromBounds(
            source.GetLineFullSpan(firstLine).Start,
            source.GetLineFullSpan(lastLine).End);
        if (span.Length <= MaximumStateEditPreviewCharacters)
        {
            return source.GetText(span);
        }

        var previewStart = Math.Clamp(
            offset - MaximumStateEditPreviewCharacters / 2,
            span.Start,
            span.End - MaximumStateEditPreviewCharacters);
        var hasPrefix = previewStart > span.Start;
        var previewLength = MaximumStateEditPreviewCharacters - (hasPrefix ? 1 : 0);
        var hasSuffix = previewStart + previewLength < span.End;
        if (hasSuffix)
        {
            previewLength--;
        }

        var preview = source.GetText(new Oxide.Syntax.Text.TextSpan(
            previewStart,
            previewLength));
        return $"{(hasPrefix ? "…" : string.Empty)}{preview}{(hasSuffix ? "…" : string.Empty)}";
    }

    private void CloseStateEdit()
    {
        if (!stateEditProperty.HasValue && stateEditPlan is null)
        {
            return;
        }

        stateEditProperty = null;
        stateEditOriginalValue = string.Empty;
        stateEditDraftValue = string.Empty;
        stateEditPlan = null;
        NotifyStateEditChanged();
    }

    private void NotifyStateEditingCapabilities()
    {
        OnPropertyChanged(nameof(CanEditSelectedStateManpower));
        OnPropertyChanged(nameof(EditSelectedStateManpowerExplanation));
        OnPropertyChanged(nameof(SelectedStateManpowerEditHint));
        OnPropertyChanged(nameof(CanEditSelectedStateCategory));
        OnPropertyChanged(nameof(EditSelectedStateCategoryExplanation));
        OnPropertyChanged(nameof(SelectedStateCategoryEditHint));
    }

    private void NotifyStateEditChanged()
    {
        OnPropertyChanged(nameof(IsStateEditOpen));
        OnPropertyChanged(nameof(StateEditTitle));
        OnPropertyChanged(nameof(StateEditPropertyLabel));
        OnPropertyChanged(nameof(StateEditOriginalValue));
        OnPropertyChanged(nameof(StateEditDraftValue));
        OnPropertyChanged(nameof(CanApplyStateEdit));
        OnPropertyChanged(nameof(StateEditValidationMessage));
        OnPropertyChanged(nameof(StateEditSourcePath));
        OnPropertyChanged(nameof(StateEditPreviewBefore));
        OnPropertyChanged(nameof(StateEditPreviewAfter));
    }

    private void SetUndoRecord(WorkspaceEditUndoRecord? undoRecord)
    {
        lastUndoRecord = undoRecord;
        OnPropertyChanged(nameof(CanUndoLastEdit));
        OnPropertyChanged(nameof(UndoLastEditLabel));
    }

    private async Task<bool> PauseAutomaticRefreshForMutationAsync()
    {
        if (!AutomaticRefreshEnabled || refreshCoordinator is null)
        {
            return false;
        }

        await refreshCoordinator.StopAsync();
        ApplyRefreshStatus(refreshCoordinator.Status);
        return true;
    }

    private async Task ReloadAfterMutationAsync() => await LoadAsync(
        (progress, cancellation) => workspaceService.ReloadAsync(progress, cancellation),
        startAutomaticRefresh: false);

    private async Task ResumeAutomaticRefreshAfterMutationAsync()
    {
        if (snapshot is not null && AutomaticRefreshEnabled)
        {
            await StartAutomaticRefreshAsync(snapshot.Configuration);
        }
    }

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

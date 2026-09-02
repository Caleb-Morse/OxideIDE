using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Oxide.App.Settings;
using Oxide.App.ViewModels;
using Oxide.Core.Workspaces.Editing;

namespace Oxide.App;

public partial class MainView : Window
{
    public MainView() : this(new MainWindowViewModel())
    {
    }

    public MainView(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Title = viewModel.ApplicationName;
        ViewModel.ThemeChanged += ApplyTheme;
        Opened += async (_, _) => await ViewModel.InitializeAsync();
        Closing += (_, _) =>
        {
            ViewModel.ThemeChanged -= ApplyTheme;
            ViewModel.Dispose();
        };
    }

    public MainWindowViewModel ViewModel { get; }

    private async void BrowseGameRoot_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Choose your Hearts of Iron IV installation");
        if (path is not null) ViewModel.GameRootPath = path;
    }

    private async void BrowseModRoot_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Choose your active mod folder");
        if (path is not null) ViewModel.ActiveModRootPath = path;
    }

    private void ClearModRoot_Click(object? sender, RoutedEventArgs e) => ViewModel.ClearActiveMod();
    private async void OpenWorkspace_Click(object? sender, RoutedEventArgs e) => await ViewModel.OpenWorkspaceAsync();
    private async void Reload_Click(object? sender, RoutedEventArgs e) => await ViewModel.ReloadAsync();
    private void CancelLoading_Click(object? sender, RoutedEventArgs e) => ViewModel.CancelLoading();
    private async void ChangeWorkspace_Click(object? sender, RoutedEventArgs e) => await ViewModel.ShowWelcomeAsync();
    private async void ToggleTheme_Click(object? sender, RoutedEventArgs e) => await ViewModel.ToggleThemeAsync();
    private void DismissError_Click(object? sender, RoutedEventArgs e) => ViewModel.DismissError();

    private async void Language_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: LanguageOptionViewModel language } &&
            language.Id != ViewModel.SelectedLanguage)
        {
            await ViewModel.ChangeLanguageAsync(language.Id);
        }
    }

    private async void EnglishFallback_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { IsChecked: { } enabled })
        {
            await ViewModel.SetEnglishFallbackAsync(enabled);
        }
    }

    private async void AutomaticRefresh_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { IsChecked: { } enabled })
        {
            await ViewModel.SetAutomaticRefreshAsync(enabled);
        }
    }

    private void State_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedState is not null) ViewModel.ShowStateDetails();
    }

    private void EditStateCategory_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.BeginStateEdit(StateScalarProperty.StateCategory);
        FocusStateEditValue();
    }

    private void EditStateManpower_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.BeginStateEdit(StateScalarProperty.Manpower);
        FocusStateEditValue();
    }

    private void CancelStateEdit_Click(object? sender, RoutedEventArgs e) => ViewModel.CancelStateEdit();

    private async void ApplyStateEdit_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.ApplyStateEditAsync();

    private async void UndoLastEdit_Click(object? sender, RoutedEventArgs e) =>
        await ViewModel.UndoLastEditAsync();

    private void FocusStateEditValue() => Dispatcher.UIThread.Post(() =>
    {
        if (!ViewModel.IsStateEditOpen) return;
        StateEditValueTextBox.Focus();
        StateEditValueTextBox.SelectAll();
    }, DispatcherPriority.Loaded);

    private void Country_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedCountry is not null) ViewModel.ShowCountryDetails();
    }

    private void CountryState_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int stateId })
        {
            ViewModel.SelectStateFromCountry(stateId);
            ViewModel.ShowStateDetails();
        }
    }

    private void OpenSource_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SourceNavigationRequest request })
        {
            e.Handled = true;
            ViewModel.RequestSourceNavigation(request);
            ApplySourceSelection();
        }
    }

    private void CloseSourceViewer_Click(object? sender, RoutedEventArgs e) => ViewModel.CloseSourceViewer();

    private void SourceHistoryBack_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.NavigateSourceBack();
        ApplySourceSelection();
    }

    private void SourceHistoryForward_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.NavigateSourceForward();
        ApplySourceSelection();
    }

    private void OpenRelatedSource_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SourceNavigationRequest request })
        {
            ViewModel.RequestSourceNavigation(request);
            ApplySourceSelection();
        }
    }

    private void SourceFindNext_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SourceViewer?.FindNext() is true) ApplySourceSelection();
    }

    private void SourceFindPrevious_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SourceViewer?.FindPrevious() is true) ApplySourceSelection();
    }

    private void SourceViewer_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!ViewModel.IsSourceViewerVisible)
        {
            return;
        }

        var commandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                              e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (commandModifier && e.Key is Key.F)
        {
            SourceFindTextBox.Focus();
            SourceFindTextBox.SelectAll();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key is Key.Left && ViewModel.CanNavigateSourceBack)
        {
            ViewModel.NavigateSourceBack();
            ApplySourceSelection();
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key is Key.Right && ViewModel.CanNavigateSourceForward)
        {
            ViewModel.NavigateSourceForward();
            ApplySourceSelection();
            e.Handled = true;
        }
        else if (e.Key is Key.Escape)
        {
            ViewModel.CloseSourceViewer();
            e.Handled = true;
        }
    }

    private void SourceDiagnostic_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplySourceSelection();

    private async void CopyFullSource_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SourceViewer is not { } sourceViewer || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(sourceViewer.FullText);
    }

    private void ApplySourceSelection()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel.SourceViewer is not { } sourceViewer) return;

            SourceTextBlock.SelectionStart = sourceViewer.SelectionStart;
            SourceTextBlock.SelectionEnd = sourceViewer.SelectionEnd;
            SourceTextBlock.Focus();
        }, DispatcherPriority.Loaded);
    }

    private static void ApplyTheme(OxideTheme theme)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = theme is OxideTheme.IronRustDark
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });
            return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
        }
        catch (Exception exception)
        {
            ViewModel.ReportError($"Oxide could not open the folder picker: {exception.Message}");
            return null;
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Oxide.App.ViewModels;

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
        Closing += (_, _) => ViewModel.Dispose();
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
    private void ChangeWorkspace_Click(object? sender, RoutedEventArgs e) => ViewModel.ShowWelcome();

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}

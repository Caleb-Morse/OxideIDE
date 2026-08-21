using Oxide.App.ViewModels;

namespace Oxide.App.Composition;

/// <summary>
/// Creates application-level services and presentation objects.
/// </summary>
internal static class ApplicationComposition
{
    public static MainView CreateMainView()
    {
        return new MainView(new MainWindowViewModel());
    }
}

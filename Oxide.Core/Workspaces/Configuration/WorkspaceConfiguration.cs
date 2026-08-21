namespace Oxide.Core.Workspaces.Configuration;

public sealed record WorkspaceConfiguration
{
    public WorkspaceConfiguration(string gameRoot, string? activeModRoot = null, string? displayName = null)
    {
        GameRoot = NormalizeRoot(gameRoot, nameof(gameRoot));
        ActiveModRoot = string.IsNullOrWhiteSpace(activeModRoot)
            ? null
            : NormalizeRoot(activeModRoot, nameof(activeModRoot));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(ActiveModRoot ?? GameRoot)
            : displayName.Trim();
    }

    public string GameRoot { get; }

    public string? ActiveModRoot { get; }

    public string DisplayName { get; }

    private static string NormalizeRoot(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}

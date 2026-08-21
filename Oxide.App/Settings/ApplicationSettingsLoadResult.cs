namespace Oxide.App.Settings;

public sealed record ApplicationSettingsLoadResult(
    ApplicationSettings Settings,
    string? Warning = null);

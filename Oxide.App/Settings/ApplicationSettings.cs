namespace Oxide.App.Settings;

public sealed record ApplicationSettings(
    int SchemaVersion = 1,
    string? LastGameRoot = null,
    string? LastActiveModRoot = null,
    OxideTheme Theme = OxideTheme.IronRustDark,
    string PreferredLanguage = "english",
    bool EnglishFallbackEnabled = true);

using Oxide.App.Settings;

namespace Oxide.Tests.Application;

public sealed class ApplicationSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "oxide-settings-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Settings_round_trip_workspace_paths_and_material_theme_atomically()
    {
        var path = Path.Combine(directory, "settings.json");
        var store = new JsonApplicationSettingsStore(path);
        var expected = new ApplicationSettings(
            LastGameRoot: "/game",
            LastActiveModRoot: "/mod",
            Theme: OxideTheme.CopperVerdigrisLight,
            PreferredLanguage: "russian",
            EnglishFallbackEnabled: false);

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        Assert.Equal(expected, result.Settings);
        Assert.Null(result.Warning);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Older_settings_receive_language_policy_defaults()
    {
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, """
            {
              "SchemaVersion": 1,
              "LastGameRoot": "/game",
              "Theme": 0
            }
            """);
        var store = new JsonApplicationSettingsStore(path);

        var result = await store.LoadAsync();

        Assert.Null(result.Warning);
        Assert.Equal("english", result.Settings.PreferredLanguage);
        Assert.True(result.Settings.EnglishFallbackEnabled);
    }

    [Fact]
    public async Task Corrupt_settings_fall_back_to_iron_rust_defaults_with_a_warning()
    {
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "{ this is not json");
        var store = new JsonApplicationSettingsStore(path);

        var result = await store.LoadAsync();

        Assert.Equal(new ApplicationSettings(), result.Settings);
        Assert.Equal(OxideTheme.IronRustDark, result.Settings.Theme);
        Assert.NotNull(result.Warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

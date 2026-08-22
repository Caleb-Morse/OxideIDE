using System.Text.Json;

namespace Oxide.App.Settings;

public sealed class JsonApplicationSettingsStore : IApplicationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string settingsPath;

    public JsonApplicationSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? GetDefaultPath();
    }

    public async Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            return new ApplicationSettingsLoadResult(new ApplicationSettings());
        }

        try
        {
            await using var stream = File.OpenRead(settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream,
                JsonOptions,
                cancellationToken);
            if (settings is null || settings.SchemaVersion != 1 || !Enum.IsDefined(settings.Theme))
            {
                return InvalidSettings("The saved settings use an unsupported format.");
            }

            return new ApplicationSettingsLoadResult(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return InvalidSettings($"Oxide could not read its saved settings: {exception.Message}");
        }
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("The settings path must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = settingsPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ApplicationSettingsLoadResult InvalidSettings(string warning) =>
        new(new ApplicationSettings(), warning);

    private static string GetDefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Oxide",
        "settings.json");
}

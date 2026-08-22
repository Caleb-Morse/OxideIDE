namespace Oxide.App.Settings;

public interface IApplicationSettingsStore
{
    Task<ApplicationSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}

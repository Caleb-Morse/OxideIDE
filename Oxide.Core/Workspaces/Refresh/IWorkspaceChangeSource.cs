namespace Oxide.Core.Workspaces.Refresh;

public interface IWorkspaceChangeSource : IAsyncDisposable
{
    event Action<WorkspaceChangeBatch>? ChangesAvailable;

    event Action<WorkspaceChangeSourceError>? Error;

    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

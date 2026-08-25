using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Tests.Workspaces;

internal sealed class DeterministicWorkspaceChangeSource : IWorkspaceChangeSource
{
    public event Action<WorkspaceChangeBatch>? ChangesAvailable;

    public event Action<WorkspaceChangeSourceError>? Error;

    public bool IsRunning { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void Emit(WorkspaceChangeBatch batch)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("The deterministic change source is not running.");
        }

        ChangesAvailable?.Invoke(batch);
    }

    public void EmitError(WorkspaceChangeSourceError error)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("The deterministic change source is not running.");
        }

        Error?.Invoke(error);
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

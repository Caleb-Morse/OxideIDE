namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceRefreshCoordinatorOptions
{
    public const int DefaultQueueCapacity = 32;

    public WorkspaceRefreshCoordinatorOptions(int queueCapacity = DefaultQueueCapacity)
    {
        if (queueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), "The queue capacity must be positive.");
        }

        QueueCapacity = queueCapacity;
    }

    public int QueueCapacity { get; }
}

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceChangeSourceOptions
{
    public WorkspaceChangeSourceOptions(TimeSpan? debounceInterval = null, int queueCapacity = 1024)
    {
        DebounceInterval = debounceInterval ?? TimeSpan.FromMilliseconds(200);
        if (DebounceInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceInterval));
        }

        if (queueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        QueueCapacity = queueCapacity;
    }

    public TimeSpan DebounceInterval { get; }

    public int QueueCapacity { get; }
}

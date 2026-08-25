using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceChange
{
    public WorkspaceChange(
        WorkspaceChangeKind kind,
        SourceIdentity? previousSource,
        SourceIdentity? currentSource,
        DateTimeOffset observedAt,
        WorkspaceChangeOrigin origin)
    {
        ValidateSources(kind, previousSource, currentSource);
        if (previousSource is not null
            && currentSource is not null
            && previousSource.LayerId != currentSource.LayerId)
        {
            throw new ArgumentException("One workspace change cannot cross content layers.");
        }

        Kind = kind;
        PreviousSource = previousSource;
        CurrentSource = currentSource;
        ObservedAt = observedAt;
        Origin = origin;
    }

    public WorkspaceChangeKind Kind { get; }

    public SourceIdentity? PreviousSource { get; }

    public SourceIdentity? CurrentSource { get; }

    public DateTimeOffset ObservedAt { get; }

    public WorkspaceChangeOrigin Origin { get; }

    public SourceIdentity Source => CurrentSource ?? PreviousSource!;

    private static void ValidateSources(
        WorkspaceChangeKind kind,
        SourceIdentity? previousSource,
        SourceIdentity? currentSource)
    {
        var isValid = kind switch
        {
            WorkspaceChangeKind.Created => previousSource is null && currentSource is not null,
            WorkspaceChangeKind.Changed => previousSource is not null && currentSource is not null,
            WorkspaceChangeKind.Deleted => previousSource is not null && currentSource is null,
            WorkspaceChangeKind.Renamed => previousSource is not null && currentSource is not null,
            WorkspaceChangeKind.Uncertain => previousSource is not null || currentSource is not null,
            _ => false,
        };
        if (!isValid)
        {
            throw new ArgumentException($"Change kind '{kind}' has an invalid previous/current source shape.");
        }
    }
}

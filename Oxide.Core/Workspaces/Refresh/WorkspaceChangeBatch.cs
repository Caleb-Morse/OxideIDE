using System.Collections.Immutable;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceChangeBatch
{
    public WorkspaceChangeBatch(
        IEnumerable<DocumentChange> changes,
        bool requiresFullRescan = false,
        string? fullRescanReason = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Changes = changes
            .OrderBy(change => change.Change.ObservedAt)
            .ThenBy(change => change.Change.Source.LayerId.Value, StringComparer.Ordinal)
            .ThenBy(change => change.Change.Source.VirtualPath)
            .ThenBy(change => change.Change.Kind)
            .ToImmutableArray();
        RequiresFullRescan = requiresFullRescan
            || Changes.Any(change => change.Change.Kind is WorkspaceChangeKind.Uncertain);
        FullRescanReason = string.IsNullOrWhiteSpace(fullRescanReason)
            ? Changes.Any(change => change.Change.Kind is WorkspaceChangeKind.Uncertain)
                ? "The change source reported an uncertain filesystem state."
                : null
            : fullRescanReason.Trim();
        if (RequiresFullRescan && FullRescanReason is null)
        {
            throw new ArgumentException("A full rescan request must include a reason.", nameof(fullRescanReason));
        }
    }

    public ImmutableArray<DocumentChange> Changes { get; }

    public bool RequiresFullRescan { get; }

    public string? FullRescanReason { get; }

    public DateTimeOffset? FirstObservedAt => Changes.IsEmpty ? null : Changes[0].Change.ObservedAt;

    public DateTimeOffset? LastObservedAt => Changes.IsEmpty ? null : Changes[^1].Change.ObservedAt;
}

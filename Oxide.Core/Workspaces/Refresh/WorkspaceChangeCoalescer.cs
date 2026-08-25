using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public static class WorkspaceChangeCoalescer
{
    public static WorkspaceChangeBatch Coalesce(
        IEnumerable<DocumentChange> changes,
        int? rawEventCount = null,
        bool requiresFullRescan = false,
        string? fullRescanReason = null)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var ordered = changes
            .OrderBy(change => change.Change.ObservedAt)
            .ThenBy(change => change.Change.Source.LayerId.Value, StringComparer.Ordinal)
            .ThenBy(change => change.Change.Source.VirtualPath)
            .ToArray();
        var byIdentity = new Dictionary<DocumentId, DocumentChange>();
        var renames = new List<DocumentChange>();
        foreach (var change in ordered)
        {
            if (change.Change.Kind is WorkspaceChangeKind.Renamed)
            {
                renames.Add(change);
                continue;
            }

            var identity = change.Change.Source.DocumentId;
            if (!byIdentity.TryGetValue(identity, out var previous))
            {
                byIdentity.Add(identity, change);
                continue;
            }

            var combined = Combine(previous, change);
            if (combined is null)
            {
                byIdentity.Remove(identity);
            }
            else
            {
                byIdentity[identity] = combined;
            }
        }

        var result = byIdentity.Values.Concat(renames).ToArray();
        return new WorkspaceChangeBatch(
            result,
            requiresFullRescan,
            fullRescanReason,
            rawEventCount ?? ordered.Length);
    }

    private static DocumentChange? Combine(DocumentChange previous, DocumentChange current)
    {
        var previousKind = previous.Change.Kind;
        var currentKind = current.Change.Kind;
        if (previousKind is WorkspaceChangeKind.Created && currentKind is WorkspaceChangeKind.Deleted)
        {
            return null;
        }

        var combinedKind = (previousKind, currentKind) switch
        {
            (WorkspaceChangeKind.Created, WorkspaceChangeKind.Changed) => WorkspaceChangeKind.Created,
            (WorkspaceChangeKind.Changed, WorkspaceChangeKind.Changed) => WorkspaceChangeKind.Changed,
            (WorkspaceChangeKind.Changed, WorkspaceChangeKind.Deleted) => WorkspaceChangeKind.Deleted,
            (WorkspaceChangeKind.Deleted, WorkspaceChangeKind.Created) => WorkspaceChangeKind.Changed,
            (_, WorkspaceChangeKind.Uncertain) => WorkspaceChangeKind.Uncertain,
            _ => currentKind,
        };
        var previousSource = combinedKind is WorkspaceChangeKind.Created
            ? null
            : previous.Change.PreviousSource ?? current.Change.PreviousSource ?? current.Change.CurrentSource;
        var currentSource = combinedKind is WorkspaceChangeKind.Deleted
            ? null
            : current.Change.CurrentSource ?? previous.Change.CurrentSource;
        return new DocumentChange(
            new WorkspaceChange(
                combinedKind,
                previousSource,
                currentSource,
                current.Change.ObservedAt,
                current.Change.Origin),
            current.DocumentKind,
            current.Category);
    }
}

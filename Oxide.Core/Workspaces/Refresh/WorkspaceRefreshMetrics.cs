namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceRefreshMetrics(
    int RawEventCount,
    int CoalescedChangeCount,
    int DocumentsAdded,
    int DocumentsChanged,
    int DocumentsRemoved,
    int DocumentsReused,
    int DocumentsReparsed,
    int DomainsRebuilt,
    int DomainsReused,
    bool UsedFullRescan,
    double DebounceMilliseconds,
    double DiscoveryMilliseconds,
    double DocumentLoadingMilliseconds,
    double SemanticMilliseconds,
    double PublicationMilliseconds,
    double TotalMilliseconds);

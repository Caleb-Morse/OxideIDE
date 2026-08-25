using System.Collections.Immutable;

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
    double TotalMilliseconds)
{
    public ImmutableArray<SemanticRefreshDomain> RebuiltSemanticDomains { get; init; } = [];

    public ImmutableArray<SemanticRefreshDomain> ReusedSemanticDomains { get; init; } = [];
}

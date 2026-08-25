using System.Collections.Immutable;

namespace Oxide.Core.Verification;

public sealed record IncrementalRefreshCorpusSummary(
    string Outcome,
    string Trigger,
    int RawEventCount,
    int CoalescedChangeCount,
    int DocumentsReparsed,
    int DocumentsReused,
    bool UsedFullRescan,
    ImmutableArray<string> RebuiltSemanticDomains,
    ImmutableArray<string> ReusedSemanticDomains,
    double DocumentLoadingMilliseconds,
    double SemanticMilliseconds,
    double PublicationMilliseconds,
    double TotalMilliseconds);

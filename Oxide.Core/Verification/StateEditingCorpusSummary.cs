using System.Collections.Immutable;

namespace Oxide.Core.Verification;

public sealed record StateEditingCorpusSummary(
    int StateCount,
    EditingCapabilityCounts Manpower,
    EditingCapabilityCounts StateCategory,
    int EditableForBoth);

public sealed record EditingCapabilityCounts(
    int Total,
    int Editable,
    ImmutableSortedDictionary<string, int> RefusalsByReason)
{
    public int Refused => Total - Editable;
}

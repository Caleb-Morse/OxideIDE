using System.Collections.Immutable;

namespace Oxide.Core.Semantics.Model;

public sealed record EffectiveValue<T>(
    T Value,
    SourceProvenance Provenance,
    string SelectionReason,
    ImmutableArray<SourceProvenance> IgnoredCandidates)
{
    public static EffectiveValue<T> FromSingle(SourcedValue<T> value) =>
        new(value.Value, value.Provenance, "Single unambiguous declaration", []);
}

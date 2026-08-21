namespace Oxide.Core.Semantics.Model;

public sealed record SourcedValue<T>(T Value, string OriginalText, SourceProvenance Provenance);

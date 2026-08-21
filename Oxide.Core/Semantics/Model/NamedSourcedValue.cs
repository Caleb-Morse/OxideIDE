namespace Oxide.Core.Semantics.Model;

public sealed record NamedSourcedValue<T>(string Name, SourcedValue<T> Value);

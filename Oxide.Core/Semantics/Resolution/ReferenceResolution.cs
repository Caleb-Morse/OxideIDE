using System.Collections.Immutable;

namespace Oxide.Core.Semantics.Resolution;

public abstract record ReferenceResolution<T>;

public sealed record ResolvedReference<T>(T Target) : ReferenceResolution<T>;

public sealed record MissingReference<T>(string CandidateKey) : ReferenceResolution<T>;

public sealed record AmbiguousReference<T>(
    string CandidateKey,
    ImmutableArray<T> Candidates,
    string Reason) : ReferenceResolution<T>;

public sealed record InvalidReference<T>(string Reason) : ReferenceResolution<T>;

using Oxide.Core.Semantics.Model;
using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;

namespace Oxide.Core.Semantics.Resolution;

public sealed record CountryReference(
    string OriginalTag,
    SourceProvenance Provenance,
    CountryResolution Resolution);

public abstract record CountryResolution;

public sealed record ResolvedCountry(CountryEntity Target) : CountryResolution;

public sealed record MissingCountry(string CandidateTag) : CountryResolution;

public sealed record AmbiguousCountry(
    string CandidateTag,
    ImmutableArray<CountryTagDeclaration> Candidates,
    string Reason) : CountryResolution;

public sealed record InvalidCountry(string Reason) : CountryResolution;

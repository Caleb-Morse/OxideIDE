using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Resolution;

public abstract record LocalisationLookupResult;

public abstract record LocalisationResolution(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key) : LocalisationLookupResult;

public sealed record ResolvedLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    LocalisationDeclaration Declaration,
    string SelectionReason)
    : LocalisationResolution(RequestedLanguage, Key)
{
    public string Value => Declaration.Value.Value;

    public SourceProvenance Provenance => Declaration.Value.Provenance;
}

public sealed record MissingLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    ImmutableArray<LocalisationLanguage> LanguagesTried)
    : LocalisationResolution(RequestedLanguage, Key);

public sealed record AmbiguousLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    LocalisationLanguage CandidateLanguage,
    ImmutableArray<LocalisationDeclaration> Candidates)
    : LocalisationResolution(RequestedLanguage, Key);

public sealed record InvalidLocalisation(string Language, string Key, string Reason) : LocalisationLookupResult;

public sealed record HumanReadableName(
    string DisplayText,
    string FallbackText,
    LocalisationLookupResult? Resolution);

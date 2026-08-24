using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Resolution;

public abstract record LocalisationLookupResult;

public abstract record LocalisationResolution(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    ImmutableArray<LocalisationResolutionAttempt> Attempts) : LocalisationLookupResult;

public sealed record LocalisationResolutionAttempt(
    LocalisationLanguage CandidateLanguage,
    ContributionResolution<LocalisationIdentity, LocalisationDeclaration>? ContributionResolution)
{
    public bool WasMissing => ContributionResolution is null
        || ContributionResolution.Kind is ContributionResolutionKind.Missing;
}

public sealed record ResolvedLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    LocalisationDeclaration Declaration,
    string SelectionReason,
    ContributionResolution<LocalisationIdentity, LocalisationDeclaration> ContributionResolution,
    ImmutableArray<LocalisationResolutionAttempt> Attempts)
    : LocalisationResolution(RequestedLanguage, Key, Attempts)
{
    public string Value => Declaration.Value.Value;

    public SourceProvenance Provenance => Declaration.Value.Provenance;

    public LocalisationLanguage ResolvedLanguage => Declaration.Language;

    public bool IsFallback => RequestedLanguage != ResolvedLanguage;
}

public sealed record MissingLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    ImmutableArray<LocalisationLanguage> LanguagesTried,
    ImmutableArray<LocalisationResolutionAttempt> Attempts)
    : LocalisationResolution(RequestedLanguage, Key, Attempts);

public sealed record AmbiguousLocalisation(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    LocalisationLanguage CandidateLanguage,
    ImmutableArray<LocalisationDeclaration> Candidates,
    ContributionResolution<LocalisationIdentity, LocalisationDeclaration> ContributionResolution,
    ImmutableArray<LocalisationResolutionAttempt> Attempts)
    : LocalisationResolution(RequestedLanguage, Key, Attempts);

public sealed record InvalidLocalisationContribution(
    LocalisationLanguage RequestedLanguage,
    LocalisationKey Key,
    LocalisationLanguage CandidateLanguage,
    ContributionResolution<LocalisationIdentity, LocalisationDeclaration> ContributionResolution,
    ImmutableArray<LocalisationResolutionAttempt> Attempts)
    : LocalisationResolution(RequestedLanguage, Key, Attempts);

public sealed record InvalidLocalisation(string Language, string Key, string Reason) : LocalisationLookupResult;

public sealed record HumanReadableName(
    string DisplayText,
    string FallbackText,
    LocalisationLookupResult? Resolution);

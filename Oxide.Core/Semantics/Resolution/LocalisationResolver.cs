using System.Collections.Immutable;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Resolution;

public sealed class LocalisationResolver
{
    private static readonly LocalisationLanguage English = new("english");
    private readonly ImmutableDictionary<LocalisationIdentity, LocalisationEntry> _entries;

    internal LocalisationResolver(ImmutableDictionary<LocalisationIdentity, LocalisationEntry> entries)
    {
        _entries = entries;
    }

    public ImmutableArray<LocalisationLanguage> AvailableLanguages => _entries.Keys
        .Select(identity => identity.Language)
        .Distinct()
        .OrderBy(language => language.Value, StringComparer.Ordinal)
        .ToImmutableArray();

    public LocalisationLookupResult Resolve(string language, string key, bool allowEnglishFallback = true)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return new InvalidLocalisation(language, key, "Language must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return new InvalidLocalisation(language, key, "Localisation key must not be empty.");
        }

        return Resolve(new LocalisationLanguage(language), new LocalisationKey(key), allowEnglishFallback);
    }

    public LocalisationResolution Resolve(
        LocalisationLanguage language,
        LocalisationKey key,
        bool allowEnglishFallback = true)
    {
        var exact = ResolveCandidate(language, language, key, "Exact language match");
        if (exact is not MissingLocalisation || !allowEnglishFallback || language == English)
        {
            return exact;
        }

        var fallback = ResolveCandidate(language, English, key, "English fallback");
        return fallback is MissingLocalisation
            ? new MissingLocalisation(language, key, [language, English])
            : fallback;
    }

    public HumanReadableName ResolveName(
        ISemanticEntity entity,
        string language,
        bool allowEnglishFallback = true)
    {
        var (key, fallback) = entity switch
        {
            StateEntity state => (state.Name?.Value, $"State {state.Id.LocalKey}"),
            CountryEntity country => (country.Id.LocalKey, country.Id.LocalKey),
            _ => (null, entity.Id.LocalKey),
        };

        if (key is null)
        {
            return new HumanReadableName(fallback, fallback, null);
        }

        var outcome = Resolve(language, key, allowEnglishFallback);
        return outcome is ResolvedLocalisation resolved
            ? new HumanReadableName(resolved.Value, fallback, resolved)
            : new HumanReadableName(fallback, fallback, outcome);
    }

    private LocalisationResolution ResolveCandidate(
        LocalisationLanguage requestedLanguage,
        LocalisationLanguage candidateLanguage,
        LocalisationKey key,
        string reason)
    {
        if (!_entries.TryGetValue(new LocalisationIdentity(candidateLanguage, key), out var entry))
        {
            return new MissingLocalisation(requestedLanguage, key, [candidateLanguage]);
        }

        return entry.Contributions.Length == 1
            ? new ResolvedLocalisation(requestedLanguage, key, entry.Contributions[0], reason)
            : new AmbiguousLocalisation(requestedLanguage, key, candidateLanguage, entry.Contributions);
    }
}

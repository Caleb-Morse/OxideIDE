using System.Collections.Immutable;
using System.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Verification;

public static class CorpusSummaryBuilder
{
    public static CorpusSummary Build(
        WorkspaceSnapshot snapshot,
        TimeSpan totalLoadDuration,
        CorpusSummaryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (totalLoadDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLoadDuration));
        }

        options ??= new CorpusSummaryOptions();

        var syntaxDiagnostics = snapshot.Documents
            .SelectMany(document => document.SyntaxTree?.Diagnostics
                ?? document.LocalisationSyntaxTree?.Diagnostics
                ?? [])
            .ToArray();
        var references = snapshot.Semantics.States.Values
            .SelectMany(state => state.Owner is null ? state.Cores : state.Cores.Insert(0, state.Owner))
            .Select(reference => reference.Resolution)
            .ToArray();

        return new CorpusSummary(
            snapshot.Configuration.DisplayName,
            snapshot.Version,
            snapshot.Documents.Length,
            snapshot.Documents.Count(document => document.LoadStatus is DocumentLoadStatus.Loaded),
            snapshot.Documents.Count(document => document.LoadStatus is DocumentLoadStatus.Failed),
            syntaxDiagnostics.Length,
            CountByCode(syntaxDiagnostics.Select(diagnostic => diagnostic.Code)),
            CountByCode(snapshot.Diagnostics.Select(diagnostic => diagnostic.Code)),
            snapshot.Semantics.StateDeclarations.Length,
            snapshot.Semantics.States.Count,
            snapshot.Semantics.CountryDeclarations.Length,
            snapshot.Semantics.Countries.Count,
            snapshot.Semantics.Diagnostics.Length,
            CountByCode(snapshot.Semantics.Diagnostics.Select(diagnostic => diagnostic.Code)),
            new ReferenceResolutionCounts(
                references.Length,
                references.Count(reference => reference is ResolvedCountry),
                references.Count(reference => reference is MissingCountry),
                references.Count(reference => reference is AmbiguousCountry),
                references.Count(reference => reference is InvalidCountry)),
            BuildLocalisationSummary(snapshot, options),
            snapshot.LoadMetrics,
            totalLoadDuration.TotalMilliseconds);
    }

    private static LocalisationCorpusSummary BuildLocalisationSummary(
        WorkspaceSnapshot snapshot,
        CorpusSummaryOptions options)
    {
        var documents = snapshot.Documents
            .Where(document => document.Kind is SourceDocumentKind.Localisation)
            .ToArray();
        var declarations = snapshot.Semantics.LocalisationDeclarations;
        var syntaxDiagnostics = documents
            .Where(document => document.LocalisationSyntaxTree is not null)
            .SelectMany(document => document.LocalisationSyntaxTree!.Diagnostics)
            .ToArray();
        var semanticDiagnostics = snapshot.Semantics.Diagnostics
            .Where(diagnostic => diagnostic.Code is "OXIDE4009")
            .ToArray();
        var languages = snapshot.Semantics.LocalisationResolver.AvailableLanguages
            .Select(language => language.Value)
            .ToImmutableArray();
        var requestedLanguage = NormalizeLanguage(options.RequestedLanguage);
        var effectiveLanguage = ChooseEffectiveLanguage(requestedLanguage, languages);
        var projectionStart = Stopwatch.GetTimestamp();
        var stateNames = CountNames(snapshot.Semantics.States.Values.Select(entity =>
            snapshot.Semantics.LocalisationResolver.ResolveName(
                entity,
                effectiveLanguage,
                options.EnglishFallbackEnabled)));
        var countryNames = CountNames(snapshot.Semantics.Countries.Values.Select(entity =>
            snapshot.Semantics.LocalisationResolver.ResolveName(
                entity,
                effectiveLanguage,
                options.EnglishFallbackEnabled)));
        var projectionElapsed = Stopwatch.GetElapsedTime(projectionStart);
        var projectionCount = stateNames.Total + countryNames.Total;

        return new LocalisationCorpusSummary(
            documents.Length,
            documents.Count(document => document.IsLoaded),
            documents.Count(document => !document.IsLoaded),
            syntaxDiagnostics.Length,
            CountByCode(syntaxDiagnostics.Select(diagnostic => diagnostic.Code)),
            semanticDiagnostics.Length,
            CountByCode(semanticDiagnostics.Select(diagnostic => diagnostic.Code)),
            languages,
            declarations
                .GroupBy(declaration => declaration.Language.Value, StringComparer.Ordinal)
                .ToImmutableSortedDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal),
            declarations.Length,
            snapshot.Semantics.Localisations.Count,
            snapshot.Semantics.Localisations.Count(entry => entry.Value.Contributions.Length > 1),
            snapshot.Semantics.Localisations.Count(entry => entry.Value.IsAmbiguous),
            declarations.Count(declaration => HasValidProvenance(snapshot, declaration.Provenance) &&
                HasValidProvenance(snapshot, declaration.Value.Provenance)),
            requestedLanguage,
            effectiveLanguage,
            options.EnglishFallbackEnabled,
            stateNames,
            countryNames,
            projectionElapsed.TotalMilliseconds,
            projectionElapsed.TotalMilliseconds <= 0
                ? 0
                : projectionCount / projectionElapsed.TotalMilliseconds * 1_000,
            GC.GetTotalMemory(forceFullCollection: false));
    }

    private static LocalisationResolutionCounts CountNames(IEnumerable<HumanReadableName> names)
    {
        var exact = 0;
        var fallback = 0;
        var missing = 0;
        var ambiguous = 0;
        var invalid = 0;
        var noKey = 0;
        var total = 0;
        foreach (var name in names)
        {
            total++;
            switch (name.Resolution)
            {
                case ResolvedLocalisation resolved when resolved.IsFallback: fallback++; break;
                case ResolvedLocalisation: exact++; break;
                case MissingLocalisation: missing++; break;
                case AmbiguousLocalisation: ambiguous++; break;
                case InvalidLocalisation: invalid++; break;
                case null: noKey++; break;
            }
        }

        return new LocalisationResolutionCounts(total, exact, fallback, missing, ambiguous, invalid, noKey);
    }

    private static bool HasValidProvenance(
        WorkspaceSnapshot snapshot,
        Oxide.Core.Semantics.Model.SourceProvenance provenance) =>
        snapshot.DocumentsById.TryGetValue(provenance.DocumentId, out var document) &&
        document.Text is not null &&
        document.PhysicalPath == provenance.PhysicalPath &&
        document.Layer == provenance.Layer &&
        provenance.Span.Start >= 0 &&
        provenance.Span.End <= document.Text.Length;

    private static string NormalizeLanguage(string language) =>
        string.IsNullOrWhiteSpace(language) ? "english" : LocalisationLanguage.Normalize(language);

    private static string ChooseEffectiveLanguage(string requested, ImmutableArray<string> available) =>
        available.Contains(requested, StringComparer.Ordinal)
            ? requested
            : available.Contains("english", StringComparer.Ordinal)
                ? "english"
                : available.FirstOrDefault() ?? "english";

    private static ImmutableSortedDictionary<string, int> CountByCode(IEnumerable<string> codes) =>
        codes.GroupBy(code => code, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}

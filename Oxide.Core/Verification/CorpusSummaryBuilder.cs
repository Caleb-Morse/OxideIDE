using System.Collections.Immutable;
using System.Diagnostics;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.Core.Verification;

public static class CorpusSummaryBuilder
{
    public static CorpusSummary Build(
        WorkspaceSnapshot snapshot,
        TimeSpan totalLoadDuration,
        CorpusSummaryOptions? options = null,
        WorkspaceRefreshResult? incrementalRefresh = null)
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
            BuildContributionSummary(snapshot),
            BuildStrategicRegionSummary(snapshot),
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
            totalLoadDuration.TotalMilliseconds,
            incrementalRefresh is null ? null : BuildIncrementalRefreshSummary(incrementalRefresh));
    }

    private static IncrementalRefreshCorpusSummary BuildIncrementalRefreshSummary(WorkspaceRefreshResult refresh) =>
        new(
            refresh.Outcome.ToString(),
            refresh.Request.Trigger.ToString(),
            refresh.Metrics.RawEventCount,
            refresh.Metrics.CoalescedChangeCount,
            refresh.Metrics.DocumentsReparsed,
            refresh.Metrics.DocumentsReused,
            refresh.Metrics.UsedFullRescan,
            refresh.Metrics.RebuiltSemanticDomains.Select(domain => domain.ToString()).ToImmutableArray(),
            refresh.Metrics.ReusedSemanticDomains.Select(domain => domain.ToString()).ToImmutableArray(),
            refresh.Metrics.DocumentLoadingMilliseconds,
            refresh.Metrics.SemanticMilliseconds,
            refresh.Metrics.PublicationMilliseconds,
            refresh.Metrics.TotalMilliseconds);

    private static ContributionCorpusSummary BuildContributionSummary(WorkspaceSnapshot snapshot)
    {
        var states = BuildContributionDomain(snapshot.Semantics.States.Values
            .Select(entity => entity.ContributionResolution));
        var countries = BuildContributionDomain(snapshot.Semantics.Countries.Values
            .Select(entity => entity.ContributionResolution));
        var strategicRegions = BuildContributionDomain(snapshot.Semantics.StrategicRegions.Values
            .Select(entity => entity.ContributionResolution));
        var localisations = BuildContributionDomain(snapshot.Semantics.Localisations.Values
            .Select(entry => entry.Resolution));
        return new ContributionCorpusSummary(
            states,
            countries,
            strategicRegions,
            localisations,
            Aggregate([states, countries, strategicRegions, localisations]));
    }

    private static ContributionDomainSummary BuildContributionDomain<TIdentity, TDeclaration>(
        IEnumerable<ContributionResolution<TIdentity, TDeclaration>> source)
        where TIdentity : notnull
    {
        var resolutions = source.ToArray();
        var contributions = resolutions.SelectMany(resolution => resolution.Contributions).ToArray();
        return new ContributionDomainSummary(
            resolutions.Length,
            resolutions.Count(resolution => resolution.Contributions.Length > 1),
            resolutions.Count(resolution =>
                resolution.Reason.Kind is ContributionResolutionReasonKind.HigherLayerPrecedence),
            resolutions.Count(resolution =>
                resolution.Kind is ContributionResolutionKind.DuplicateWithinLayer),
            resolutions.Count(resolution => resolution.Kind is ContributionResolutionKind.InvalidWinner),
            resolutions.Count(resolution => resolution.Kind is ContributionResolutionKind.Missing),
            new ContributionDispositionCounts(
                contributions.Length,
                contributions.Count(contribution => contribution.Disposition is ContributionDisposition.Effective),
                contributions.Count(contribution => contribution.Disposition is ContributionDisposition.Shadowed),
                contributions.Count(contribution => contribution.Disposition is ContributionDisposition.Ambiguous),
                contributions.Count(contribution => contribution.Disposition is ContributionDisposition.Invalid),
                contributions.Count(contribution => contribution.Disposition is ContributionDisposition.Excluded)),
            contributions
                .GroupBy(contribution => contribution.Contribution.Provenance.Layer.Id.Value, StringComparer.Ordinal)
                .ToImmutableSortedDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal));
    }

    private static ContributionDomainSummary Aggregate(ImmutableArray<ContributionDomainSummary> domains)
    {
        var layers = domains
            .SelectMany(domain => domain.ContributionsByLayer)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.Value),
                StringComparer.Ordinal);
        return new ContributionDomainSummary(
            domains.Sum(domain => domain.IdentityCount),
            domains.Sum(domain => domain.MultiContributionIdentityCount),
            domains.Sum(domain => domain.CrossLayerOverrideCount),
            domains.Sum(domain => domain.SameLayerDuplicateIdentityCount),
            domains.Sum(domain => domain.InvalidWinnerIdentityCount),
            domains.Sum(domain => domain.MissingIdentityCount),
            new ContributionDispositionCounts(
                domains.Sum(domain => domain.Dispositions.Total),
                domains.Sum(domain => domain.Dispositions.Effective),
                domains.Sum(domain => domain.Dispositions.Shadowed),
                domains.Sum(domain => domain.Dispositions.Ambiguous),
                domains.Sum(domain => domain.Dispositions.Invalid),
                domains.Sum(domain => domain.Dispositions.Excluded)),
            layers);
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
        var strategicRegionNames = CountNames(snapshot.Semantics.StrategicRegions.Values.Select(entity =>
            snapshot.Semantics.LocalisationResolver.ResolveName(
                entity,
                effectiveLanguage,
                options.EnglishFallbackEnabled)));
        var projectionElapsed = Stopwatch.GetElapsedTime(projectionStart);
        var projectionCount = stateNames.Total + countryNames.Total + strategicRegionNames.Total;

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
            strategicRegionNames,
            projectionElapsed.TotalMilliseconds,
            projectionElapsed.TotalMilliseconds <= 0
                ? 0
                : projectionCount / projectionElapsed.TotalMilliseconds * 1_000,
            GC.GetTotalMemory(forceFullCollection: false));
    }

    private static StrategicRegionCorpusSummary BuildStrategicRegionSummary(WorkspaceSnapshot snapshot)
    {
        var documents = snapshot.Documents
            .Where(document => document.VirtualPath.Value.StartsWith(
                "map/strategicregions/",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var declarations = snapshot.Semantics.StrategicRegionDeclarations;
        var entities = snapshot.Semantics.StrategicRegions.Values.ToArray();
        var index = snapshot.Semantics.ProvinceStrategicRegionIndex;
        var memberships = snapshot.Semantics.StateStrategicRegionMemberships.Values.ToArray();
        var provinceCandidates = declarations.SelectMany(declaration => declaration.Provinces).ToArray();
        var repeatedProvinceCandidates = declarations.Sum(declaration => declaration.Provinces
            .GroupBy(province => province.Value)
            .Sum(group => Math.Max(0, group.Count() - 1)));

        return new StrategicRegionCorpusSummary(
            documents.Length,
            documents.Count(document => document.IsLoaded),
            documents.Count(document => !document.IsLoaded),
            declarations.Length,
            entities.Length,
            entities.Count(entity => entity.Status is Oxide.Core.Semantics.Model.SemanticEntityStatus.Effective),
            entities.Count(entity => entity.Status is Oxide.Core.Semantics.Model.SemanticEntityStatus.Ambiguous),
            provinceCandidates.Length,
            repeatedProvinceCandidates,
            index.CandidatesByProvince.Count,
            index.CandidatesByProvince.Keys.Count(provinceId =>
                index.Resolve(provinceId) is AmbiguousProvinceStrategicRegion),
            declarations.Count(declaration => HasValidProvenance(snapshot, declaration.Provenance)),
            provinceCandidates.Count(candidate => HasValidProvenance(snapshot, candidate.Provenance)),
            new StrategicRegionMembershipCounts(
                memberships.Length,
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.SingleRegion),
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.Split),
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.Partial),
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.Missing),
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.Ambiguous),
                memberships.Count(membership => membership.Status is StateStrategicRegionMembershipStatus.NoProvinces)));
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
                case InvalidLocalisationContribution: invalid++; break;
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

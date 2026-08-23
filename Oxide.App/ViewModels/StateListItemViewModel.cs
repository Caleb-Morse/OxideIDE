using System.Collections.Immutable;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed class StateListItemViewModel
{
    public StateListItemViewModel(
        StateEntity entity,
        WorkspaceSnapshot snapshot,
        string language = "english",
        bool allowEnglishFallback = true)
    {
        Entity = entity;
        Id = int.Parse(entity.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture);
        LocalizationKey = entity.Name?.Value ?? "No name key";
        var name = LocalisedNamePresentation.Create(
            snapshot.Semantics.LocalisationResolver.ResolveName(entity, language, allowEnglishFallback),
            LocalizationKey,
            snapshot);
        DisplayName = name.DisplayName;
        NameStatus = name.ResolutionStatus;
        LocalisationSource = name.SourcePath;
        LocalisationLocation = name.SourceLocation;
        LocalisationLayer = name.SourceLayer;
        ResolvedLanguage = name.SourceLanguage;
        Owner = DescribeCountry(entity.Owner, snapshot, language, allowEnglishFallback);
        Category = entity.StateCategory?.Value ?? "Unknown";
        Manpower = entity.Manpower?.Value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) ?? "Unknown";
        Resources = entity.Resources.Count == 0
            ? "None declared"
            : string.Join(", ", entity.Resources.OrderBy(resource => resource.Key)
                .Select(resource => $"{resource.Key}: {resource.Value.Value:0.##}"));
        ProvinceIds = entity.Provinces.Select(province => province.Value).ToImmutableArray();
        Provinces = ProvinceIds.Length == 0 ? "None declared" : string.Join(", ", ProvinceIds);
        ProvinceSummary = $"{ProvinceIds.Length:N0} provinces";
        Cores = entity.Cores.Length == 0
            ? "None declared"
            : string.Join(", ", entity.Cores.Select(core => DescribeCountry(
                core,
                snapshot,
                language,
                allowEnglishFallback)));
        var regionMembership = StrategicRegionMembershipPresentation.Create(
            snapshot.Semantics.StateStrategicRegionMemberships[Id],
            snapshot,
            language,
            allowEnglishFallback);
        StrategicRegion = regionMembership.DisplayName;
        StrategicRegionStatus = regionMembership.Status;
        StrategicRegionSummary = regionMembership.Summary;
        StrategicRegionEvidence = regionMembership.Evidence;
        Status = entity.Status.ToString();
        SourceSummary = entity.Contributions.Length == 1
            ? entity.Contributions[0].Provenance.PhysicalPath
            : $"{entity.Contributions.Length} competing declarations";
        SourceLayer = entity.Contributions.Length == 1
            ? entity.Contributions[0].Provenance.Layer.Kind.ToString()
            : "Ambiguous";
        SourceLocation = DescribeLocation(entity, snapshot);
        DiagnosticCount = entity.Diagnostics.Length + regionMembership.DiagnosticCount;
    }

    public StateEntity Entity { get; }

    public int Id { get; }

    public string DisplayName { get; }

    public string NameStatus { get; }

    public string LocalizationKey { get; }

    public string Owner { get; }

    public string Category { get; }

    public string Manpower { get; }

    public string Resources { get; }

    public ImmutableArray<int> ProvinceIds { get; }

    public string Provinces { get; }

    public string ProvinceSummary { get; }

    public string Cores { get; }

    public string StrategicRegion { get; }

    public string StrategicRegionStatus { get; }

    public string StrategicRegionSummary { get; }

    public ImmutableArray<ProvinceRegionEvidenceViewModel> StrategicRegionEvidence { get; }

    public string Status { get; }

    public string SourceSummary { get; }

    public string SourceLayer { get; }

    public string SourceLocation { get; }

    public int DiagnosticCount { get; }

    public string LocalisationSource { get; }

    public string LocalisationLocation { get; }

    public string LocalisationLayer { get; }

    public string ResolvedLanguage { get; }

    public string SearchText =>
        $"{Id} {DisplayName} {LocalizationKey} {Owner} {Category} {StrategicRegion} {StrategicRegionStatus} {SourceSummary}";

    private static string DescribeCountry(
        CountryReference? reference,
        WorkspaceSnapshot snapshot,
        string language,
        bool allowEnglishFallback) => reference is null
        ? "Not declared"
        : reference.Resolution switch
        {
            ResolvedCountry resolved => DescribeResolvedCountry(
                resolved.Target,
                snapshot,
                language,
                allowEnglishFallback),
            MissingCountry missing => $"{missing.CandidateTag} (missing)",
            AmbiguousCountry ambiguous => $"{ambiguous.CandidateTag} (ambiguous)",
            InvalidCountry => $"{reference.OriginalTag} (invalid)",
            _ => reference.OriginalTag,
        };

    private static string DescribeResolvedCountry(
        CountryEntity country,
        WorkspaceSnapshot snapshot,
        string language,
        bool allowEnglishFallback)
    {
        var name = snapshot.Semantics.LocalisationResolver.ResolveName(country, language, allowEnglishFallback);
        return name.DisplayText == country.Id.LocalKey
            ? country.Id.LocalKey
            : $"{name.DisplayText} · {country.Id.LocalKey}";
    }

    private static string DescribeLocation(StateEntity entity, WorkspaceSnapshot snapshot)
    {
        if (entity.Contributions.Length != 1)
        {
            return "No effective source location";
        }

        var provenance = entity.Contributions[0].Provenance;
        if (!snapshot.DocumentsById.TryGetValue(provenance.DocumentId, out var document) || document.Text is null)
        {
            return $"Offset {provenance.Span.Start}";
        }

        var position = document.Text.GetPosition(provenance.Span.Start);
        return $"Line {position.Line + 1}, column {position.Character + 1}";
    }
}

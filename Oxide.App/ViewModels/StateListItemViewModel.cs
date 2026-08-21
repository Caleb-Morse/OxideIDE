using System.Collections.Immutable;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed class StateListItemViewModel
{
    public StateListItemViewModel(StateEntity entity, WorkspaceSnapshot snapshot)
    {
        Entity = entity;
        Id = int.Parse(entity.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture);
        DisplayName = entity.Name?.Value ?? $"State {Id}";
        LocalizationKey = entity.Name?.Value ?? "No name key";
        Owner = DescribeCountry(entity.Owner);
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
            : string.Join(", ", entity.Cores.Select(core => $"{core.OriginalTag} ({DescribeResolution(core.Resolution)})"));
        Status = entity.Status.ToString();
        SourceSummary = entity.Contributions.Length == 1
            ? entity.Contributions[0].Provenance.PhysicalPath
            : $"{entity.Contributions.Length} competing declarations";
        SourceLayer = entity.Contributions.Length == 1
            ? entity.Contributions[0].Provenance.Layer.Kind.ToString()
            : "Ambiguous";
        SourceLocation = DescribeLocation(entity, snapshot);
        DiagnosticCount = entity.Diagnostics.Length;
    }

    public StateEntity Entity { get; }

    public int Id { get; }

    public string DisplayName { get; }

    public string LocalizationKey { get; }

    public string Owner { get; }

    public string Category { get; }

    public string Manpower { get; }

    public string Resources { get; }

    public ImmutableArray<int> ProvinceIds { get; }

    public string Provinces { get; }

    public string ProvinceSummary { get; }

    public string Cores { get; }

    public string Status { get; }

    public string SourceSummary { get; }

    public string SourceLayer { get; }

    public string SourceLocation { get; }

    public int DiagnosticCount { get; }

    public string SearchText => $"{Id} {DisplayName} {LocalizationKey} {Owner} {Category} {SourceSummary}";

    private static string DescribeCountry(CountryReference? reference) => reference is null
        ? "Not declared"
        : reference.Resolution switch
        {
            ResolvedCountry resolved => resolved.Target.Id.LocalKey,
            MissingCountry missing => $"{missing.CandidateTag} (missing)",
            AmbiguousCountry ambiguous => $"{ambiguous.CandidateTag} (ambiguous)",
            InvalidCountry => $"{reference.OriginalTag} (invalid)",
            _ => reference.OriginalTag,
        };

    private static string DescribeResolution(CountryResolution resolution) => resolution switch
    {
        ResolvedCountry => "resolved",
        MissingCountry => "missing",
        AmbiguousCountry => "ambiguous",
        InvalidCountry => "invalid",
        _ => "unknown",
    };

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

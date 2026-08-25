using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Text;

namespace Oxide.App.ViewModels;

internal sealed record LocalisedNamePresentation(
    string DisplayName,
    string ResolutionStatus,
    string SourcePath,
    string SourceLocation,
    string SourceLayer,
    string SourceLanguage,
    string SourceKey,
    LocalisationInspectionPresentation Inspection)
{
    public static LocalisedNamePresentation Create(
        HumanReadableName name,
        string key,
        WorkspaceSnapshot snapshot)
    {
        var inspection = LocalisationInspectionPresentation.Create(name, key, snapshot);
        if (name.Resolution is not ResolvedLocalisation resolved)
        {
            var status = name.Resolution switch
            {
                AmbiguousLocalisation => "Ambiguous localisation",
                InvalidLocalisationContribution => "Invalid localisation contribution",
                MissingLocalisation => "Missing localisation",
                InvalidLocalisation => "Invalid localisation request",
                _ => "No localisation key",
            };
            return new LocalisedNamePresentation(name.DisplayText, status, "No resolved localisation source",
                "No source location", "—", "—", key, inspection);
        }

        var provenance = resolved.Provenance;
        var location = snapshot.DocumentsById.TryGetValue(provenance.DocumentId, out var document) && document.Text is not null
            ? (TextPosition?)document.Text.GetPosition(provenance.Span.Start)
            : null;
        return new LocalisedNamePresentation(
            name.DisplayText,
            resolved.IsFallback ? $"English fallback for {resolved.RequestedLanguage}" : $"Exact {resolved.ResolvedLanguage} match",
            provenance.PhysicalPath,
            location is null ? $"Offset {provenance.Span.Start}" : $"Line {location.Value.Line + 1}, column {location.Value.Character + 1}",
            provenance.Layer.Kind.ToString(),
            resolved.ResolvedLanguage.Value,
            key,
            inspection);
    }
}

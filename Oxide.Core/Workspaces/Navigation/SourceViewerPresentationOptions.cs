namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerPresentationOptions
{
    public const int DefaultMaximumMaterializedLines = 400;
    public const int DefaultMaximumHighlightSpans = 4_000;
    public const int DefaultMaximumSearchResults = 500;
    public const int DefaultMaximumDiagnosticResults = 500;
    public const int DefaultMaximumSearchQueryLength = 512;

    public SourceViewerPresentationOptions(
        int maximumMaterializedLines = DefaultMaximumMaterializedLines,
        int maximumHighlightSpans = DefaultMaximumHighlightSpans,
        int maximumSearchResults = DefaultMaximumSearchResults,
        int maximumSearchQueryLength = DefaultMaximumSearchQueryLength,
        int maximumDiagnosticResults = DefaultMaximumDiagnosticResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHighlightSpans);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchQueryLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDiagnosticResults);
        MaximumMaterializedLines = maximumMaterializedLines;
        MaximumHighlightSpans = maximumHighlightSpans;
        MaximumSearchResults = maximumSearchResults;
        MaximumSearchQueryLength = maximumSearchQueryLength;
        MaximumDiagnosticResults = maximumDiagnosticResults;
    }

    public int MaximumMaterializedLines { get; }

    public int MaximumHighlightSpans { get; }

    public int MaximumSearchResults { get; }

    public int MaximumSearchQueryLength { get; }

    public int MaximumDiagnosticResults { get; }
}

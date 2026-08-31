namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceViewerPresentationOptions
{
    public const int DefaultMaximumMaterializedLines = 400;
    public const int DefaultMaximumHighlightSpans = 4_000;
    public const int DefaultMaximumSearchResults = 500;
    public const int DefaultMaximumSearchQueryLength = 512;

    public SourceViewerPresentationOptions(
        int maximumMaterializedLines = DefaultMaximumMaterializedLines,
        int maximumHighlightSpans = DefaultMaximumHighlightSpans,
        int maximumSearchResults = DefaultMaximumSearchResults,
        int maximumSearchQueryLength = DefaultMaximumSearchQueryLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedLines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHighlightSpans);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchQueryLength);
        MaximumMaterializedLines = maximumMaterializedLines;
        MaximumHighlightSpans = maximumHighlightSpans;
        MaximumSearchResults = maximumSearchResults;
        MaximumSearchQueryLength = maximumSearchQueryLength;
    }

    public int MaximumMaterializedLines { get; }

    public int MaximumHighlightSpans { get; }

    public int MaximumSearchResults { get; }

    public int MaximumSearchQueryLength { get; }
}

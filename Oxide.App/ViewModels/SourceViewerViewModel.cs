using System.Collections.Immutable;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Text;

namespace Oxide.App.ViewModels;

public sealed class SourceViewerViewModel : ObservableObject
{
    private const int MaximumRelatedSources = 200;
    private readonly SourceNavigationResolution resolution;
    private readonly SourceViewerPresentationOptions options;
    private SourceViewerDocument document;
    private string searchText = string.Empty;
    private string searchSummary = "Find in this file";
    private SourceViewerDiagnostic? selectedDiagnostic;

    public SourceViewerViewModel(
        WorkspaceSnapshot snapshot,
        SourceNavigationRequest request,
        SourceNavigationResolution resolution,
        SourceViewerPresentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.IsResolved)
        {
            throw new ArgumentException("A resolved source location is required.", nameof(resolution));
        }

        this.resolution = resolution;
        this.options = options ?? new SourceViewerPresentationOptions();
        document = SourceViewerPresenter.Create(resolution, this.options);
        var relationships = SourceRelationshipProjector.Create(snapshot, request, MaximumRelatedSources);
        Relationships = relationships.Items;
        RelationshipsTruncated = relationships.IsTruncated;
    }

    public string FileName => Path.GetFileName(document.Location.VirtualPath.Value);
    public string VirtualPath => document.Location.VirtualPath.Value;
    public string PhysicalPath => document.Location.PhysicalPath;
    public string LayerName => document.Location.Layer.DisplayName;
    public string LayerKind => document.Location.Layer.Kind.ToString();
    public string Participation => document.Participation.Kind.ToString();
    public string DocumentKind => document.Location.DocumentKind.ToString();
    public string SnapshotLabel => $"Snapshot {document.Location.SnapshotVersion}";
    public string LocationLabel => $"Line {document.Location.StartLine}, column {document.Location.StartColumn}";
    public string SemanticIdentity => document.Location.SemanticIdentity;
    public string Reason => document.Location.Reason;
    public string EncodingLabel => document.Encoding.ToString();
    public string NewlineLabel => document.Newlines.ToString();
    public string FullText => document.Text;
    public int LineCount => document.LineCount;
    public ImmutableArray<SourceViewerDiagnostic> Diagnostics => document.Diagnostics;
    public ImmutableArray<SourceRelationshipViewModel> Relationships { get; }
    public bool RelationshipsTruncated { get; }
    public bool HasDiagnostics => !Diagnostics.IsEmpty;
    public bool HasRelationships => !Relationships.IsEmpty;
    public string RelationshipSummary => RelationshipsTruncated
        ? $"{Relationships.Length:N0}+ related contributions"
        : Relationships.Length == 1
        ? "1 related contribution"
        : $"{Relationships.Length:N0} related contributions";
    public string DiagnosticSummary => document.DiagnosticsTruncated
        ? $"{Diagnostics.Length:N0}+ source diagnostics"
        : Diagnostics.Length == 1 ? "1 source diagnostic" : $"{Diagnostics.Length:N0} source diagnostics";

    public string VisibleText
    {
        get
        {
            var span = VisibleSpan;
            return document.Text.Substring(span.Start, span.Length);
        }
    }

    public string LineNumbers => string.Join(
        Environment.NewLine,
        document.Lines.Select(line => line.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public string WindowSummary => document.LinesTruncated
        ? $"Lines {document.FirstMaterializedLine:N0}–{document.LastMaterializedLine:N0} of {document.LineCount:N0}"
        : $"{document.LineCount:N0} lines";

    public int SelectionStart => document.FocusSpan.Start - VisibleSpan.Start;
    public int SelectionEnd => SelectionStart + document.FocusSpan.Length;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value))
            {
                UpdateSearchSummary();
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public bool CanSearch => !string.IsNullOrEmpty(SearchText);

    public string SearchSummary
    {
        get => searchSummary;
        private set => SetProperty(ref searchSummary, value);
    }

    public SourceViewerDiagnostic? SelectedDiagnostic
    {
        get => selectedDiagnostic;
        set
        {
            if (SetProperty(ref selectedDiagnostic, value) && value is not null)
            {
                Focus(value.Span);
            }
        }
    }

    public bool FindNext() => Find(forward: true);

    public bool FindPrevious() => Find(forward: false);

    private TextSpan VisibleSpan => TextSpan.FromBounds(document.Lines[0].FullSpan.Start, document.Lines[^1].FullSpan.End);

    private bool Find(bool forward)
    {
        if (!CanSearch)
        {
            return false;
        }

        var match = forward
            ? SourceViewerPresenter.FindNext(document, SearchText, document.FocusSpan.End)
            : SourceViewerPresenter.FindPrevious(document, SearchText, document.FocusSpan.Start);
        if (match is null)
        {
            SearchSummary = "No matches";
            return false;
        }

        Focus(match.Value);
        return true;
    }

    private void Focus(TextSpan span)
    {
        document = SourceViewerPresenter.Create(resolution, options, span);
        OnPropertyChanged(nameof(VisibleText));
        OnPropertyChanged(nameof(LineNumbers));
        OnPropertyChanged(nameof(WindowSummary));
        OnPropertyChanged(nameof(SelectionStart));
        OnPropertyChanged(nameof(SelectionEnd));
    }

    private void UpdateSearchSummary()
    {
        if (!CanSearch)
        {
            SearchSummary = "Find in this file";
            return;
        }

        var results = SourceViewerPresenter.FindAll(document, SearchText, options);
        SearchSummary = results.IsTruncated
            ? $"{results.Matches.Length:N0}+ matches"
            : results.Matches.Length == 1 ? "1 match" : $"{results.Matches.Length:N0} matches";
    }
}

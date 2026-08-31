using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Workspaces;

public sealed class SourceViewerPresenterTests
{
    [Fact]
    public async Task Presentation_preserves_exact_text_lines_selection_and_document_metadata()
    {
        using var fixture = new TemporaryWorkspace();
        const string text = "state={\r\n name=\"Åland\"\r\n}";
        fixture.WriteGameFile("history/states/1-Test.txt", text);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = snapshot.Documents[0];
        var start = text.IndexOf("Åland", StringComparison.Ordinal);
        var presentation = Present(snapshot, document, new TextSpan(start, 5));

        Assert.Same(document.Text!.Text, presentation.Text);
        Assert.Equal(text, presentation.Text);
        Assert.Equal(NewlineKind.CarriageReturnLineFeed, presentation.Newlines);
        Assert.False(presentation.HasFinalNewline);
        Assert.Equal(3, presentation.LineCount);
        Assert.Equal(["state={", " name=\"Åland\"", "}"], presentation.Lines.Select(line => line.Text));
        Assert.Equal([1, 2, 3], presentation.Lines.Select(line => line.Number));
        Assert.Equal("Åland", presentation.Text.Substring(
            presentation.Lines[1].Selection!.Value.Start,
            presentation.Lines[1].Selection!.Value.Length));
        Assert.Equal(document.Participation, presentation.Participation);
        Assert.Equal(document.Kind, presentation.Location.DocumentKind);
    }

    [Fact]
    public async Task Large_document_materializes_a_bounded_window_around_the_focus()
    {
        using var fixture = new TemporaryWorkspace();
        var lines = Enumerable.Range(1, 1_000).Select(index => $"value_{index}=yes").ToArray();
        var text = string.Join('\n', lines);
        fixture.WriteGameFile("history/states/1-Large.txt", text);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = snapshot.Documents[0];
        var focusStart = text.IndexOf("value_900", StringComparison.Ordinal);
        var resolution = Resolve(snapshot, document, new TextSpan(0, 5));
        var options = new SourceViewerPresentationOptions(
            maximumMaterializedLines: 12,
            maximumHighlightSpans: 8,
            maximumSearchResults: 4,
            maximumSearchQueryLength: 20);

        var presentation = SourceViewerPresenter.Create(
            resolution,
            options,
            new TextSpan(focusStart, "value_900".Length));

        Assert.Equal(12, presentation.Lines.Length);
        Assert.True(presentation.LinesTruncated);
        Assert.Contains(presentation.Lines, line => line.Text == "value_900=yes" && line.Selection is not null);
        Assert.InRange(900 - presentation.FirstMaterializedLine, 0, 4);
        Assert.InRange(presentation.Highlights.Length, 0, 8);
        Assert.True(presentation.HighlightsTruncated);
        Assert.InRange(900, presentation.FirstMaterializedLine, presentation.LastMaterializedLine);
    }

    [Fact]
    public async Task Clausewitz_highlights_are_derived_from_lossless_tokens()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "history/states/1-Test.txt",
            "# note\nstate={ id=1 name=\"Test\" date=1936.1.1 bad=\u0001 }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var presentation = Present(snapshot, snapshot.Documents[0], new TextSpan(0, 1));

        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Comment);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Identifier);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Number);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.String);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Date);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Brace);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Operator);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Invalid);
        Assert.NotEmpty(presentation.Diagnostics);
        Assert.All(presentation.Diagnostics, diagnostic =>
        {
            Assert.True(diagnostic.Line >= 1);
            Assert.True(diagnostic.Column >= 1);
            Assert.Equal(diagnostic.Span, diagnostic.NavigationTarget.Span);
            Assert.Equal(snapshot.Version, diagnostic.NavigationTarget.SnapshotVersion);
        });
    }

    [Fact]
    public async Task Localisation_highlights_use_the_existing_localisation_tree()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile(
            "localisation/english/test_l_english.yml",
            "l_english:\n # note\n KEY:12 \"Value\"\nmalformed");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var presentation = Present(snapshot, snapshot.Documents[0], new TextSpan(0, 1));

        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Comment);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Identifier);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Number);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.String);
        Assert.Contains(presentation.Highlights, span => span.Kind is SourceHighlightKind.Invalid);
        Assert.NotEmpty(presentation.Diagnostics);
    }

    [Fact]
    public async Task Search_is_bounded_and_supports_next_previous_and_wrap()
    {
        using var fixture = new TemporaryWorkspace();
        const string text = "Alpha beta ALPHA gamma alpha";
        fixture.WriteGameFile("history/states/1-Test.txt", text);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var presentation = Present(snapshot, snapshot.Documents[0], new TextSpan(0, 1));
        var options = new SourceViewerPresentationOptions(maximumSearchResults: 2);

        var results = SourceViewerPresenter.FindAll(presentation, "alpha", options);
        var next = SourceViewerPresenter.FindNext(presentation, "alpha", 1);
        var previous = SourceViewerPresenter.FindPrevious(presentation, "alpha", 1);

        Assert.Equal(2, results.Matches.Length);
        Assert.True(results.IsTruncated);
        Assert.Equal(11, next?.Start);
        Assert.Equal(23, previous?.Start);
        Assert.Throws<ArgumentException>(() => SourceViewerPresenter.FindAll(
            presentation,
            new string('x', SourceViewerPresentationOptions.DefaultMaximumSearchQueryLength + 1)));
    }

    [Fact]
    public async Task Unresolved_navigation_cannot_be_presented_as_source_text()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameBytes("history/states/1-Broken.txt", [0xFF]);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var resolution = Resolve(snapshot, snapshot.Documents[0], new TextSpan(0, 0));

        Assert.False(resolution.IsResolved);
        Assert.Throws<ArgumentException>(() => SourceViewerPresenter.Create(resolution));
    }

    [Fact]
    public void Source_text_line_spans_handle_lf_crlf_cr_empty_and_final_newline()
    {
        var source = SourceText.From("a\r\nb\nc\rd\n");

        Assert.Equal(5, source.LineCount);
        Assert.Equal(["a", "b", "c", "d", ""],
            Enumerable.Range(0, source.LineCount).Select(line => source.GetText(source.GetLineSpan(line))));
        Assert.Equal("a\r\n", source.GetText(source.GetLineFullSpan(0)));
        Assert.Equal(string.Empty, source.GetText(source.GetLineFullSpan(4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetLineSpan(5));
    }

    private static SourceViewerDocument Present(
        WorkspaceSnapshot snapshot,
        SourceDocument document,
        TextSpan span) =>
        SourceViewerPresenter.Create(Resolve(snapshot, document, span));

    private static SourceNavigationResolution Resolve(
        WorkspaceSnapshot snapshot,
        SourceDocument document,
        TextSpan span)
    {
        var target = new SourceNavigationTarget(
            snapshot.Version,
            document.Id,
            document.Layer.Id,
            document.VirtualPath,
            span,
            "State:global:1",
            "Inspect source");
        return SourceNavigationResolver.Resolve(snapshot, target);
    }
}

using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public static class SourceViewerPresenter
{
    public static SourceViewerDocument Create(
        SourceNavigationResolution resolution,
        SourceViewerPresentationOptions? options = null,
        TextSpan? focusSpan = null)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.IsResolved || resolution.Document?.Text is null || resolution.Location is null)
        {
            throw new ArgumentException("A resolved source navigation result with snapshot text is required.", nameof(resolution));
        }

        options ??= new SourceViewerPresentationOptions();
        var document = resolution.Document;
        var source = document.Text;
        var focus = focusSpan ?? resolution.Location.Span;
        if (focus.End > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(focusSpan), "The focus span must be within the document.");
        }

        var selectedLine = source.GetPosition(focus.Start).Line;
        const int preferredContextLines = 4;
        var firstLine = Math.Max(0, selectedLine - Math.Min(preferredContextLines, options.MaximumMaterializedLines / 3));
        var lastLineExclusive = Math.Min(source.LineCount, firstLine + options.MaximumMaterializedLines);
        firstLine = Math.Max(0, lastLineExclusive - options.MaximumMaterializedLines);
        var lines = ImmutableArray.CreateBuilder<SourceViewerLine>(lastLineExclusive - firstLine);
        for (var line = firstLine; line < lastLineExclusive; line++)
        {
            var span = source.GetLineSpan(line);
            lines.Add(new SourceViewerLine(
                line + 1,
                span,
                source.GetLineFullSpan(line),
                source.GetText(span),
                IntersectSelection(focus, span)));
        }

        var window = TextSpan.FromBounds(
            source.GetLineFullSpan(firstLine).Start,
            source.GetLineFullSpan(lastLineExclusive - 1).End);
        var highlights = EnumerateHighlights(document)
            .Where(highlight => Intersects(highlight.Span, window))
            .Take(options.MaximumHighlightSpans + 1)
            .ToArray();
        var highlightsTruncated = highlights.Length > options.MaximumHighlightSpans;
        var diagnostics = document.Diagnostics
            .Where(diagnostic => diagnostic.Span is not null)
            .Select(diagnostic =>
            {
                var span = diagnostic.Span!.Value;
                var position = source.GetPosition(Math.Min(span.Start, source.Length));
                return new SourceViewerDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    span,
                    position.Line + 1,
                    position.Character + 1,
                    new SourceNavigationTarget(
                        resolution.Location.SnapshotVersion,
                        document.Id,
                        document.Layer.Id,
                        document.VirtualPath,
                        span,
                        diagnostic.Code,
                        $"Open source diagnostic {diagnostic.Code}"));
            })
            .ToImmutableArray();

        return new SourceViewerDocument(
            resolution.Location,
            focus,
            source.Text,
            source.Encoding,
            source.Newlines,
            source.HasFinalNewline,
            source.LineCount,
            firstLine + 1,
            lastLineExclusive,
            lines.ToImmutable(),
            highlights.Take(options.MaximumHighlightSpans).ToImmutableArray(),
            highlightsTruncated,
            diagnostics);
    }

    public static SourceSearchResults FindAll(
        SourceViewerDocument document,
        string query,
        SourceViewerPresentationOptions? options = null,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SourceViewerPresentationOptions();
        ValidateQuery(query, options);
        var matches = ImmutableArray.CreateBuilder<TextSpan>();
        var offset = 0;
        while (offset <= document.Text.Length - query.Length)
        {
            var found = document.Text.IndexOf(query, offset, comparison);
            if (found < 0)
            {
                break;
            }

            if (matches.Count == options.MaximumSearchResults)
            {
                return new SourceSearchResults(query, matches.ToImmutable(), IsTruncated: true);
            }

            matches.Add(new TextSpan(found, query.Length));
            offset = found + Math.Max(1, query.Length);
        }

        return new SourceSearchResults(query, matches.ToImmutable(), IsTruncated: false);
    }

    public static TextSpan? FindNext(
        SourceViewerDocument document,
        string query,
        int afterOffset,
        bool wrap = true,
        SourceViewerPresentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SourceViewerPresentationOptions();
        ValidateQuery(query, options);
        if ((uint)afterOffset > (uint)document.Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(afterOffset));
        }

        var found = document.Text.IndexOf(query, afterOffset, StringComparison.OrdinalIgnoreCase);
        if (found < 0 && wrap)
        {
            found = document.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        }

        return found < 0 ? null : new TextSpan(found, query.Length);
    }

    public static TextSpan? FindPrevious(
        SourceViewerDocument document,
        string query,
        int beforeOffset,
        bool wrap = true,
        SourceViewerPresentationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SourceViewerPresentationOptions();
        ValidateQuery(query, options);
        if ((uint)beforeOffset > (uint)document.Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(beforeOffset));
        }

        var start = Math.Min(beforeOffset - 1, document.Text.Length - 1);
        var found = start < 0
            ? -1
            : document.Text.LastIndexOf(query, start, StringComparison.OrdinalIgnoreCase);
        if (found < 0 && wrap)
        {
            found = document.Text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
        }

        return found < 0 ? null : new TextSpan(found, query.Length);
    }

    private static IEnumerable<SourceHighlightSpan> EnumerateHighlights(SourceDocument document)
    {
        if (document.SyntaxTree is not null)
        {
            return document.SyntaxTree.Tokens
                .Where(token => !token.IsMissing)
                .Select(token => (Token: token, Kind: HighlightKind(token.Kind)))
                .Where(item => item.Kind is not null)
                .Select(item => new SourceHighlightSpan(item.Token.Span, item.Kind!.Value));
        }

        if (document.LocalisationSyntaxTree is not null)
        {
            return document.LocalisationSyntaxTree.Lines.SelectMany(HighlightLocalisationLine);
        }

        return [];
    }

    private static IEnumerable<SourceHighlightSpan> HighlightLocalisationLine(LocalisationLineSyntax line) => line switch
    {
        LocalisationCommentLineSyntax comment =>
            [new SourceHighlightSpan(comment.FullSpan, SourceHighlightKind.Comment)],
        LocalisationLanguageHeaderSyntax header =>
            [new SourceHighlightSpan(header.LanguageSpan, SourceHighlightKind.Identifier)],
        LocalisationEntrySyntax entry => HighlightLocalisationEntry(entry),
        LocalisationUnknownLineSyntax unknown =>
            [new SourceHighlightSpan(unknown.FullSpan, SourceHighlightKind.Invalid)],
        _ => [],
    };

    private static IEnumerable<SourceHighlightSpan> HighlightLocalisationEntry(LocalisationEntrySyntax entry)
    {
        yield return new SourceHighlightSpan(entry.KeySpan, SourceHighlightKind.Identifier);
        if (entry.VersionSpan is { } version)
        {
            yield return new SourceHighlightSpan(version, SourceHighlightKind.Number);
        }

        yield return new SourceHighlightSpan(entry.QuotedValueSpan, SourceHighlightKind.String);
    }

    private static SourceHighlightKind? HighlightKind(SyntaxKind kind) => kind switch
    {
        SyntaxKind.CommentToken => SourceHighlightKind.Comment,
        SyntaxKind.IdentifierToken => SourceHighlightKind.Identifier,
        SyntaxKind.QuotedStringToken => SourceHighlightKind.String,
        SyntaxKind.NumberToken => SourceHighlightKind.Number,
        SyntaxKind.DateToken => SourceHighlightKind.Date,
        SyntaxKind.OpenBraceToken or SyntaxKind.CloseBraceToken => SourceHighlightKind.Brace,
        SyntaxKind.EqualsToken or SyntaxKind.DoubleEqualsToken or SyntaxKind.NotEqualsToken or
            SyntaxKind.LessThanToken or SyntaxKind.LessThanOrEqualsToken or SyntaxKind.GreaterThanToken or
            SyntaxKind.GreaterThanOrEqualsToken or SyntaxKind.QuestionEqualsToken => SourceHighlightKind.Operator,
        SyntaxKind.BadToken => SourceHighlightKind.Invalid,
        _ => null,
    };

    private static TextSpan? IntersectSelection(TextSpan selection, TextSpan line)
    {
        if (selection.Length == 0)
        {
            return selection.Start >= line.Start && selection.Start <= line.End ? selection : null;
        }

        var start = Math.Max(selection.Start, line.Start);
        var end = Math.Min(selection.End, line.End);
        return end > start ? TextSpan.FromBounds(start, end) : null;
    }

    private static bool Intersects(TextSpan left, TextSpan right) =>
        left.Length == 0
            ? left.Start >= right.Start && left.Start <= right.End
            : left.Start < right.End && right.Start < left.End;

    private static void ValidateQuery(string query, SourceViewerPresentationOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        if (query.Length > options.MaximumSearchQueryLength)
        {
            throw new ArgumentException(
                $"Search queries cannot exceed {options.MaximumSearchQueryLength} characters.",
                nameof(query));
        }
    }
}

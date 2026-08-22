using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Syntax.Localisation;

public static class LocalisationParser
{
    public static LocalisationSyntaxTree Parse(string text) => Parse(SourceText.From(text));

    public static LocalisationSyntaxTree Parse(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lines = ImmutableArray.CreateBuilder<LocalisationLineSyntax>();
        var diagnostics = ImmutableArray.CreateBuilder<SyntaxDiagnostic>();
        string? language = null;
        var sawHeader = false;

        foreach (var line in EnumerateLines(source.Text))
        {
            var content = source.Text.AsSpan(line.Start, line.ContentEnd - line.Start);
            var first = FirstNonWhitespace(content);
            if (first == content.Length)
            {
                lines.Add(new LocalisationBlankLineSyntax(line.FullSpan));
                continue;
            }

            if (content[first] == '#')
            {
                lines.Add(new LocalisationCommentLineSyntax(line.FullSpan));
                continue;
            }

            if (TryParseHeader(content, line, first, out var header))
            {
                language = header.Language;
                sawHeader = true;
                lines.Add(header);
                continue;
            }

            if (TryParseEntry(content, line, first, language, out var entry))
            {
                lines.Add(entry);
                if (language is null)
                {
                    diagnostics.Add(new SyntaxDiagnostic(
                        "OXIDE1202",
                        DiagnosticSeverity.Warning,
                        "Localisation entry appears before a valid language header.",
                        entry.KeySpan));
                }

                continue;
            }

            lines.Add(new LocalisationUnknownLineSyntax(line.FullSpan));
            diagnostics.Add(new SyntaxDiagnostic(
                content[first..].StartsWith("l_", StringComparison.Ordinal)
                    ? "OXIDE1201"
                    : "OXIDE1203",
                DiagnosticSeverity.Error,
                content[first..].StartsWith("l_", StringComparison.Ordinal)
                    ? "Malformed localisation language header."
                    : "Malformed localisation entry.",
                new TextSpan(line.Start + first, content.Length - first)));
        }

        if (!sawHeader)
        {
            diagnostics.Add(new SyntaxDiagnostic(
                "OXIDE1204",
                DiagnosticSeverity.Error,
                "Localisation document does not contain a valid language header.",
                new TextSpan(0, 0)));
        }

        return new LocalisationSyntaxTree(source, lines.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool TryParseHeader(
        ReadOnlySpan<char> content,
        LineBounds line,
        int first,
        out LocalisationLanguageHeaderSyntax header)
    {
        header = null!;
        if (!content[first..].StartsWith("l_", StringComparison.Ordinal))
        {
            return false;
        }

        var colon = content[first..].IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        colon += first;
        var languageStart = first + 2;
        var languageLength = colon - languageStart;
        if (languageLength == 0 || !IsIdentifier(content.Slice(languageStart, languageLength)))
        {
            return false;
        }

        if (!ContainsOnlyWhitespaceOrComment(content[(colon + 1)..]))
        {
            return false;
        }

        header = new LocalisationLanguageHeaderSyntax(
            content.Slice(languageStart, languageLength).ToString(),
            new TextSpan(line.Start + languageStart, languageLength),
            line.FullSpan);
        return true;
    }

    private static bool TryParseEntry(
        ReadOnlySpan<char> content,
        LineBounds line,
        int first,
        string? language,
        out LocalisationEntrySyntax entry)
    {
        entry = null!;
        var colon = content[first..].IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        colon += first;
        var keyEnd = colon;
        while (keyEnd > first && char.IsWhiteSpace(content[keyEnd - 1]))
        {
            keyEnd--;
        }

        if (keyEnd == first || ContainsWhitespace(content[first..keyEnd]))
        {
            return false;
        }

        var cursor = colon + 1;
        var versionStart = cursor;
        while (cursor < content.Length && char.IsAsciiDigit(content[cursor]))
        {
            cursor++;
        }

        TextSpan? versionSpan = null;
        int? version = null;
        if (cursor > versionStart)
        {
            versionSpan = new TextSpan(line.Start + versionStart, cursor - versionStart);
            if (!int.TryParse(content[versionStart..cursor], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            version = parsed;
        }

        while (cursor < content.Length && char.IsWhiteSpace(content[cursor]))
        {
            cursor++;
        }

        if (cursor >= content.Length || content[cursor] != '"')
        {
            return false;
        }

        var quoteStart = cursor;
        var quoteEnd = FindClosingQuote(content, quoteStart + 1);
        if (quoteEnd < 0)
        {
            return false;
        }

        var valueStart = quoteStart + 1;
        var decoded = DecodeValue(content[valueStart..quoteEnd]);
        entry = new LocalisationEntrySyntax(
            language,
            content[first..keyEnd].ToString(),
            version,
            decoded,
            new TextSpan(line.Start + first, keyEnd - first),
            versionSpan,
            new TextSpan(line.Start + quoteStart, quoteEnd - quoteStart + 1),
            new TextSpan(line.Start + valueStart, quoteEnd - valueStart),
            line.FullSpan);
        return true;
    }

    private static int FindClosingQuote(ReadOnlySpan<char> content, int valueStart)
    {
        for (var index = content.Length - 1; index >= valueStart; index--)
        {
            if (content[index] == '"' && ContainsOnlyWhitespaceOrComment(content[(index + 1)..]))
            {
                return index;
            }
        }

        return -1;
    }

    private static string DecodeValue(ReadOnlySpan<char> value)
    {
        var decoded = new StringBuilder(value.Length);
        for (var cursor = 0; cursor < value.Length; cursor++)
        {
            var character = value[cursor];
            if (character != '\\' || cursor + 1 >= value.Length)
            {
                decoded.Append(character);
                continue;
            }

            var escaped = value[++cursor];
            decoded.Append(escaped switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => '\\',
            });
            if (escaped is not ('"' or '\\' or 'n' or 'r' or 't'))
            {
                decoded.Append(escaped);
            }
        }

        return decoded.ToString();
    }

    private static IEnumerable<LineBounds> EnumerateLines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var cursor = start;
            while (cursor < text.Length && text[cursor] is not ('\r' or '\n'))
            {
                cursor++;
            }

            var contentEnd = cursor;
            if (cursor < text.Length && text[cursor++] == '\r' && cursor < text.Length && text[cursor] == '\n')
            {
                cursor++;
            }

            yield return new LineBounds(start, contentEnd, new TextSpan(start, cursor - start));
            start = cursor;
        }
    }

    private static int FirstNonWhitespace(ReadOnlySpan<char> value)
    {
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsIdentifier(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsWhitespace(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOnlyWhitespaceOrComment(ReadOnlySpan<char> value)
    {
        var first = FirstNonWhitespace(value);
        return first == value.Length || value[first] == '#';
    }

    private readonly record struct LineBounds(int Start, int ContentEnd, TextSpan FullSpan);
}

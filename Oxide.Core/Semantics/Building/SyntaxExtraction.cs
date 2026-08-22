using System.Globalization;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Parsing;

namespace Oxide.Core.Semantics.Building;

internal static class SyntaxExtraction
{
    public static IEnumerable<PropertySyntax> Properties(BlockValueSyntax block, string key) =>
        block.Elements
            .OfType<PropertySyntax>()
            .Where(property => property.OperatorToken.Kind is SyntaxKind.EqualsToken
                && string.Equals(property.Key.Text, key, StringComparison.Ordinal));

    public static SourcedValue<string>? ReadString(SourceDocument document, PropertySyntax property)
    {
        if (property.Value is not ScalarValueSyntax scalar)
        {
            return null;
        }

        var value = scalar.Token.Kind is SyntaxKind.QuotedStringToken
            ? Unquote(scalar.Token.Text)
            : scalar.Token.Text;
        return new SourcedValue<string>(
            value,
            scalar.Token.Text,
            Provenance(document, scalar.Token.Span));
    }

    public static SourcedValue<int>? ReadInt32(SourceDocument document, SyntaxToken token) =>
        int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? new SourcedValue<int>(value, token.Text, Provenance(document, token.Span))
            : null;

    public static SourcedValue<long>? ReadInt64(SourceDocument document, PropertySyntax property)
    {
        if (property.Value is not ScalarValueSyntax scalar
            || !long.TryParse(scalar.Token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return new SourcedValue<long>(value, scalar.Token.Text, Provenance(document, scalar.Token.Span));
    }

    public static SourcedValue<decimal>? ReadDecimal(SourceDocument document, PropertySyntax property)
    {
        if (property.Value is not ScalarValueSyntax scalar
            || !decimal.TryParse(scalar.Token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return new SourcedValue<decimal>(value, scalar.Token.Text, Provenance(document, scalar.Token.Span));
    }

    public static SourceProvenance Provenance(SourceDocument document, Oxide.Syntax.Text.TextSpan span) =>
        new(document.Id, document.PhysicalPath, document.Layer, span);

    private static string Unquote(string text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return text.TrimStart('"');
        }

        var content = text[1..^1];
        return content.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}

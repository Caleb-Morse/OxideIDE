using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Parsing;

namespace Oxide.Core.Semantics.Building;

internal static class CountryTagDeclarationExtractor
{
    public static ExtractionResult<CountryTagDeclaration> Extract(SourceDocument document)
    {
        if (document.SyntaxTree is null)
        {
            return new ExtractionResult<CountryTagDeclaration>([], []);
        }

        var declarations = ImmutableArray.CreateBuilder<CountryTagDeclaration>();
        var diagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
        foreach (var property in document.SyntaxTree.Root.Elements.OfType<PropertySyntax>())
        {
            var definition = SyntaxExtraction.ReadString(document, property);
            if (definition is null || !LooksLikeCountryDefinition(definition.Value))
            {
                continue;
            }

            if (!IsCountryTag(property.Key.Text))
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "OXIDE4008",
                    DiagnosticSeverity.Error,
                    $"Country tag '{property.Key.Text}' must contain three ASCII letters or digits.",
                    null,
                    SyntaxExtraction.Provenance(document, property.Key.Span),
                    []));
                continue;
            }

            var normalizedTag = EntityId.NormalizeCountryTag(property.Key.Text);
            declarations.Add(new CountryTagDeclaration(
                document.Id,
                property.Key.Text,
                normalizedTag,
                definition,
                SyntaxExtraction.Provenance(document, property.Span)));
        }

        return new ExtractionResult<CountryTagDeclaration>(declarations.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool LooksLikeCountryDefinition(string value) =>
        value.StartsWith("countries/", StringComparison.OrdinalIgnoreCase)
        && value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private static bool IsCountryTag(string text) =>
        text.Length == 3 && text.All(char.IsAsciiLetterOrDigit);
}

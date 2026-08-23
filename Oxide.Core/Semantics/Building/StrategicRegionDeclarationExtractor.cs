using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Parsing;

namespace Oxide.Core.Semantics.Building;

internal static class StrategicRegionDeclarationExtractor
{
    public static ExtractionResult<StrategicRegionDeclaration> Extract(SourceDocument document)
    {
        if (document.SyntaxTree is null)
        {
            return new ExtractionResult<StrategicRegionDeclaration>([], []);
        }

        var declarations = ImmutableArray.CreateBuilder<StrategicRegionDeclaration>();
        var diagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
        var properties = document.SyntaxTree.Root.Elements
            .OfType<PropertySyntax>()
            .Where(property => property.OperatorToken.Kind is SyntaxKind.EqualsToken
                && string.Equals(property.Key.Text, "strategic_region", StringComparison.Ordinal))
            .ToArray();

        foreach (var property in properties)
        {
            if (property.Value is not BlockValueSyntax block)
            {
                diagnostics.Add(Diagnostic(
                    "OXIDE4010",
                    DiagnosticSeverity.Error,
                    "A strategic-region declaration must contain a block.",
                    null,
                    document,
                    property.Span));
                continue;
            }

            declarations.Add(ExtractDeclaration(document, property, block, diagnostics));
        }

        if (properties.Length == 0)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4010",
                DiagnosticSeverity.Error,
                "Strategic-region file contains no top-level strategic_region declaration.",
                null,
                document,
                document.SyntaxTree.Root.Span));
        }

        return new ExtractionResult<StrategicRegionDeclaration>(declarations.ToImmutable(), diagnostics.ToImmutable());
    }

    private static StrategicRegionDeclaration ExtractDeclaration(
        SourceDocument document,
        PropertySyntax property,
        BlockValueSyntax block,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var idProperties = SyntaxExtraction.Properties(block, "id").ToArray();
        var ids = ImmutableArray.CreateBuilder<SourcedValue<int>>();
        foreach (var idProperty in idProperties)
        {
            if (idProperty.Value is ScalarValueSyntax scalar
                && SyntaxExtraction.ReadInt32(document, scalar.Token) is { } id)
            {
                ids.Add(id);
            }
            else
            {
                diagnostics.Add(Diagnostic(
                    "OXIDE4011",
                    DiagnosticSeverity.Error,
                    "Strategic-region ID must be an integer scalar.",
                    null,
                    document,
                    idProperty.Value.Span));
            }
        }

        if (idProperties.Length == 0)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4011",
                DiagnosticSeverity.Error,
                "Strategic-region declaration is missing its ID.",
                null,
                document,
                property.Span));
        }
        else if (idProperties.Length > 1)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4012",
                DiagnosticSeverity.Error,
                "Strategic-region declaration contains more than one ID property.",
                null,
                document,
                block.Span));
        }

        var hasSingleIdProperty = idProperties.Length == 1;
        EntityId? entityId = hasSingleIdProperty && ids.Count == 1
            ? EntityId.StrategicRegion(ids[0].Value)
            : null;
        var names = ReadNames(document, block, entityId, diagnostics);
        var provinces = ReadProvinces(document, block, entityId, diagnostics);
        AddDuplicateProvinceDiagnostics(provinces, entityId, diagnostics);

        return new StrategicRegionDeclaration(
            document.Id,
            SyntaxExtraction.Provenance(document, property.Span),
            ids.ToImmutable(),
            hasSingleIdProperty,
            names,
            provinces);
    }

    private static ImmutableArray<SourcedValue<string>> ReadNames(
        SourceDocument document,
        BlockValueSyntax block,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var properties = SyntaxExtraction.Properties(block, "name").ToArray();
        var names = ImmutableArray.CreateBuilder<SourcedValue<string>>();
        foreach (var property in properties)
        {
            if (SyntaxExtraction.ReadString(document, property) is { } name)
            {
                names.Add(name);
            }
            else
            {
                diagnostics.Add(Diagnostic(
                    "OXIDE4013",
                    DiagnosticSeverity.Error,
                    "Strategic-region name must be a scalar localisation key.",
                    entityId,
                    document,
                    property.Value.Span));
            }
        }

        if (properties.Length > 1)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4013",
                DiagnosticSeverity.Warning,
                "Strategic-region name is declared more than once; all candidates are retained.",
                entityId,
                document,
                block.Span));
        }

        return names.ToImmutable();
    }

    private static ImmutableArray<SourcedValue<int>> ReadProvinces(
        SourceDocument document,
        BlockValueSyntax block,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var properties = SyntaxExtraction.Properties(block, "provinces").ToArray();
        var provinces = ImmutableArray.CreateBuilder<SourcedValue<int>>();
        if (properties.Length == 0)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4014",
                DiagnosticSeverity.Error,
                "Strategic-region declaration is missing its provinces block.",
                entityId,
                document,
                block.Span));
            return provinces.ToImmutable();
        }

        if (properties.Length > 1)
        {
            diagnostics.Add(Diagnostic(
                "OXIDE4014",
                DiagnosticSeverity.Warning,
                "Strategic-region provinces block is declared more than once; all candidates are retained.",
                entityId,
                document,
                block.Span));
        }

        foreach (var property in properties)
        {
            if (property.Value is not BlockValueSyntax provinceBlock)
            {
                diagnostics.Add(Diagnostic(
                    "OXIDE4014",
                    DiagnosticSeverity.Error,
                    "Strategic-region provinces must be declared as a block.",
                    entityId,
                    document,
                    property.Value.Span));
                continue;
            }

            foreach (var element in provinceBlock.Elements)
            {
                if (element is BareValueSyntax { Value: ScalarValueSyntax scalar }
                    && SyntaxExtraction.ReadInt32(document, scalar.Token) is { } province)
                {
                    provinces.Add(province);
                }
                else
                {
                    diagnostics.Add(Diagnostic(
                        "OXIDE4014",
                        DiagnosticSeverity.Error,
                        "Strategic-region province ID must be an integer.",
                        entityId,
                        document,
                        element.Span));
                }
            }
        }

        return provinces.ToImmutable();
    }

    private static void AddDuplicateProvinceDiagnostics(
        ImmutableArray<SourcedValue<int>> provinces,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        foreach (var group in provinces.GroupBy(province => province.Value).Where(group => group.Count() > 1))
        {
            var candidates = group.ToImmutableArray();
            diagnostics.Add(new SemanticDiagnostic(
                "OXIDE4015",
                DiagnosticSeverity.Warning,
                $"Province {group.Key} occurs more than once in the strategic-region declaration.",
                entityId,
                candidates[0].Provenance,
                candidates.Skip(1).Select(candidate => candidate.Provenance).ToImmutableArray()));
        }
    }

    private static SemanticDiagnostic Diagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        EntityId? entityId,
        SourceDocument document,
        Oxide.Syntax.Text.TextSpan span) =>
        new(code, severity, message, entityId, SyntaxExtraction.Provenance(document, span), []);
}

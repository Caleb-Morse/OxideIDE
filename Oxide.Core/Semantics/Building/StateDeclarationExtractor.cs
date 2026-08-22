using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Parsing;

namespace Oxide.Core.Semantics.Building;

internal static class StateDeclarationExtractor
{
    public static ExtractionResult<StateDeclaration> Extract(SourceDocument document)
    {
        var declarations = ImmutableArray.CreateBuilder<StateDeclaration>();
        var diagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
        if (document.SyntaxTree is null)
        {
            return new ExtractionResult<StateDeclaration>([], []);
        }

        var stateProperties = document.SyntaxTree.Root.Elements
            .OfType<PropertySyntax>()
            .Where(property => property.OperatorToken.Kind is Oxide.Syntax.Lexing.SyntaxKind.EqualsToken
                && string.Equals(property.Key.Text, "state", StringComparison.Ordinal))
            .ToArray();

        foreach (var stateProperty in stateProperties)
        {
            if (stateProperty.Value is not BlockValueSyntax stateBlock)
            {
                diagnostics.Add(CreateDiagnostic(
                    "OXIDE4001",
                    DiagnosticSeverity.Error,
                    "A state declaration must contain a block.",
                    null,
                    SyntaxExtraction.Provenance(document, stateProperty.Span)));
                continue;
            }

            declarations.Add(ExtractDeclaration(document, stateProperty, stateBlock, diagnostics));
        }

        if (stateProperties.Length == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                "OXIDE4001",
                DiagnosticSeverity.Error,
                "State file contains no top-level state declaration.",
                null,
                SyntaxExtraction.Provenance(document, document.SyntaxTree.Root.Span)));
        }

        return new ExtractionResult<StateDeclaration>(declarations.ToImmutable(), diagnostics.ToImmutable());
    }

    private static StateDeclaration ExtractDeclaration(
        SourceDocument document,
        PropertySyntax stateProperty,
        BlockValueSyntax stateBlock,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var idProperties = SyntaxExtraction.Properties(stateBlock, "id").ToArray();
        var ids = ImmutableArray.CreateBuilder<SourcedValue<int>>();
        foreach (var property in idProperties)
        {
            if (property.Value is ScalarValueSyntax scalar
                && SyntaxExtraction.ReadInt32(document, scalar.Token) is { } id)
            {
                ids.Add(id);
            }
            else
            {
                diagnostics.Add(CreateDiagnostic(
                    "OXIDE4002",
                    DiagnosticSeverity.Error,
                    "State ID must be an integer scalar.",
                    null,
                    SyntaxExtraction.Provenance(document, property.Value.Span)));
            }
        }

        EntityId? entityId = ids.Count == 1 ? EntityId.State(ids[0].Value) : null;
        if (idProperties.Length == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                "OXIDE4002",
                DiagnosticSeverity.Error,
                "State declaration is missing its ID.",
                null,
                SyntaxExtraction.Provenance(document, stateProperty.Span)));
        }
        else if (idProperties.Length > 1)
        {
            diagnostics.Add(CreateDiagnostic(
                "OXIDE4005",
                DiagnosticSeverity.Error,
                "State declaration contains more than one ID property.",
                null,
                SyntaxExtraction.Provenance(document, stateProperty.Span)));
        }

        var names = ReadStrings(document, stateBlock, "name", entityId, diagnostics);
        var categories = ReadStrings(document, stateBlock, "state_category", entityId, diagnostics);
        var manpower = ReadLongs(document, stateBlock, "manpower", entityId, diagnostics);
        var resources = ReadResources(document, stateBlock, entityId, diagnostics);
        var provinces = ReadProvinces(document, stateBlock, entityId, diagnostics);
        var histories = SyntaxExtraction.Properties(stateBlock, "history")
            .Select(property => property.Value)
            .OfType<BlockValueSyntax>()
            .ToArray();
        var owners = histories
            .SelectMany(history => ReadStrings(document, history, "owner", entityId, diagnostics))
            .ToImmutableArray();
        var cores = histories
            .SelectMany(history => ReadStrings(document, history, "add_core_of", entityId, diagnostics, allowMultiple: true))
            .ToImmutableArray();

        if (owners.Length > 1)
        {
            diagnostics.Add(CreateDiagnostic(
                "OXIDE4005",
                DiagnosticSeverity.Warning,
                "State declaration contains multiple initial owner properties.",
                entityId,
                SyntaxExtraction.Provenance(document, stateProperty.Span)));
        }

        return new StateDeclaration(
            document.Id,
            SyntaxExtraction.Provenance(document, stateProperty.Span),
            ids.ToImmutable(),
            names,
            manpower,
            categories,
            resources,
            provinces,
            owners,
            cores);
    }

    private static ImmutableArray<SourcedValue<string>> ReadStrings(
        SourceDocument document,
        BlockValueSyntax block,
        string key,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics,
        bool allowMultiple = false)
    {
        var properties = SyntaxExtraction.Properties(block, key).ToArray();
        var values = ImmutableArray.CreateBuilder<SourcedValue<string>>();
        foreach (var property in properties)
        {
            if (SyntaxExtraction.ReadString(document, property) is { } value)
            {
                values.Add(value);
            }
            else
            {
                diagnostics.Add(InvalidValue(document, property, entityId, key, "a scalar"));
            }
        }

        if (!allowMultiple && properties.Length > 1)
        {
            diagnostics.Add(DuplicateProperty(document, block, entityId, key));
        }

        return values.ToImmutable();
    }

    private static ImmutableArray<SourcedValue<long>> ReadLongs(
        SourceDocument document,
        BlockValueSyntax block,
        string key,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var properties = SyntaxExtraction.Properties(block, key).ToArray();
        var values = ImmutableArray.CreateBuilder<SourcedValue<long>>();
        foreach (var property in properties)
        {
            if (SyntaxExtraction.ReadInt64(document, property) is { } value)
            {
                values.Add(value);
            }
            else
            {
                diagnostics.Add(InvalidValue(document, property, entityId, key, "an integer"));
            }
        }

        if (properties.Length > 1)
        {
            diagnostics.Add(DuplicateProperty(document, block, entityId, key));
        }

        return values.ToImmutable();
    }

    private static ImmutableArray<NamedSourcedValue<decimal>> ReadResources(
        SourceDocument document,
        BlockValueSyntax stateBlock,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var values = ImmutableArray.CreateBuilder<NamedSourcedValue<decimal>>();
        foreach (var resourcesProperty in SyntaxExtraction.Properties(stateBlock, "resources"))
        {
            if (resourcesProperty.Value is not BlockValueSyntax resourcesBlock)
            {
                diagnostics.Add(InvalidValue(document, resourcesProperty, entityId, "resources", "a block"));
                continue;
            }

            foreach (var resourceProperty in resourcesBlock.Elements.OfType<PropertySyntax>())
            {
                if (SyntaxExtraction.ReadDecimal(document, resourceProperty) is { } value)
                {
                    values.Add(new NamedSourcedValue<decimal>(resourceProperty.Key.Text, value));
                }
                else
                {
                    diagnostics.Add(InvalidValue(
                        document,
                        resourceProperty,
                        entityId,
                        $"resource '{resourceProperty.Key.Text}'",
                        "a number"));
                }
            }
        }

        return values.ToImmutable();
    }

    private static ImmutableArray<SourcedValue<int>> ReadProvinces(
        SourceDocument document,
        BlockValueSyntax stateBlock,
        EntityId? entityId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var provinces = ImmutableArray.CreateBuilder<SourcedValue<int>>();
        foreach (var provincesProperty in SyntaxExtraction.Properties(stateBlock, "provinces"))
        {
            if (provincesProperty.Value is not BlockValueSyntax provincesBlock)
            {
                diagnostics.Add(InvalidValue(document, provincesProperty, entityId, "provinces", "a block"));
                continue;
            }

            foreach (var bareValue in provincesBlock.Elements.OfType<BareValueSyntax>())
            {
                if (bareValue.Value is ScalarValueSyntax scalar
                    && SyntaxExtraction.ReadInt32(document, scalar.Token) is { } province)
                {
                    provinces.Add(province);
                }
                else
                {
                    diagnostics.Add(CreateDiagnostic(
                        "OXIDE4004",
                        DiagnosticSeverity.Error,
                        "Province ID must be an integer.",
                        entityId,
                        SyntaxExtraction.Provenance(document, bareValue.Span)));
                }
            }
        }

        return provinces.ToImmutable();
    }

    private static SemanticDiagnostic InvalidValue(
        SourceDocument document,
        PropertySyntax property,
        EntityId? entityId,
        string propertyName,
        string expected) =>
        CreateDiagnostic(
            "OXIDE4004",
            DiagnosticSeverity.Error,
            $"Property '{propertyName}' must be {expected}.",
            entityId,
            SyntaxExtraction.Provenance(document, property.Value.Span));

    private static SemanticDiagnostic DuplicateProperty(
        SourceDocument document,
        BlockValueSyntax block,
        EntityId? entityId,
        string propertyName) =>
        CreateDiagnostic(
            "OXIDE4005",
            DiagnosticSeverity.Warning,
            $"Property '{propertyName}' is declared more than once; no effective value is selected.",
            entityId,
            SyntaxExtraction.Provenance(document, block.Span));

    private static SemanticDiagnostic CreateDiagnostic(
        string code,
        DiagnosticSeverity severity,
        string message,
        EntityId? entityId,
        SourceProvenance provenance) =>
        new(code, severity, message, entityId, provenance, []);
}

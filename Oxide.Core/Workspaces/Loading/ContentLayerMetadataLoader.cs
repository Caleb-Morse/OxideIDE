using System.Collections.Immutable;
using System.Security;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Loading;

internal static class ContentLayerMetadataLoader
{
    public static ImmutableArray<ContentLayer> Load(
        IEnumerable<ContentLayer> layers,
        ImmutableArray<WorkspaceDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken) =>
        layers.Select(layer => Load(layer, diagnostics, cancellationToken)).ToImmutableArray();

    private static ContentLayer Load(
        ContentLayer layer,
        ImmutableArray<WorkspaceDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (layer.Kind is ContentLayerKind.BaseGame || !Directory.Exists(layer.RootPath))
        {
            return layer;
        }

        var descriptorPath = Path.Combine(layer.RootPath, "descriptor.mod");
        if (!File.Exists(descriptorPath))
        {
            return layer;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = SourceText.Load(descriptorPath);
            var tree = ClausewitzParser.Parse(source);
            diagnostics.AddRange(tree.Diagnostics.Select(diagnostic => new WorkspaceDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                $"Mod descriptor: {diagnostic.Message}",
                descriptorPath,
                Span: diagnostic.Span)));

            var discoveredRules = ImmutableArray.CreateBuilder<ContentLayerReplacementRule>();
            foreach (var property in tree.Root.Elements.OfType<PropertySyntax>().Where(property =>
                property.OperatorToken.Kind is SyntaxKind.EqualsToken
                && string.Equals(property.Key.Text, "replace_path", StringComparison.Ordinal)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (property.Value is not ScalarValueSyntax scalar)
                {
                    diagnostics.Add(new WorkspaceDiagnostic(
                        "OXIDE3011",
                        DiagnosticSeverity.Error,
                        "A mod descriptor replace_path must be a scalar relative path.",
                        descriptorPath,
                        Span: property.Value.Span));
                    continue;
                }

                var value = Unquote(scalar.Token.Text);
                try
                {
                    discoveredRules.Add(new ContentLayerReplacementRule(
                        new VirtualPath(value),
                        descriptorPath,
                        scalar.Token.Span));
                }
                catch (ArgumentException exception)
                {
                    diagnostics.Add(new WorkspaceDiagnostic(
                        "OXIDE3011",
                        DiagnosticSeverity.Error,
                        $"Invalid mod descriptor replace_path: {exception.Message}",
                        descriptorPath,
                        Span: scalar.Token.Span));
                }
            }

            var rules = layer.ReplacementRules
                .AddRange(discoveredRules)
                .DistinctBy(rule => rule.Path, VirtualPathComparer.GamePath)
                .OrderBy(rule => rule.Path, VirtualPathComparer.GamePath)
                .ToImmutableArray();
            return layer with { ReplacementRules = rules };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            diagnostics.Add(new WorkspaceDiagnostic(
                "OXIDE3010",
                DiagnosticSeverity.Error,
                $"Could not load mod descriptor '{descriptorPath}': {exception.Message}",
                descriptorPath));
            return layer;
        }
    }

    private static string Unquote(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"'
            ? text[1..^1]
            : text.TrimStart('"');
}

using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Building;

internal static class LocalisationDeclarationExtractor
{
    public static ImmutableArray<LocalisationDeclaration> Extract(SourceDocument document)
    {
        var tree = document.LocalisationSyntaxTree
            ?? throw new InvalidOperationException("A loaded localisation document must have a localisation syntax tree.");

        return tree.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Language))
            .Select(entry =>
            {
                var valueProvenance = new SourceProvenance(
                    document.Id,
                    document.PhysicalPath,
                    document.Layer,
                    entry.ValueSpan);
                var declarationProvenance = valueProvenance with { Span = entry.FullSpan };
                return new LocalisationDeclaration(
                    new LocalisationLanguage(entry.Language!),
                    new LocalisationKey(entry.Key),
                    entry.Version,
                    new SourcedValue<string>(entry.Value, tree.Source.GetText(entry.ValueSpan), valueProvenance),
                    declarationProvenance);
            })
            .ToImmutableArray();
    }
}

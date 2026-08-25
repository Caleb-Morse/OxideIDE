using System.Collections.Immutable;

namespace Oxide.Core.Workspaces.Documents;

public static class SupportedContentProfile
{
    public static ImmutableArray<SupportedContentRule> Rules { get; } =
    [
        new(new VirtualPath("history/states"), ".txt", false, SourceDocumentKind.Clausewitz, ContentCategory.StateHistory),
        new(new VirtualPath("map/strategicregions"), ".txt", false, SourceDocumentKind.Clausewitz, ContentCategory.StrategicRegion),
        new(new VirtualPath("common/country_tags"), ".txt", false, SourceDocumentKind.Clausewitz, ContentCategory.CountryTags),
        new(new VirtualPath("localisation"), ".yml", true, SourceDocumentKind.Localisation, ContentCategory.Localisation),
    ];

    public static bool TryClassify(
        VirtualPath path,
        out SourceDocumentKind documentKind,
        out ContentCategory category)
    {
        foreach (var rule in Rules)
        {
            if (!rule.Matches(path))
            {
                continue;
            }

            documentKind = rule.DocumentKind;
            category = rule.Category;
            return true;
        }

        documentKind = default;
        category = default;
        return false;
    }
}

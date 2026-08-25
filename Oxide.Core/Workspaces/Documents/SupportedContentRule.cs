namespace Oxide.Core.Workspaces.Documents;

public sealed record SupportedContentRule(
    VirtualPath Directory,
    string Extension,
    bool IncludeSubdirectories,
    SourceDocumentKind DocumentKind,
    ContentCategory Category)
{
    public bool Matches(VirtualPath path)
    {
        if (!path.Value.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = $"{Directory.Value}/";
        if (!path.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path.Value[prefix.Length..];
        return remainder.Length > 0
            && (IncludeSubdirectories || !remainder.Contains('/', StringComparison.Ordinal));
    }
}

using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public static class WorkspaceChangeClassifier
{
    public static WorkspaceChangePathResult Classify(ContentLayer layer, string physicalPath)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalPath);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(layer.RootPath));
        var candidate = Path.GetFullPath(physicalPath);
        var relativePath = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return new WorkspaceChangePathResult(
                WorkspaceChangePathStatus.OutsideContentLayer,
                null,
                null,
                null);
        }

        VirtualPath virtualPath;
        try
        {
            virtualPath = new VirtualPath(relativePath);
        }
        catch (ArgumentException)
        {
            return new WorkspaceChangePathResult(
                WorkspaceChangePathStatus.OutsideContentLayer,
                null,
                null,
                null);
        }

        var source = new SourceIdentity(
            DocumentId.Create(layer.Id, virtualPath),
            layer.Id,
            virtualPath,
            candidate);
        if (!SupportedContentProfile.TryClassify(virtualPath, out var documentKind, out var category))
        {
            return new WorkspaceChangePathResult(
                WorkspaceChangePathStatus.Unsupported,
                source,
                null,
                null);
        }

        return new WorkspaceChangePathResult(
            WorkspaceChangePathStatus.Supported,
            source,
            documentKind,
            category);
    }
}

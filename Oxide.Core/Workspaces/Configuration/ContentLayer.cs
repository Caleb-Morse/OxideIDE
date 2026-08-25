using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Configuration;

public sealed record ContentLayer(
    ContentLayerId Id,
    string DisplayName,
    ContentLayerKind Kind,
    string RootPath,
    int Position,
    bool IsWritable,
    bool IsEnabled = true,
    ImmutableArray<ContentLayerReplacementRule> ReplacementRules = default)
{
    public static ContentLayer BaseGame(string rootPath) =>
        new(new ContentLayerId("base-game"), "Base game", ContentLayerKind.BaseGame, rootPath, 0, IsWritable: false);

    public static ContentLayer ActiveMod(string rootPath) =>
        new(new ContentLayerId("active-mod"), "Active mod", ContentLayerKind.Mod, rootPath, 1, IsWritable: true);

    public static ContentLayer Mod(
        string id,
        string displayName,
        string rootPath,
        int position,
        bool isWritable = true,
        bool isEnabled = true,
        IEnumerable<string>? replacePaths = null) =>
        new(
            new ContentLayerId(id),
            displayName,
            ContentLayerKind.Mod,
            rootPath,
            position,
            isWritable,
            isEnabled,
            replacePaths?.Select(path => new ContentLayerReplacementRule(new VirtualPath(path))).ToImmutableArray()
                ?? []);
}

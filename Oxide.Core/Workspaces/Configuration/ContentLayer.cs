namespace Oxide.Core.Workspaces.Configuration;

public sealed record ContentLayer(
    ContentLayerId Id,
    string DisplayName,
    ContentLayerKind Kind,
    string RootPath,
    int Position,
    bool IsWritable,
    bool IsEnabled = true)
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
        bool isEnabled = true) =>
        new(new ContentLayerId(id), displayName, ContentLayerKind.Mod, rootPath, position, isWritable, isEnabled);
}

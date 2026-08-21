namespace Oxide.Core.Workspaces.Configuration;

public sealed record ContentLayer(
    string Id,
    ContentLayerKind Kind,
    string RootPath,
    int Position,
    bool IsWritable)
{
    public static ContentLayer BaseGame(string rootPath) =>
        new("base-game", ContentLayerKind.BaseGame, rootPath, 0, IsWritable: false);

    public static ContentLayer ActiveMod(string rootPath) =>
        new("active-mod", ContentLayerKind.ActiveMod, rootPath, 1, IsWritable: true);
}

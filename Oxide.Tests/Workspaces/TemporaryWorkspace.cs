using System.Text;

namespace Oxide.Tests.Workspaces;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "oxide-tests", Guid.NewGuid().ToString("N"));
        GameRoot = Path.Combine(Root, "game");
        ModRoot = Path.Combine(Root, "mod");
        Directory.CreateDirectory(GameRoot);
        Directory.CreateDirectory(ModRoot);
    }

    public string Root { get; }

    public string GameRoot { get; }

    public string ModRoot { get; }

    public string WriteGameFile(string virtualPath, string text) =>
        WriteFile(GameRoot, virtualPath, Encoding.UTF8.GetBytes(text));

    public string WriteModFile(string virtualPath, string text) =>
        WriteFile(ModRoot, virtualPath, Encoding.UTF8.GetBytes(text));

    public string WriteGameBytes(string virtualPath, byte[] bytes) =>
        WriteFile(GameRoot, virtualPath, bytes);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static string WriteFile(string root, string virtualPath, byte[] bytes)
    {
        var path = Path.Combine(root, virtualPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

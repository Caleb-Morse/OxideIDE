namespace Oxide.Core.Workspaces.Documents;

public sealed class VirtualPathComparer : IEqualityComparer<VirtualPath>, IComparer<VirtualPath>
{
    public static VirtualPathComparer GamePath { get; } = new();

    private VirtualPathComparer()
    {
    }

    public bool Equals(VirtualPath x, VirtualPath y) =>
        StringComparer.OrdinalIgnoreCase.Equals(x.Value, y.Value);

    public int GetHashCode(VirtualPath obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);

    public int Compare(VirtualPath x, VirtualPath y) =>
        StringComparer.OrdinalIgnoreCase.Compare(x.Value, y.Value);
}

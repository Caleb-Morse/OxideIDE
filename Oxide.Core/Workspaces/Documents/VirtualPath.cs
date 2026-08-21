namespace Oxide.Core.Workspaces.Documents;

public readonly record struct VirtualPath : IComparable<VirtualPath>
{
    public VirtualPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Virtual paths must be non-empty relative paths without traversal.", nameof(value));
        }

        Value = string.Join('/', segments);
    }

    public string Value { get; }

    public int CompareTo(VirtualPath other) => StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}

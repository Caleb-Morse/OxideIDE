namespace Oxide.Core.Workspaces.Configuration;

public readonly record struct ContentLayerId
{
    public ContentLayerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

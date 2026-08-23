namespace Oxide.Core.Semantics.Identity;

public readonly record struct LocalisationKey
{
    public LocalisationKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

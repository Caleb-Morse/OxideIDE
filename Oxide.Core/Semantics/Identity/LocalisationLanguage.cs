namespace Oxide.Core.Semantics.Identity;

public readonly record struct LocalisationLanguage
{
    public LocalisationLanguage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = Normalize(value);
    }

    public string Value { get; }

    public static string Normalize(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            var language when language.StartsWith("l_", StringComparison.Ordinal) => language[2..],
            var language => language,
        };

    public override string ToString() => Value;
}

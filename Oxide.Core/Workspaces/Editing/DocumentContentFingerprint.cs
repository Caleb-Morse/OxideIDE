using System.Security.Cryptography;

namespace Oxide.Core.Workspaces.Editing;

public readonly record struct DocumentContentFingerprint
{
    private const int Sha256HexLength = 64;

    public DocumentContentFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != Sha256HexLength || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A document fingerprint must be a 64-character SHA-256 hexadecimal value.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public static DocumentContentFingerprint Create(ReadOnlySpan<byte> content) =>
        new(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

    public override string ToString() => Value;
}

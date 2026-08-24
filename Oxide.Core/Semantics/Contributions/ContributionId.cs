using System.Security.Cryptography;
using System.Text;
using Oxide.Core.Semantics.Model;

namespace Oxide.Core.Semantics.Contributions;

public readonly record struct ContributionId
{
    public ContributionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public static ContributionId Create(SourceProvenance provenance)
    {
        var input = Encoding.UTF8.GetBytes(
            $"{provenance.DocumentId.Value}\n{provenance.Span.Start}\n{provenance.Span.Length}");
        return new ContributionId(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
    }

    public override string ToString() => Value;
}

using System.Security.Cryptography;
using System.Text;

namespace Oxide.Core.Workspaces.Documents;

public readonly record struct DocumentId(string Value)
{
    public static DocumentId Create(string layerId, VirtualPath virtualPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        var input = Encoding.UTF8.GetBytes($"{layerId}\n{virtualPath.Value}");
        return new DocumentId(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
    }

    public override string ToString() => Value;
}

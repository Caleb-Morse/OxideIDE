using System.Security.Cryptography;
using System.Text;
using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Core.Workspaces.Documents;

public readonly record struct DocumentId(string Value)
{
    public static DocumentId Create(ContentLayerId layerId, VirtualPath virtualPath)
    {
        var input = Encoding.UTF8.GetBytes($"{layerId.Value}\n{virtualPath.Value}");
        return new DocumentId(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());
    }

    public override string ToString() => Value;
}

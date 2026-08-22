namespace Oxide.Core.Verification;

public sealed record ReferenceResolutionCounts(
    int Total,
    int Resolved,
    int Missing,
    int Ambiguous,
    int Invalid)
{
    public int Unresolved => Missing + Ambiguous + Invalid;
}

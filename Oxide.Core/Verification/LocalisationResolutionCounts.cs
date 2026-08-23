namespace Oxide.Core.Verification;

public sealed record LocalisationResolutionCounts(
    int Total,
    int Exact,
    int EnglishFallback,
    int Missing,
    int Ambiguous,
    int Invalid,
    int NoKey)
{
    public int Unresolved => Missing + Ambiguous + Invalid + NoKey;
}

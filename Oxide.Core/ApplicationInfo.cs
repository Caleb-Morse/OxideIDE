namespace Oxide.Core;

/// <summary>
/// Stable application metadata exposed to presentation layers.
/// </summary>
public sealed record ApplicationInfo(string Name)
{
    public static ApplicationInfo Oxide { get; } = new("Oxide");
}

namespace Oxide.Core.Verification;

public sealed record CorpusSummaryOptions(
    string RequestedLanguage = "english",
    bool EnglishFallbackEnabled = true);

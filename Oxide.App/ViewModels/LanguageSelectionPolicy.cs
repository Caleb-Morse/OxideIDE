using System.Collections.Immutable;
using Oxide.Core.Semantics.Identity;

namespace Oxide.App.ViewModels;

internal static class LanguageSelectionPolicy
{
    public static string NormalizePreference(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "english" : LocalisationLanguage.Normalize(language);

    public static string ChooseEffective(
        string preferredLanguage,
        ImmutableArray<string> availableLanguages)
    {
        if (availableLanguages.Contains(preferredLanguage, StringComparer.Ordinal))
        {
            return preferredLanguage;
        }

        if (availableLanguages.Contains("english", StringComparer.Ordinal))
        {
            return "english";
        }

        return availableLanguages.FirstOrDefault() ?? "english";
    }
}

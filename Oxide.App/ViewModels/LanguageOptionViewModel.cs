using System.Globalization;

namespace Oxide.App.ViewModels;

public sealed record LanguageOptionViewModel(string Id, string DisplayName)
{
    public static LanguageOptionViewModel Create(string id)
    {
        var displayName = id switch
        {
            "braz_por" => "🇧🇷  Português (Brasil)",
            "english" => "🇬🇧  English",
            "french" => "🇫🇷  Français",
            "german" => "🇩🇪  Deutsch",
            "japanese" => "🇯🇵  日本語",
            "korean" => "🇰🇷  한국어",
            "polish" => "🇵🇱  Polski",
            "russian" => "🇷🇺  Русский",
            "simp_chinese" => "🇨🇳  简体中文",
            "spanish" => "🇪🇸  Español",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ')),
        };
        return new LanguageOptionViewModel(id, displayName);
    }
}

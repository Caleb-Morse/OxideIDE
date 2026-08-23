using System.Globalization;

namespace Oxide.App.ViewModels;

public sealed record LanguageOptionViewModel(string Id, string DisplayName)
{
    public static LanguageOptionViewModel Create(string id)
    {
        var displayName = id switch
        {
            "english" => "English",
            "spanish" => "Español",
            "russian" => "Русский",
            "simp_chinese" => "简体中文",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ')),
        };
        return new LanguageOptionViewModel(id, displayName);
    }
}

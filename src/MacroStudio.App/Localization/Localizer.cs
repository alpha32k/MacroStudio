using System.ComponentModel;
using System.Globalization;
using System.Resources;
using MacroStudio.Core.Settings;

namespace MacroStudio.App.Localization;

/// <summary>
/// Gives every visible text in the current language and switches the language at runtime.
/// XAML binds to the indexer, so changing the language updates open windows without a restart.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager Strings =
        new("MacroStudio.App.Resources.Strings", typeof(Localizer).Assembly);

    private Localizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Localizer Instance { get; } = new();

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A missing key is shown as !Key! so it is easy to spot instead of crashing.</summary>
    public string this[string key] => Strings.GetString(key, Culture) ?? $"!{key}!";

    public string Format(string key, params object[] arguments) =>
        string.Format(Culture, this[key], arguments);

    public void SetLanguage(AppLanguage language)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(language == AppLanguage.Turkish ? "tr-TR" : "en-US");
        if (Equals(culture, Culture))
        {
            return;
        }

        Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // "Item[]" refreshes every binding to the indexer.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}

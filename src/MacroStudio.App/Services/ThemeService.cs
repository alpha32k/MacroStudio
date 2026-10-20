using System.Windows;
using System.Windows.Media;
using MacroStudio.Core.Settings;
using MacroStudio.Native.WindowsSettings;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Services;

/// <summary>
/// Applies light, dark or high-contrast colors and the accent color, and follows Windows when the user
/// changes its theme or accent while the app runs. Mica appears on Windows 11; Windows 10 gets a solid background.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private readonly SettingsService _settings;

    public ThemeService(SettingsService settings)
    {
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnWindowsPreferenceChanged;
    }

    public void Apply()
    {
        AppSettings settings = _settings.Current;
        ApplicationTheme theme = ResolveTheme(settings.Theme);
        bool useWindowsAccent = settings.AccentColor is null || theme == ApplicationTheme.HighContrast;

        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: useWindowsAccent);
        if (!useWindowsAccent)
        {
            var accent = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
            ApplicationAccentColorManager.Apply(accent, theme, systemGlassColor: false, systemAccentColor: false);
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnWindowsPreferenceChanged;
    }

    private static ApplicationTheme ResolveTheme(ThemePreference preference)
    {
        if (SystemParameters.HighContrast)
        {
            return ApplicationTheme.HighContrast;
        }

        return preference switch
        {
            ThemePreference.Light => ApplicationTheme.Light,
            ThemePreference.Dark => ApplicationTheme.Dark,
            _ => WindowsAppearance.AppsUseLightTheme() ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => Apply();

    // Windows raises this event on its own thread; the theme must change on the UI thread.
    private void OnWindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General
            or UserPreferenceCategory.Color
            or UserPreferenceCategory.Accessibility
            or UserPreferenceCategory.VisualStyle)
        {
            Application.Current.Dispatcher.BeginInvoke(Apply);
        }
    }
}

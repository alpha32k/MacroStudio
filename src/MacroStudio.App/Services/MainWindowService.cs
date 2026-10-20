using System.Windows;
using MacroStudio.App.Pages;
using MacroStudio.App.Shell;

namespace MacroStudio.App.Services;

public sealed class MainWindowService(MainWindow window)
{
    public void Show()
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    public void ShowSettings() => ShowPage(typeof(SettingsPage));

    public void ShowPage(Type pageType)
    {
        Show();
        window.Navigate(pageType);
    }

    /// <summary>Gets the window out of the way, for example while a macro started from the editor plays.</summary>
    public void Minimize()
    {
        if (window.IsVisible)
        {
            window.WindowState = WindowState.Minimized;
        }
    }
}

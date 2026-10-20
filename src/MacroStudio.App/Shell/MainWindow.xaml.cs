using System.ComponentModel;
using MacroStudio.App.Controls;
using MacroStudio.App.Localization;
using MacroStudio.App.Pages;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Shell;

/// <summary>
/// The window behind the bar's home button. Closing it only hides it; the bar keeps running.
/// Exit is in the bar's and the tray icon's menu.
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();

        // Pages come from the service container, so they get their view models injected.
        Navigation.SetServiceProvider(services);
        TemplateButtonNames.NameWhenLoaded(this);
        Localizer.Instance.PropertyChanged += (_, _) => TemplateButtonNames.Name(this);
        Loaded += (_, _) =>
        {
            if (Navigation.SelectedItem is null)
            {
                Navigation.Navigate(typeof(HomePage));
            }
        };
    }

    public void Navigate(Type pageType) => Navigation.Navigate(pageType);

    // Application.Shutdown ignores this cancel, so exiting the app still closes the window.
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}

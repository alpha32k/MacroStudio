using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using MacroStudio.App.NotchBar;
using MacroStudio.Native.Windowing;
using ContextMenu = System.Windows.Controls.ContextMenu;

namespace MacroStudio.App.Services;

/// <summary>
/// The icon next to the clock. Left click opens the main window, right click shows the same menu as the bar.
/// It keeps the app reachable when the notch bar is hidden.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotchBarViewModel _commands;
    private readonly ContextMenu _menu;
    private readonly NotifyIcon _icon;

    public TrayIcon(NotchBarViewModel commands)
    {
        _commands = commands;
        _menu = (ContextMenu)System.Windows.Application.Current.FindResource("AppContextMenu");
        _menu.DataContext = commands;
        _menu.Opened += OnMenuOpened;

        using Stream iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/MacroStudio.ico")).Stream;
        _icon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, SystemInformation.SmallIconSize),
            Text = "Macro Studio",
            Visible = true,
        };
        _icon.MouseUp += OnMouseUp;
    }

    public void ShowWarning(string message) =>
        _icon.ShowBalloonTip(5000, "Macro Studio", message, ToolTipIcon.Warning);

    public void ShowInfo(string message) =>
        _icon.ShowBalloonTip(3000, "Macro Studio", message, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }

    // Without bringing the menu to the front, Windows would not close it when the user clicks elsewhere.
    private static void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        if (PresentationSource.FromVisual((ContextMenu)sender) is HwndSource source)
        {
            WindowInterop.BringToForeground(source.Handle);
        }
    }

    private void OnMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _commands.OpenMainWindowCommand.Execute(null);
        }
        else if (e.Button == MouseButtons.Right)
        {
            _menu.Placement = PlacementMode.MousePoint;
            _menu.IsOpen = true;
        }
    }
}

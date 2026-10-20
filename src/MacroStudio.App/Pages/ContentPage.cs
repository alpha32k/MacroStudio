using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MacroStudio.App.Pages;

/// <summary>
/// Base of every page in the main window.
/// WPF UI already places each page in a scroll viewer, so a page must not add its own: a second one would get
/// unlimited height, never scroll, and still swallow the mouse wheel.
/// The page takes the keyboard focus when it opens and when its empty area is clicked, so the arrow keys,
/// Page Up/Down and Ctrl+Home/End scroll it instead of moving through the menu on the left.
/// </summary>
public class ContentPage : Page
{
    public ContentPage()
    {
        Focusable = true;
        FocusVisualStyle = null;

        // A transparent background makes the empty area clickable.
        Background = Brushes.Transparent;
        Loaded += (_, _) => Focus();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
    }

    /// <summary>For buttons whose menu opens with a normal click ("Add step", "⋯"), below the button.</summary>
    protected void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}

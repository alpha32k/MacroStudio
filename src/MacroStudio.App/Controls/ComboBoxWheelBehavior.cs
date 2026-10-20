using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MacroStudio.App.Controls;

/// <summary>
/// A closed combo box that has the focus changes its value on the mouse wheel. While scrolling a page, or with
/// the pointer resting on the bar, that changes a setting or the repeat count without the user noticing.
/// For every combo box in the app, a closed one passes the wheel on to its parent (a page then scrolls);
/// an open one still scrolls its own list.
/// </summary>
public static class ComboBoxWheelBehavior
{
    public static void Register() =>
        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel));

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var comboBox = (ComboBox)sender;
        if (e.Handled || comboBox.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;
        if (VisualTreeHelper.GetParent(comboBox) is UIElement parent)
        {
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent });
        }
    }
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using MacroStudio.App.Localization;

namespace MacroStudio.App.Controls;

/// <summary>
/// WPF UI's title bar buttons and menu button have no accessible names, so screen readers only say "button".
/// This gives them names in the current language.
/// </summary>
public static class TemplateButtonNames
{
    private static readonly Dictionary<string, string> KeysByAutomationId = new()
    {
        ["TitleBarMinimizeButton"] = "Window_Minimize",
        ["TitleBarMaximizeButton"] = "Window_Maximize",
        ["TitleBarCloseButton"] = "Window_Close",
        ["NavigationToggleButton"] = "Window_ToggleMenu",
    };

    /// <summary>Names the buttons once the window shows them.</summary>
    public static void NameWhenLoaded(Window window) => window.Loaded += (_, _) => Name(window);

    public static void Name(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement element
                && KeysByAutomationId.TryGetValue(AutomationIdOf(element), out string? key))
            {
                AutomationProperties.SetName(element, Localizer.Instance[key]);
            }

            Name(child);
        }
    }

    private static string AutomationIdOf(FrameworkElement element) =>
        AutomationProperties.GetAutomationId(element) is { Length: > 0 } id ? id : element.Name;
}

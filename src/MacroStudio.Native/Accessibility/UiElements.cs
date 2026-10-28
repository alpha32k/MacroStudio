using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;
using Condition = System.Windows.Automation.Condition;

namespace MacroStudio.Native.Accessibility;

/// <summary>
/// Clicked controls through Windows UI Automation. Only small, named controls are described: buttons, menu items,
/// tabs, fields. A spreadsheet cell or a whole page is clicked by position, because looking for it would be slow
/// (Excel) or would find another one with the same name.
/// </summary>
public sealed class UiElements : IUiElements
{
    // A control larger than this part of its window is a page or a document, not a button.
    private const double MaxWindowShare = 0.25;

    private static readonly Dictionary<string, ControlType> Findable = new[]
    {
        ControlType.Button, ControlType.SplitButton, ControlType.MenuItem, ControlType.TabItem, ControlType.ListItem,
        ControlType.TreeItem, ControlType.CheckBox, ControlType.RadioButton, ControlType.Hyperlink, ControlType.ComboBox,
        ControlType.Edit,
    }.ToDictionary(type => type.ProgrammaticName, StringComparer.Ordinal);

    public UiElement? Describe(ScreenPoint point, ScreenRect windowBounds)
    {
        try
        {
            AutomationElement element = FindableAncestor(AutomationElement.FromPoint(new Point(point.X, point.Y)));
            AutomationElement.AutomationElementInformation info = element.Current;
            Rect bounds = info.BoundingRectangle;
            double windowArea = (double)windowBounds.Width * windowBounds.Height;
            bool isFindable = Findable.ContainsKey(info.ControlType.ProgrammaticName)
                && !info.IsPassword
                && (info.AutomationId.Length > 0 || info.Name.Length > 0)
                && !bounds.IsEmpty && bounds.Width >= 1 && bounds.Height >= 1
                && bounds.Width * bounds.Height <= windowArea * MaxWindowShare;
            if (!isFindable)
            {
                return null;
            }

            return new UiElement(
                info.AutomationId,
                info.Name,
                info.ControlType.ProgrammaticName,
                info.ClassName,
                Math.Clamp((point.X - bounds.Left) / bounds.Width, 0, 1),
                Math.Clamp((point.Y - bounds.Top) / bounds.Height, 0, 1));
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            // The program does not answer UI Automation or the control just went away: the click keeps its position.
            return null;
        }
    }

    /// <summary>The point often hits the text or icon inside a button; the button itself is a few levels up.</summary>
    private static AutomationElement FindableAncestor(AutomationElement hit)
    {
        AutomationElement? current = hit;
        for (int level = 0; level < 3 && current is not null; level++)
        {
            if (Findable.ContainsKey(current.Current.ControlType.ProgrammaticName))
            {
                return current;
            }

            current = TreeWalker.ControlViewWalker.GetParent(current);
        }

        return hit;
    }

    public ScreenRect? Find(nint window, UiElement element, ScreenPoint near, TimeSpan timeout)
    {
        if (!Findable.TryGetValue(element.ControlType, out ControlType? type))
        {
            return null;
        }

        // UI Automation calls cannot be cancelled; a slow program is left to answer on its own thread.
        Task<ScreenRect?> search = Task.Run(() => Search(window, element, type, near));
        try
        {
            return search.Wait(timeout) ? search.Result : null;
        }
        catch (AggregateException)
        {
            return null;
        }
    }

    private static ScreenRect? Search(nint window, UiElement element, ControlType type, ScreenPoint near)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(window);

            // The program's own id first; a control whose id changes between runs is still found by its name.
            ScreenRect? found = element.AutomationId.Length > 0
                ? Nearest(root, new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, element.AutomationId),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, type)), near)
                : null;
            if (found is null && element.Name.Length > 0)
            {
                found = Nearest(root, new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, element.Name),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, type)), near);
            }

            return found;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Of the visible matches, the one whose center is nearest to the recorded click.</summary>
    private static ScreenRect? Nearest(AutomationElement root, Condition condition, ScreenPoint near)
    {
        ScreenRect? best = null;
        double bestDistance = double.MaxValue;
        foreach (AutomationElement match in root.FindAll(TreeScope.Descendants, condition))
        {
            AutomationElement.AutomationElementInformation info = match.Current;
            Rect bounds = info.BoundingRectangle;
            if (info.IsOffscreen || bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
            {
                continue;
            }

            double dx = bounds.Left + (bounds.Width / 2) - near.X;
            double dy = bounds.Top + (bounds.Height / 2) - near.Y;
            double distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = new ScreenRect((int)bounds.Left, (int)bounds.Top, (int)bounds.Right, (int)bounds.Bottom);
            }
        }

        return best;
    }
}

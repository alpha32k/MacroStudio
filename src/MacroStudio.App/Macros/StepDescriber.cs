using MacroStudio.App.Localization;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Macros;

/// <summary>Readable text and an icon for each kind of step, in the current language.</summary>
public static class StepDescriber
{
    private const int MaxShownLength = 40;
    private const double WheelNotch = 120;

    public static IReadOnlyList<StepRow> ToRows(IReadOnlyList<MacroStep> steps) =>
        steps.Select((step, index) => new StepRow(index + 1, IconOf(step), Describe(step), step.DelayBeforeMs)).ToList();

    public static string Describe(MacroStep step)
    {
        Localizer text = Localizer.Instance;
        return step switch
        {
            MouseClickStep click => WithWindow(ClickName(click), click.Target, click.ScreenPosition),
            MouseDragStep drag => WithWindow(text["Step_Drag"], drag.Target, drag.ScreenPath[0]),
            MouseWheelStep wheel => WithWindow(text.Format(WheelKey(wheel), Notches(wheel)), wheel.Target, wheel.ScreenPosition),
            KeyPressStep key => WithWindow(Repeated("Step_Key", KeyNames.Of(key.VirtualKey), key.PressCount), key.Target),
            HotkeyStep hotkey => WithWindow(Repeated("Step_Hotkey", KeyNames.WithModifiers(hotkey.Modifiers, hotkey.VirtualKey), hotkey.PressCount), hotkey.Target),
            TypeTextStep typed => WithWindow(text.Format("Step_Type", Shorten(typed.Text)), typed.Target),
            FocusWindowStep focus => WithWindow(text["Step_Focus"], focus.Target),
            CopyStep copy => WithWindow(text["Step_Copy"], copy.Target),
            PasteStep paste => WithWindow(text["Step_Paste"], paste.Target),
            WaitStep { IsPasswordEntry: true } => text["Step_Password"],
            WaitStep wait => text.Format("Step_Wait", wait.DurationMs),
            _ => step.GetType().Name,
        };
    }

    public static SymbolRegular IconOf(MacroStep step) => step switch
    {
        MouseClickStep => SymbolRegular.CursorClick24,
        MouseDragStep => SymbolRegular.ArrowMove24,
        MouseWheelStep => SymbolRegular.ArrowSort24,
        KeyPressStep or HotkeyStep => SymbolRegular.Keyboard24,
        TypeTextStep => SymbolRegular.TextT24,
        FocusWindowStep => SymbolRegular.Window24,
        CopyStep => SymbolRegular.Copy24,
        PasteStep => SymbolRegular.ClipboardPaste24,
        WaitStep { IsPasswordEntry: true } => SymbolRegular.LockClosed24,
        _ => SymbolRegular.Timer24,
    };

    private static string ClickName(MouseClickStep click)
    {
        string name = click.ClickCount == 2
            ? Localizer.Instance["Click_Double"]
            : Localizer.Instance[click.Button switch
            {
                MouseButton.Right => "Click_Right",
                MouseButton.Middle => "Click_Middle",
                MouseButton.X1 or MouseButton.X2 => "Click_Side",
                _ => "Click_Left",
            }];

        // Ctrl + click adds to a selection, Shift + click extends it.
        return KeyNames.ModifierPrefix(click.Modifiers) + name;
    }

    private static string WheelKey(MouseWheelStep wheel) => (wheel.IsHorizontal, wheel.Delta < 0) switch
    {
        (false, true) => "Step_WheelDown",
        (false, false) => "Step_WheelUp",
        (true, true) => "Step_WheelLeft",
        (true, false) => "Step_WheelRight",
    };

    private static int Notches(MouseWheelStep wheel) => Math.Max(1, (int)Math.Round(Math.Abs(wheel.Delta) / WheelNotch));

    private static string Repeated(string key, string keyName, int pressCount) =>
        pressCount > 1
            ? Localizer.Instance.Format(key + "Repeated", keyName, pressCount)
            : Localizer.Instance.Format(key, keyName);

    private static string WithWindow(string action, WindowTarget? target, ScreenPoint? screenPosition = null)
    {
        Localizer text = Localizer.Instance;
        if (target is not null)
        {
            return text.Format("Step_WithWindow", action, Shorten(target.Title.Length > 0 ? target.Title : target.ProcessName));
        }

        return screenPosition is ScreenPoint point
            ? text.Format("Step_WithWindow", action, text.Format("Window_ScreenPosition", point.X, point.Y))
            : action;
    }

    private static string Shorten(string value) =>
        value.Length <= MaxShownLength ? value : value[..(MaxShownLength - 1)] + "…";
}

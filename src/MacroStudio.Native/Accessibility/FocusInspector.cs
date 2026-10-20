using System.Runtime.InteropServices;
using System.Windows.Automation;
using MacroStudio.Core.Recording;

namespace MacroStudio.Native.Accessibility;

/// <summary>Asks Windows UI Automation about the focused control; it works across programs.</summary>
public sealed class FocusInspector : IFocusInspector
{
    public bool IsPasswordFieldFocused()
    {
        try
        {
            return AutomationElement.FocusedElement?.Current.IsPassword == true;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // The control went away or the program does not answer; it is not treated as a password field.
            return false;
        }
    }
}

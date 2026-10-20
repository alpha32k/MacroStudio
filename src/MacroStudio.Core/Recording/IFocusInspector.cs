namespace MacroStudio.Core.Recording;

public interface IFocusInspector
{
    /// <summary>True when the keyboard focus is in a password field; what is typed there is never recorded.</summary>
    bool IsPasswordFieldFocused();
}

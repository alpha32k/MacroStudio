using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;

namespace MacroStudio.Core.Playback;

/// <summary>
/// Sends mouse and keyboard input to Windows as if the user did it. Throws <see cref="InputBlockedException"/>
/// when Windows refuses the input (except in <see cref="ReleaseAll"/>, which is used while stopping).
/// </summary>
public interface IInputSimulator
{
    void MoveMouse(ScreenPoint position);

    void SetMouseButton(MouseButton button, bool isDown);

    /// <summary>120 is one notch; positive is up (or right).</summary>
    void TurnWheel(int delta, bool isHorizontal);

    void SetKey(int virtualKey, int scanCode, bool isExtended, bool isDown);

    /// <summary>Types one UTF-16 unit as text, whatever the keyboard layout.</summary>
    void TypeCharacter(char character);

    /// <summary>Releases every key and button pressed through this simulator and not released yet.</summary>
    void ReleaseAll();
}

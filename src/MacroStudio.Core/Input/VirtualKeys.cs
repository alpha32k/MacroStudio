namespace MacroStudio.Core.Input;

/// <summary>Windows virtual-key codes used by the macro logic.</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int CapsLock = 0x14;
    public const int Escape = 0x1B;
    public const int Up = 0x26;
    public const int Down = 0x28;
    public const int C = 0x43;
    public const int V = 0x56;
    public const int LeftWindows = 0x5B;
    public const int RightWindows = 0x5C;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftMenu = 0xA4;
    public const int RightMenu = 0xA5;

    /// <summary>A character typed through Unicode input (on-screen keyboards, other tools); not a real key.</summary>
    public const int Packet = 0xE7;

    public static bool IsModifier(int virtualKey) => virtualKey is
        Shift or Control or Menu or LeftShift or RightShift or LeftControl or RightControl
        or LeftMenu or RightMenu or LeftWindows or RightWindows;

    /// <summary>Caps Lock, Num Lock and Scroll Lock change a state; the typed text already reflects it.</summary>
    public static bool IsLock(int virtualKey) => virtualKey is CapsLock or NumLock or ScrollLock;

    public static bool IsAlt(int virtualKey) => virtualKey is Menu or LeftMenu or RightMenu;

    public static bool IsWindows(int virtualKey) => virtualKey is LeftWindows or RightWindows;
}

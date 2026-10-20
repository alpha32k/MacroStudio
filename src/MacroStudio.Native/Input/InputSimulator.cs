using System.Runtime.InteropServices;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Playback;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>
/// Sends input through SendInput, the way Windows delivers real keyboard and mouse events, so every
/// program receives it normally. Remembers what it holds down, so a stopped macro never leaves Ctrl pressed.
/// </summary>
public sealed unsafe class InputSimulator : IInputSimulator
{
    private readonly Dictionary<int, (ushort ScanCode, bool IsExtended)> _heldKeys = [];
    private readonly HashSet<MouseButton> _heldButtons = [];

    public void MoveMouse(ScreenPoint position)
    {
        // Absolute moves use 0..65535 across all monitors together.
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = Math.Max(2, GetSystemMetrics(SM_CXVIRTUALSCREEN));
        int height = Math.Max(2, GetSystemMetrics(SM_CYVIRTUALSCREEN));
        int x = (int)Math.Round((position.X - left) * 65535.0 / (width - 1));
        int y = (int)Math.Round((position.Y - top) * 65535.0 / (height - 1));
        SendMouse(x, y, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK);
    }

    public void SetMouseButton(MouseButton button, bool isDown)
    {
        (uint downFlag, uint upFlag, uint data) = button switch
        {
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, 0u),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP, 0u),
            MouseButton.X1 => (MOUSEEVENTF_XDOWN, MOUSEEVENTF_XUP, XBUTTON1),
            MouseButton.X2 => (MOUSEEVENTF_XDOWN, MOUSEEVENTF_XUP, XBUTTON2),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP, 0u),
        };

        SendMouse(0, 0, data, isDown ? downFlag : upFlag);
        if (isDown)
        {
            _heldButtons.Add(button);
        }
        else
        {
            _heldButtons.Remove(button);
        }
    }

    public void TurnWheel(int delta, bool isHorizontal) =>
        SendMouse(0, 0, unchecked((uint)delta), isHorizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL);

    public void SetKey(int virtualKey, int scanCode, bool isExtended, bool isDown)
    {
        // Some programs read the scan code instead of the key code, so it is always filled in.
        ushort scan = scanCode != 0 ? (ushort)scanCode : (ushort)MapVirtualKey((uint)virtualKey, MAPVK_VK_TO_VSC);
        uint flags = (isExtended ? KEYEVENTF_EXTENDEDKEY : 0) | (isDown ? 0 : KEYEVENTF_KEYUP);
        SendKeyboard((ushort)virtualKey, scan, flags);
        if (isDown)
        {
            _heldKeys[virtualKey] = (scan, isExtended);
        }
        else
        {
            _heldKeys.Remove(virtualKey);
        }
    }

    public void TypeCharacter(char character)
    {
        INPUT* inputs = stackalloc INPUT[2];
        inputs[0] = Keyboard(0, character, KEYEVENTF_UNICODE);
        inputs[1] = Keyboard(0, character, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP);
        Send(inputs, 2);
    }

    public void ReleaseAll()
    {
        try
        {
            foreach ((int virtualKey, (ushort scan, bool isExtended)) in _heldKeys.ToList())
            {
                SetKey(virtualKey, scan, isExtended, false);
            }

            foreach (MouseButton button in _heldButtons.ToList())
            {
                SetMouseButton(button, false);
            }
        }
        catch (InputBlockedException)
        {
            // The screen got locked; Windows resets the keyboard state when it switches desktops.
            _heldKeys.Clear();
            _heldButtons.Clear();
        }
    }

    private static INPUT Keyboard(ushort virtualKey, ushort scanCode, uint flags) => new()
    {
        Type = INPUT_KEYBOARD,
        Data = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, ScanCode = scanCode, Flags = flags } },
    };

    // SendInput reports how many events it delivered; fewer means Windows refused them (locked screen, UAC prompt).
    private static void Send(INPUT* inputs, uint count)
    {
        if (SendInput(count, inputs, sizeof(INPUT)) != count)
        {
            throw new InputBlockedException(Marshal.GetLastPInvokeError());
        }
    }

    private static void SendKeyboard(ushort virtualKey, ushort scanCode, uint flags)
    {
        INPUT input = Keyboard(virtualKey, scanCode, flags);
        Send(&input, 1);
    }

    private static void SendMouse(int x, int y, uint data, uint flags)
    {
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Data = new INPUTUNION { Mouse = new MOUSEINPUT { X = x, Y = y, MouseData = data, Flags = flags } },
        };
        Send(&input, 1);
    }
}

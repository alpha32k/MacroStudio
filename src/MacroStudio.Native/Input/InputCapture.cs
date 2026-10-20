using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Recording;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>
/// System-wide low-level keyboard and mouse hooks plus a foreground-window hook, used only while recording.
/// Each callback only notes the event and returns: Windows removes a hook without warning when it answers too slowly.
/// </summary>
public sealed unsafe class InputCapture : IInputCapture, IDisposable
{
    // Windows calls plain functions; they reach the running capture through this field.
    private static InputCapture? s_active;

    private readonly HookThread _thread = new("MacroStudio.InputCapture");
    private readonly ModifierTracker _modifiers = new();
    private ChannelWriter<CapturedInput>? _output;
    private uint _startTick;
    private int _heldButtons;

    public void Start(ChannelWriter<CapturedInput> output)
    {
        if (s_active is not null)
        {
            throw new InvalidOperationException("Input capture is already running.");
        }

        _output = output;
        _startTick = (uint)Environment.TickCount;
        _modifiers.ReadFromKeyboard();
        _heldButtons = 0;
        s_active = this;

        if (!_thread.Start(InstallHooks))
        {
            s_active = null;
            _output = null;
            throw new Win32Exception("The keyboard and mouse hooks could not be installed.");
        }
    }

    public void Stop()
    {
        _thread.Stop();
        _output = null;
        s_active = null;
    }

    public void Dispose() => Stop();

    private static Action? InstallHooks()
    {
        nint module = GetModuleHandle(null);
        nint keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, &OnKeyboard, module, 0);
        nint mouseHook = SetWindowsHookEx(WH_MOUSE_LL, &OnMouse, module, 0);
        nint foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, &OnForeground, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        void Uninstall()
        {
            if (keyboardHook != 0)
            {
                UnhookWindowsHookEx(keyboardHook);
            }

            if (mouseHook != 0)
            {
                UnhookWindowsHookEx(mouseHook);
            }

            if (foregroundHook != 0)
            {
                UnhookWinEvent(foregroundHook);
            }
        }

        if (keyboardHook == 0 || mouseHook == 0 || foregroundHook == 0)
        {
            Uninstall();
            return null;
        }

        return Uninstall;
    }

    private static CapturedWindow Snapshot(nint hwnd)
    {
        if (hwnd == 0 || !GetWindowRect(hwnd, out RECT rect))
        {
            return new CapturedWindow(hwnd, default);
        }

        return new CapturedWindow(hwnd, new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
    }

    [UnmanagedCallersOnly]
    private static nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            s_active?.HandleKey((uint)message, (KBDLLHOOKSTRUCT*)data);
        }

        return CallNextHookEx(0, code, message, data);
    }

    [UnmanagedCallersOnly]
    private static nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0)
        {
            s_active?.HandleMouse((uint)message, (MSLLHOOKSTRUCT*)data);
        }

        return CallNextHookEx(0, code, message, data);
    }

    [UnmanagedCallersOnly]
    private static void OnForeground(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint threadId, uint timeMs)
    {
        if (objectId == OBJID_WINDOW && hwnd != 0)
        {
            s_active?.HandleForeground(hwnd, timeMs);
        }
    }

    private void HandleKey(uint message, KBDLLHOOKSTRUCT* key)
    {
        bool isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
        int virtualKey = (int)key->VirtualKey;

        // A modifier key does not count itself, so "Alt alone" and "Ctrl+C" can be told apart.
        KeyModifiers modifiers = _modifiers.Without(virtualKey);
        _modifiers.Update(virtualKey, isDown);

        nint foreground = GetForegroundWindow();
        string? text = isDown && !VirtualKeys.IsModifier(virtualKey) ? TranslateToText(virtualKey, key->ScanCode, foreground) : null;

        Write(new CapturedKey(
            ElapsedMs(key->Time),
            virtualKey,
            (int)key->ScanCode,
            (key->Flags & LLKHF_EXTENDED) != 0,
            isDown,
            text,
            modifiers,
            Snapshot(foreground)));
    }

    /// <summary>The character the key types in the focused window's keyboard layout, with Shift, AltGr and Caps Lock.</summary>
    private string? TranslateToText(int virtualKey, uint scanCode, nint foreground)
    {
        if (virtualKey == VirtualKeys.Packet)
        {
            // Unicode input (on-screen keyboards, other tools) carries the character in the scan code field.
            return ((char)scanCode).ToString();
        }

        byte* keyState = stackalloc byte[256];
        new Span<byte>(keyState, 256).Clear();
        KeyModifiers held = _modifiers.Current;
        if (held.HasFlag(KeyModifiers.Shift))
        {
            keyState[VirtualKeys.Shift] = 0x80;
        }

        if (held.HasFlag(KeyModifiers.Control))
        {
            keyState[VirtualKeys.Control] = 0x80;
        }

        if (held.HasFlag(KeyModifiers.Alt))
        {
            keyState[VirtualKeys.Menu] = 0x80;
        }

        keyState[VirtualKeys.CapsLock] = (byte)(GetKeyState(VirtualKeys.CapsLock) & 1);
        keyState[VirtualKeys.NumLock] = (byte)(GetKeyState(VirtualKeys.NumLock) & 1);

        nint layout = GetKeyboardLayout(GetWindowThreadProcessId(foreground, out _));
        char* buffer = stackalloc char[8];
        int count = ToUnicodeEx((uint)virtualKey, scanCode, keyState, buffer, 8, TOUNICODE_KEEP_STATE, layout);
        return count > 0 ? new string(buffer, 0, count) : null;
    }

    private void HandleMouse(uint message, MSLLHOOKSTRUCT* mouse)
    {
        MouseButton sideButton = (mouse->MouseData >> 16) == XBUTTON1 ? MouseButton.X1 : MouseButton.X2;
        (MouseAction action, MouseButton button)? kind = message switch
        {
            WM_MOUSEMOVE => (MouseAction.Move, MouseButton.Left),
            WM_LBUTTONDOWN => (MouseAction.Down, MouseButton.Left),
            WM_LBUTTONUP => (MouseAction.Up, MouseButton.Left),
            WM_RBUTTONDOWN => (MouseAction.Down, MouseButton.Right),
            WM_RBUTTONUP => (MouseAction.Up, MouseButton.Right),
            WM_MBUTTONDOWN => (MouseAction.Down, MouseButton.Middle),
            WM_MBUTTONUP => (MouseAction.Up, MouseButton.Middle),
            WM_XBUTTONDOWN => (MouseAction.Down, sideButton),
            WM_XBUTTONUP => (MouseAction.Up, sideButton),
            WM_MOUSEWHEEL => (MouseAction.Wheel, MouseButton.Left),
            WM_MOUSEHWHEEL => (MouseAction.HorizontalWheel, MouseButton.Left),
            _ => null,
        };

        // Moves only matter as the path of a drag; a thousand moves a second would flood the queue.
        if (kind is not var (action, button) || (action == MouseAction.Move && _heldButtons == 0))
        {
            return;
        }

        int buttonBit = 1 << (int)button;
        _heldButtons = action switch
        {
            MouseAction.Down => _heldButtons | buttonBit,
            MouseAction.Up => _heldButtons & ~buttonBit,
            _ => _heldButtons,
        };

        int wheelDelta = action is MouseAction.Wheel or MouseAction.HorizontalWheel ? (short)(mouse->MouseData >> 16) : 0;
        nint window = action == MouseAction.Move ? 0 : GetAncestor(WindowFromPoint(mouse->Point), GA_ROOT);

        Write(new CapturedMouse(
            ElapsedMs(mouse->Time),
            action,
            button,
            new ScreenPoint(mouse->Point.X, mouse->Point.Y),
            wheelDelta,
            _modifiers.Current,
            Snapshot(window)));
    }

    private void HandleForeground(nint hwnd, uint timeMs) =>
        Write(new CapturedForeground(ElapsedMs(timeMs), Snapshot(GetAncestor(hwnd, GA_ROOT))));

    /// <summary>Windows stamps events with its millisecond tick count; the difference survives the 49-day wrap-around.</summary>
    private long ElapsedMs(uint eventTick) => Math.Max(0, unchecked((int)(eventTick - _startTick)));

    private void Write(CapturedInput input) => _output?.TryWrite(input);
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using MacroStudio.Core.Input;
using MacroStudio.Core.Playback;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>
/// Low-level hooks used only while a macro plays. Input the macro sends is marked "injected" by Windows and
/// ignored; anything else is the user. Esc stops the macro and is kept from the program, so it cannot also
/// close a dialog there.
/// </summary>
public sealed unsafe class UserActivityMonitor : IUserActivityMonitor, IDisposable
{
    // Windows calls plain functions; they reach the running monitor through this field.
    private static UserActivityMonitor? s_active;

    private readonly HookThread _thread = new("MacroStudio.UserActivity");
    private readonly ModifierTracker _modifiers = new();
    private IReadOnlySet<KeyShortcut> _ignoredShortcuts = new HashSet<KeyShortcut>();

    public event EventHandler? UserInput;

    public event EventHandler? StopRequested;

    public void Start(IReadOnlySet<KeyShortcut> ignoredShortcuts)
    {
        if (s_active is not null)
        {
            throw new InvalidOperationException("The activity monitor is already running.");
        }

        _ignoredShortcuts = ignoredShortcuts;
        _modifiers.ReadFromKeyboard();
        s_active = this;
        if (!_thread.Start(InstallHooks))
        {
            s_active = null;
            throw new Win32Exception("The keyboard and mouse hooks could not be installed.");
        }
    }

    public void Stop()
    {
        _thread.Stop();
        s_active = null;
    }

    public void Dispose() => Stop();

    private static Action? InstallHooks()
    {
        nint module = GetModuleHandle(null);
        nint keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, &OnKeyboard, module, 0);
        nint mouseHook = SetWindowsHookEx(WH_MOUSE_LL, &OnMouse, module, 0);

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
        }

        if (keyboardHook == 0 || mouseHook == 0)
        {
            Uninstall();
            return null;
        }

        return Uninstall;
    }

    [UnmanagedCallersOnly]
    private static nint OnKeyboard(int code, nint message, nint data)
    {
        if (code >= 0 && s_active is { } monitor && monitor.HandleKey((uint)message, (KBDLLHOOKSTRUCT*)data))
        {
            // Swallowed: the program never sees this key.
            return 1;
        }

        return CallNextHookEx(0, code, message, data);
    }

    [UnmanagedCallersOnly]
    private static nint OnMouse(int code, nint message, nint data)
    {
        bool isFromTheUser = (((MSLLHOOKSTRUCT*)data)->Flags & LLMHF_INJECTED) == 0;
        if (code >= 0 && isFromTheUser && s_active is { } monitor)
        {
            monitor.UserInput?.Invoke(monitor, EventArgs.Empty);
        }

        return CallNextHookEx(0, code, message, data);
    }

    /// <returns>True when the key must not reach the program.</returns>
    private bool HandleKey(uint message, KBDLLHOOKSTRUCT* key)
    {
        if ((key->Flags & LLKHF_INJECTED) != 0)
        {
            return false;
        }

        int virtualKey = (int)key->VirtualKey;
        bool isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
        if (virtualKey == VirtualKeys.Escape)
        {
            if (isDown)
            {
                StopRequested?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        KeyModifiers modifiers = _modifiers.Without(virtualKey);
        _modifiers.Update(virtualKey, isDown);

        // Modifiers alone and Macro Studio's own shortcuts (Ctrl+Alt+P to pause, for example) are not interference.
        bool isUserInput = isDown
            && !VirtualKeys.IsModifier(virtualKey)
            && !_ignoredShortcuts.Contains(new KeyShortcut(modifiers, virtualKey));
        if (isUserInput)
        {
            UserInput?.Invoke(this, EventArgs.Empty);
        }

        return false;
    }
}

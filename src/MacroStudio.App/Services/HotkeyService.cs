using System.Windows.Input;
using System.Windows.Interop;
using MacroStudio.Native.Input;

namespace MacroStudio.App.Services;

/// <summary>System-wide shortcuts. They work while any program is in front.</summary>
public sealed class HotkeyService : IDisposable
{
    // A message-only window receives WM_HOTKEY; it is never visible.
    private static readonly nint MessageOnlyParent = -3;

    private readonly HwndSource _messageWindow;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 1;

    public HotkeyService()
    {
        _messageWindow = new HwndSource(new HwndSourceParameters("MacroStudio.Hotkeys") { ParentWindow = MessageOnlyParent });
        _messageWindow.AddHook(OnMessage);
    }

    /// <returns>An id for <see cref="Unregister"/>; null when another program already uses the shortcut.</returns>
    public int? TryRegister(AppShortcut shortcut, Action action)
    {
        int id = _nextId++;
        HotkeyModifiers nativeModifiers = ToNative(shortcut.Modifiers) | HotkeyModifiers.NoRepeat;
        if (!HotkeyInterop.Register(_messageWindow.Handle, id, nativeModifiers, KeyInterop.VirtualKeyFromKey(shortcut.Key)))
        {
            return null;
        }

        _actions[id] = action;
        return id;
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id))
        {
            HotkeyInterop.Unregister(_messageWindow.Handle, id);
        }
    }

    public void Dispose()
    {
        foreach (int id in _actions.Keys)
        {
            HotkeyInterop.Unregister(_messageWindow.Handle, id);
        }

        _messageWindow.Dispose();
    }

    private static HotkeyModifiers ToNative(ModifierKeys modifiers)
    {
        HotkeyModifiers result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Windows;
        }

        return result;
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == HotkeyInterop.WM_HOTKEY && _actions.TryGetValue((int)wParam, out Action? action))
        {
            action();
            handled = true;
        }

        return 0;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Services;
using MacroStudio.Core.Input;

namespace MacroStudio.App.Controls;

/// <summary>
/// A box that shows a shortcut and takes a new one: click it, press the keys. Esc leaves it as it was;
/// Backspace or Delete removes it where removing is allowed.
/// </summary>
public sealed class ShortcutBox : TextBox
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut),
        typeof(KeyShortcut?),
        typeof(ShortcutBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (box, _) => ((ShortcutBox)box).ShowShortcut()));

    public static readonly DependencyProperty CanClearProperty =
        DependencyProperty.Register(nameof(CanClear), typeof(bool), typeof(ShortcutBox), new PropertyMetadata(false));

    public ShortcutBox()
    {
        // WPF finds implicit styles by exact type, so a TextBox subclass would get Windows' plain white box.
        // The theme's TextBox style follows light and dark like every other field.
        SetResourceReference(StyleProperty, typeof(TextBox));
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        MinWidth = 220;
        Cursor = Cursors.Hand;
        Localizer.Instance.PropertyChanged += (_, _) => ShowShortcut();
        ShowShortcut();
    }

    /// <summary>
    /// The box takes keys (true) or let them go (false). While it takes keys, Macro Studio's own system-wide
    /// shortcuts must be switched off, or pressing Ctrl+Alt+R here would start a recording instead.
    /// </summary>
    public static event EventHandler<bool>? CapturingChanged;

    public KeyShortcut? Shortcut
    {
        get => (KeyShortcut?)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    public bool CanClear
    {
        get => (bool)GetValue(CanClearProperty);
        set => SetValue(CanClearProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        CapturingChanged?.Invoke(this, true);
        Text = Localizer.Instance["Shortcut_PressKeys"];
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        CapturingChanged?.Invoke(this, false);
        ShowShortcut();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys held = Keyboard.Modifiers;

        if (key == Key.Tab && held == ModifierKeys.None)
        {
            // Tab still moves on, so the box can be left with the keyboard.
            e.Handled = false;
            return;
        }

        if (key == Key.Escape && held == ModifierKeys.None)
        {
            MoveFocusAway();
            return;
        }

        if (key is Key.Back or Key.Delete && held == ModifierKeys.None && CanClear)
        {
            Shortcut = null;
            MoveFocusAway();
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var shortcut = new KeyShortcut(ToModifiers(held), virtualKey);
        if (VirtualKeys.IsModifier(virtualKey))
        {
            // Only Ctrl or Alt so far; show them while the hand looks for the last key.
            Text = Macros.KeyNames.ModifierPrefix(shortcut.Modifiers) + "…";
            return;
        }

        if (!shortcut.IsUsable)
        {
            Text = Localizer.Instance["Shortcut_Invalid"];
            return;
        }

        Shortcut = shortcut;
        MoveFocusAway();
    }

    private static KeyModifiers ToModifiers(ModifierKeys held)
    {
        KeyModifiers result = KeyModifiers.None;
        if (held.HasFlag(ModifierKeys.Control))
        {
            result |= KeyModifiers.Control;
        }

        if (held.HasFlag(ModifierKeys.Alt))
        {
            result |= KeyModifiers.Alt;
        }

        if (held.HasFlag(ModifierKeys.Shift))
        {
            result |= KeyModifiers.Shift;
        }

        if (held.HasFlag(ModifierKeys.Windows))
        {
            result |= KeyModifiers.Windows;
        }

        return result;
    }

    private void MoveFocusAway()
    {
        Keyboard.ClearFocus();
        ShowShortcut();
    }

    private void ShowShortcut()
    {
        if (IsKeyboardFocused)
        {
            return;
        }

        Text = Shortcut is { } shortcut ? ShortcutService.NameOf(shortcut) : Localizer.Instance["Shortcut_None"];
    }
}

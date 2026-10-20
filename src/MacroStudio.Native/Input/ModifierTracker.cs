using MacroStudio.Core.Input;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>Follows which Shift, Ctrl, Alt and Windows keys are held, left and right separately, from hook events.</summary>
internal sealed class ModifierTracker
{
    private static readonly int[] TrackedKeys =
    [
        VirtualKeys.LeftShift, VirtualKeys.RightShift, VirtualKeys.LeftControl, VirtualKeys.RightControl,
        VirtualKeys.LeftMenu, VirtualKeys.RightMenu, VirtualKeys.LeftWindows, VirtualKeys.RightWindows,
    ];

    private readonly HashSet<int> _held = [];

    public KeyModifiers Current => ToModifiers(_held);

    /// <summary>A key already held when tracking starts (Shift, for example) must count from the first event.</summary>
    public void ReadFromKeyboard()
    {
        _held.Clear();
        foreach (int virtualKey in TrackedKeys)
        {
            if (GetAsyncKeyState(virtualKey) < 0)
            {
                _held.Add(virtualKey);
            }
        }
    }

    /// <summary>The held modifiers, leaving out the key itself: Alt pressed alone reports no modifier.</summary>
    public KeyModifiers Without(int virtualKey)
    {
        int key = Normalize(virtualKey);
        return ToModifiers(_held.Where(held => held != key));
    }

    public void Update(int virtualKey, bool isDown)
    {
        if (!VirtualKeys.IsModifier(virtualKey))
        {
            return;
        }

        int key = Normalize(virtualKey);
        if (isDown)
        {
            _held.Add(key);
        }
        else
        {
            _held.Remove(key);
        }
    }

    // Programs that send input may use the generic codes; they count as the left key.
    private static int Normalize(int virtualKey) => virtualKey switch
    {
        VirtualKeys.Shift => VirtualKeys.LeftShift,
        VirtualKeys.Control => VirtualKeys.LeftControl,
        VirtualKeys.Menu => VirtualKeys.LeftMenu,
        _ => virtualKey,
    };

    private static KeyModifiers ToModifiers(IEnumerable<int> held)
    {
        KeyModifiers result = KeyModifiers.None;
        foreach (int key in held)
        {
            result |= key switch
            {
                VirtualKeys.LeftShift or VirtualKeys.RightShift => KeyModifiers.Shift,
                VirtualKeys.LeftControl or VirtualKeys.RightControl => KeyModifiers.Control,
                VirtualKeys.LeftMenu or VirtualKeys.RightMenu => KeyModifiers.Alt,
                _ => KeyModifiers.Windows,
            };
        }

        return result;
    }
}

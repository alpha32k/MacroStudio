using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Tests.Playback;

/// <summary>
/// A pretend Windows for player tests: Ctrl+C puts the next value of a list on the clipboard, Ctrl+V notes
/// what was pasted, and time only moves when the player waits.
/// </summary>
internal sealed class FakeDesktop : IInputSimulator, IWindowSystem, IClipboard, IPlaybackClock, IUiElements
{
    /// <summary>Controls UI Automation would find, by name, with where they are now.</summary>
    public Dictionary<string, ScreenRect> Controls { get; } = [];

    public int ControlSearches { get; private set; }

    public UiElement? Describe(ScreenPoint point, ScreenRect windowBounds) => null;

    public ScreenRect? Find(nint window, UiElement element, ScreenPoint near, TimeSpan timeout)
    {
        ControlSearches++;
        return Controls.TryGetValue(element.Name, out ScreenRect bounds) ? bounds : null;
    }

    private readonly Queue<string> _cellsToCopy;
    private readonly HashSet<int> _heldKeys = [];
    private string _clipboard = "";

    public FakeDesktop(params string[] cellsToCopy)
    {
        _cellsToCopy = new Queue<string>(cellsToCopy);
    }

    public List<WindowCandidate> Windows { get; } = [];

    public List<string> Pasted { get; } = [];

    public List<ScreenPoint> Clicks { get; } = [];

    public List<string> Typed { get; } = [];

    public List<nint> Activations { get; } = [];

    public HashSet<nint> ElevatedWindows { get; } = [];

    public List<nint> Moves { get; } = [];

    /// <summary>After this many key presses Windows "locks the screen": every further input is refused.</summary>
    public int? KeysUntilInputBlocked { get; set; }

    public bool CopyChangesClipboard { get; set; } = true;

    /// <summary>False acts like Notepad: past the end of the list nothing is selected, and Ctrl+C copies nothing.</summary>
    public bool CopyPastTheListChangesClipboard { get; set; } = true;

    public int ReleaseAllCalls { get; private set; }

    public IReadOnlyCollection<int> HeldKeys => _heldKeys;

    public long NowMs { get; private set; }

    public nint ForegroundWindow { get; private set; }

    public uint ChangeCount { get; private set; }

    public ScreenPoint MousePosition { get; private set; }

    /// <summary>Called on every wait; lets a test act "while" the player waits, for example to stop it.</summary>
    public Action<long>? OnWait { get; set; }

    public void Wait(TimeSpan duration, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        NowMs += (long)duration.TotalMilliseconds;
        OnWait?.Invoke(NowMs);
        cancellation.ThrowIfCancellationRequested();
    }

    public IReadOnlyList<WindowCandidate> ListWindows() => Windows;

    public bool TryActivate(nint window)
    {
        Activations.Add(window);
        ForegroundWindow = window;
        return true;
    }

    public ScreenRect? GetBounds(nint window) => Windows.FirstOrDefault(w => w.Handle == window)?.Bounds;

    public bool TryMove(nint window, ScreenRect bounds)
    {
        int index = Windows.FindIndex(w => w.Handle == window);
        Windows[index] = Windows[index] with { Bounds = bounds };
        Moves.Add(window);
        return true;
    }

    public bool IsElevatedAboveUs(nint window) => ElevatedWindows.Contains(window);

    /// <summary>The screen's colors by spot; spots not listed are white.</summary>
    public Dictionary<ScreenPoint, int> Pixels { get; } = [];

    public int? PixelAt(ScreenPoint point) => Pixels.TryGetValue(point, out int color) ? color : 0xFFFFFF;

    /// <summary>The whole fake screen as a picture, for image search; null cannot be read.</summary>
    public Bgra32Image? Screen { get; set; }

    public ScreenRect DesktopBounds => Screen is null ? default : new ScreenRect(0, 0, Screen.Width, Screen.Height);

    public Bgra32Image? Capture(ScreenRect area)
    {
        if (Screen is null)
        {
            return null;
        }

        var pixels = new byte[area.Width * area.Height * 4];
        for (int y = 0; y < area.Height; y++)
        {
            Array.Copy(Screen.Pixels, (((area.Top + y) * Screen.Width) + area.Left) * 4, pixels, y * area.Width * 4, area.Width * 4);
        }

        return new Bgra32Image(area.Width, area.Height, pixels);
    }

    public List<string> Opened { get; } = [];

    public bool CanOpen { get; set; } = true;

    public bool TryOpen(string path, string arguments)
    {
        Opened.Add(path);
        return CanOpen;
    }

    public string? ReadText() => _clipboard;

    public bool TryWriteText(string text)
    {
        _clipboard = text;
        ChangeCount++;
        return true;
    }

    public void MoveMouse(ScreenPoint position) => MousePosition = position;

    public void SetMouseButton(MouseButton button, bool isDown)
    {
        if (isDown)
        {
            Clicks.Add(MousePosition);
        }
    }

    public void TurnWheel(int delta, bool isHorizontal)
    {
    }

    public void SetKey(int virtualKey, int scanCode, bool isExtended, bool isDown)
    {
        if (isDown && KeysUntilInputBlocked is not null && KeysUntilInputBlocked-- == 0)
        {
            throw new InputBlockedException(5);
        }

        if (!isDown)
        {
            _heldKeys.Remove(virtualKey);
            return;
        }

        _heldKeys.Add(virtualKey);
        if (!_heldKeys.Contains(VirtualKeys.LeftControl))
        {
            return;
        }

        if (virtualKey == VirtualKeys.C && CopyChangesClipboard && (_cellsToCopy.Count > 0 || CopyPastTheListChangesClipboard))
        {
            _clipboard = _cellsToCopy.Count > 0 ? _cellsToCopy.Dequeue() : "\r\n";
            ChangeCount++;
        }
        else if (virtualKey == VirtualKeys.V)
        {
            Pasted.Add(_clipboard);
        }
    }

    public void TypeCharacter(char character) => Typed.Add(character.ToString());

    public void ReleaseAll()
    {
        ReleaseAllCalls++;
        _heldKeys.Clear();
    }
}

using System.Text;
using MacroStudio.Core.Macros;

namespace MacroStudio.Engine.Recording;

/// <summary>Characters being typed into one window; becomes a text step when the typing ends.</summary>
internal sealed class TextRun(nint window, WindowTarget? target, long startMs, string firstText)
{
    private readonly StringBuilder _text = new(firstText);

    public nint Window { get; } = window;

    public WindowTarget? Target { get; } = target;

    public long StartMs { get; } = startMs;

    public long LastKeyMs { get; set; } = startMs;

    public string Text => _text.ToString();

    public void Append(string text, long timeMs)
    {
        _text.Append(text);
        LastKeyMs = timeMs;
    }

    /// <summary>Backspace while typing removes the last character instead of being recorded as a key.</summary>
    public bool TryRemoveLast()
    {
        if (_text.Length == 0)
        {
            return false;
        }

        // Characters outside the basic range (some emoji) are two UTF-16 units.
        int length = _text.Length >= 2 && char.IsSurrogatePair(_text[^2], _text[^1]) ? 2 : 1;
        _text.Remove(_text.Length - length, length);
        return true;
    }
}

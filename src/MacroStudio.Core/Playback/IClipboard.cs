namespace MacroStudio.Core.Playback;

public interface IClipboard
{
    /// <summary>A number Windows raises every time the clipboard changes.</summary>
    uint ChangeCount { get; }

    /// <summary>The clipboard text; "" when the clipboard holds no text; null while another program keeps it locked.</summary>
    string? ReadText();

    bool TryWriteText(string text);
}

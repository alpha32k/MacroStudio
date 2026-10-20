namespace MacroStudio.Core.Playback;

/// <summary>What playback does when the user moves the real mouse or presses a key while a macro runs.</summary>
public enum InterferencePolicy
{
    Pause,
    Stop,
    Ignore,
}

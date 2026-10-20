namespace MacroStudio.Core.Playback;

/// <summary>
/// Windows did not accept the keys or clicks, for example because the screen got locked. Thrown by
/// <see cref="IInputSimulator"/>; going on would make the macro look like it works while nothing reaches the programs.
/// </summary>
public sealed class InputBlockedException(int errorCode)
    : Exception($"Windows refused the input (error {errorCode}).")
{
    public int ErrorCode { get; } = errorCode;
}

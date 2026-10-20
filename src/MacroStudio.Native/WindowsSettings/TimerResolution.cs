using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.WindowsSettings;

/// <summary>
/// Windows wakes sleeping threads every 15.6 ms by default; recorded waits like 230 ms need 1 ms steps.
/// Asked for only while a macro plays, because it costs a little battery.
/// </summary>
public sealed class TimerResolution : IDisposable
{
    private const uint OneMillisecond = 1;
    private bool _isActive;

    private TimerResolution()
    {
        _isActive = timeBeginPeriod(OneMillisecond) == 0;
    }

    public static TimerResolution OneMs() => new();

    public void Dispose()
    {
        if (_isActive)
        {
            timeEndPeriod(OneMillisecond);
            _isActive = false;
        }
    }
}

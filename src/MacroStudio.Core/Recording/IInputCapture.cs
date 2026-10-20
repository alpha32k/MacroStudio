using System.Threading.Channels;

namespace MacroStudio.Core.Recording;

/// <summary>System-wide mouse, keyboard and foreground-window hooks. They exist only between Start and Stop.</summary>
public interface IInputCapture
{
    /// <summary>Installs the hooks; every event is written to <paramref name="output"/> until Stop.</summary>
    void Start(ChannelWriter<CapturedInput> output);

    /// <summary>Removes the hooks and returns when no more events will be written.</summary>
    void Stop();
}

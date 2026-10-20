using System.Threading.Channels;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;

namespace MacroStudio.Engine.Recording;

/// <summary>
/// Runs one recording. The hooks only put events into a queue and return at once (Windows silently removes a
/// slow hook); this class reads the queue on a background thread and builds the steps.
/// </summary>
public sealed class MacroRecorder(IInputCapture capture, IWindowInspector windows, IFocusInspector focus)
{
    private Channel<CapturedInput>? _events;
    private Task<IReadOnlyList<MacroStep>>? _builder;

    public bool IsRecording => _builder is not null;

    public void Start(RecordingOptions options)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("A recording is already running.");
        }

        _events = Channel.CreateUnbounded<CapturedInput>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        ChannelReader<CapturedInput> reader = _events.Reader;
        _builder = Task.Run(() => BuildStepsAsync(reader, new StepBuilder(options)));
        capture.Start(_events.Writer);
    }

    /// <returns>The recorded steps, in order.</returns>
    public async Task<IReadOnlyList<MacroStep>> StopAsync()
    {
        if (_events is null || _builder is null)
        {
            throw new InvalidOperationException("No recording is running.");
        }

        try
        {
            capture.Stop();
            _events.Writer.TryComplete();
            return await _builder.ConfigureAwait(false);
        }
        finally
        {
            _events = null;
            _builder = null;
        }
    }

    private async Task<IReadOnlyList<MacroStep>> BuildStepsAsync(ChannelReader<CapturedInput> reader, StepBuilder builder)
    {
        await foreach (CapturedInput input in reader.ReadAllAsync().ConfigureAwait(false))
        {
            switch (input)
            {
                case CapturedMouse { Action: MouseAction.Move } move:
                    builder.AddMouseMove(move);
                    break;

                case CapturedMouse mouse:
                    builder.AddMouse(mouse, windows.Describe(mouse.Window));
                    break;

                case CapturedKey key:
                    WindowInfo window = windows.Describe(key.Window);
                    bool isPasswordField = key is { IsDown: true, Text: not null }
                        && window.Role == WindowRole.Application
                        && focus.IsPasswordFieldFocused();
                    builder.AddKey(key, window, isPasswordField);
                    break;

                case CapturedForeground foreground:
                    builder.AddForeground(foreground, windows.Describe(foreground.Window));
                    break;
            }
        }

        return builder.Finish();
    }
}

using System.Threading.Channels;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;
using MacroStudio.Core.Recording;

namespace MacroStudio.Engine.Recording;

/// <summary>
/// Runs one recording. The hooks only put events into a queue and return at once (Windows silently removes a
/// slow hook); this class reads the queue on a background thread and builds the steps.
/// </summary>
public sealed class MacroRecorder(IInputCapture capture, IWindowInspector windows, IFocusInspector focus, IUiElements? elements = null)
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

        // The hooks first: when Windows refuses them, nothing is left half started and the next try works.
        // Events that come before the builder runs wait in the queue.
        Channel<CapturedInput> events = Channel.CreateUnbounded<CapturedInput>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        capture.Start(events.Writer);
        _events = events;
        _builder = Task.Run(() => BuildStepsAsync(events.Reader, new StepBuilder(options), options.FindElements));
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

    private async Task<IReadOnlyList<MacroStep>> BuildStepsAsync(ChannelReader<CapturedInput> reader, StepBuilder builder, bool findElements)
    {
        await foreach (CapturedInput input in reader.ReadAllAsync().ConfigureAwait(false))
        {
            switch (input)
            {
                case CapturedMouse { Action: MouseAction.Move } move:
                    builder.AddMouseMove(move);
                    break;

                case CapturedMouse mouse:
                    WindowInfo clicked = windows.Describe(mouse.Window);

                    // Asked at the press, before the program reacts to it and perhaps opens something over the control.
                    UiElement? element = findElements && mouse.Action == MouseAction.Down && clicked.Role == WindowRole.Application
                        ? elements?.Describe(mouse.Position, clicked.Target.Bounds)
                        : null;
                    builder.AddMouse(mouse, clicked, element);
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

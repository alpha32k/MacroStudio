using System.ComponentModel;
using System.Threading.Channels;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Recording;
using MacroStudio.Engine.Recording;

namespace MacroStudio.Engine.Tests.Recording;

public sealed class MacroRecorderTests
{
    [Fact]
    public async Task HooksRefused_LeaveNoRecordingBehind_AndTheNextTryWorks()
    {
        var capture = new FakeCapture { RefuseNextStart = true };
        var recorder = new MacroRecorder(capture, new FakeWindows(), new FakeFocus());

        Assert.Throws<Win32Exception>(() => recorder.Start(new RecordingOptions()));
        Assert.False(recorder.IsRecording);

        recorder.Start(new RecordingOptions());
        Assert.True(recorder.IsRecording);
        Assert.Empty(await recorder.StopAsync());
    }

    private sealed class FakeCapture : IInputCapture
    {
        public bool RefuseNextStart { get; set; }

        public void Start(ChannelWriter<CapturedInput> output)
        {
            if (RefuseNextStart)
            {
                RefuseNextStart = false;
                throw new Win32Exception("The keyboard and mouse hooks could not be installed.");
            }
        }

        public void Stop()
        {
        }
    }

    private sealed class FakeWindows : IWindowInspector
    {
        public WindowInfo Describe(CapturedWindow window) => new(WindowRole.Desktop, new WindowTarget("", "", "", window.Bounds));
    }

    private sealed class FakeFocus : IFocusInspector
    {
        public bool IsPasswordFieldFocused() => false;
    }
}

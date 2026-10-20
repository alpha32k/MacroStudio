using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using MacroStudio.App.Localization;
using MacroStudio.Native.Windowing;

namespace MacroStudio.App.Recording;

public partial class CountdownWindow : Window
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private CountdownWindow()
    {
        InitializeComponent();
    }

    /// <summary>Counts down in the middle of the screen. Cancelling closes it and throws OperationCanceledException.</summary>
    /// <param name="titleKey">Text resource for the title, for example "Recording starts".</param>
    /// <param name="hintKey">Text resource for the line below it.</param>
    public static async Task RunAsync(int seconds, string titleKey, string hintKey, CancellationToken cancellation)
    {
        if (seconds <= 0)
        {
            return;
        }

        var window = new CountdownWindow();
        window.TitleText.Text = Localizer.Instance[titleKey];
        window.HintText.Text = Localizer.Instance[hintKey];
        window.Show();
        try
        {
            for (int left = seconds; left > 0; left--)
            {
                window.SecondsText.Text = left.ToString(CultureInfo.CurrentCulture);
                await Task.Delay(OneSecond, cancellation);
            }
        }
        finally
        {
            window.Close();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint hwnd = new WindowInteropHelper(this).Handle;
        WindowInterop.MakeNonActivatingToolWindow(hwnd);
        WindowInterop.SetClickThrough(hwnd, true);
    }
}

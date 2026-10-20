using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using MacroStudio.Native.Windowing;

namespace MacroStudio.App.Services;

/// <summary>
/// Makes sure only one Macro Studio runs per Windows session. Starting it again hands the request to the running
/// copy through a pipe that only the same user can open: show the main window, or open a double-clicked .msmacro file.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MacroStudio.SingleInstance";
    private const int ConnectTimeoutMs = 3000;

    private static readonly string PipeName = $"MacroStudio.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _ = ListenAsync(_stop.Token);
    }

    /// <summary>Raised on a thread-pool thread. Carries the file to open, or null to show the main window.</summary>
    public event EventHandler<string?>? RequestReceived;

    /// <returns>Null when another copy is already running.</returns>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    /// <param name="filePath">The file to open; null shows the main window.</param>
    public static void SendToRunningInstance(string? filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeoutMs);

            // The running copy may then bring its window in front of this one.
            WindowInterop.AllowOtherProcessToComeToFront();
            using var writer = new StreamWriter(client);
            writer.WriteLine(filePath ?? "");
        }
        catch (Exception ex) when (ex is TimeoutException or IOException)
        {
            // The running copy is closing; there is nobody left to ask.
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private async Task ListenAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(stop);
                using var reader = new StreamReader(server);
                string? filePath = await reader.ReadLineAsync(stop);
                RequestReceived?.Invoke(this, string.IsNullOrWhiteSpace(filePath) ? null : filePath);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // The other copy went away in the middle of its message; wait for the next one.
            }
        }
    }
}

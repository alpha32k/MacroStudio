using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Input;

/// <summary>
/// A thread with a message loop for system-wide hooks: Windows calls low-level hooks while this thread
/// waits for messages, so they never depend on the user interface being responsive.
/// </summary>
internal sealed class HookThread(string name)
{
    private Thread? _thread;
    private uint _threadId;

    public bool IsRunning => _thread is not null;

    /// <param name="install">
    /// Runs on the new thread and installs the hooks. Returns the action that removes them,
    /// or null when installing failed (after removing what it had installed).
    /// </param>
    public bool Start(Func<Action?> install)
    {
        bool installed = false;
        using var started = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();

            // Creates the message queue now, so Stop can always post WM_QUIT to it.
            PeekMessage(out _, 0, 0, 0, PM_NOREMOVE);
            Action? uninstall = install();
            installed = uninstall is not null;
            started.Set();
            if (uninstall is null)
            {
                return;
            }

            while (GetMessage(out _, 0, 0, 0) > 0)
            {
            }

            uninstall();
        })
        {
            IsBackground = true,
            Name = name,
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        started.Wait();

        if (!installed)
        {
            _thread.Join();
            _thread = null;
        }

        return installed;
    }

    public void Stop()
    {
        if (_thread is null)
        {
            return;
        }

        PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        _thread.Join();
        _thread = null;
    }
}

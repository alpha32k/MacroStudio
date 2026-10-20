using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MacroStudio.App.Controls;
using MacroStudio.App.Localization;
using MacroStudio.App.NotchBar;
using MacroStudio.App.Pages;
using MacroStudio.App.Playback;
using MacroStudio.App.Recording;
using MacroStudio.App.Services;
using MacroStudio.Core;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;
using MacroStudio.Core.Recording;
using MacroStudio.Core.Settings;
using MacroStudio.Engine.Playback;
using MacroStudio.Engine.Recording;
using MacroStudio.Native.Accessibility;
using MacroStudio.Native.Clipboard;
using MacroStudio.Native.Input;
using MacroStudio.Native.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace MacroStudio.App;

/// <summary>Start-up and shut-down. This is the only place where the parts of the app are wired together.</summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A double-clicked .msmacro file arrives as the first argument.
        string? fileToOpen = e.Args.Length > 0 ? Path.GetFullPath(e.Args[0]) : null;
        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            SingleInstance.SendToRunningInstance(fileToOpen);
            Shutdown();
            return;
        }

        StartLogging();
        ComboBoxWheelBehavior.Register();
        _services = CreateServices();

        SettingsService settings = _services.GetRequiredService<SettingsService>();
        Localizer.Instance.SetLanguage(settings.Current.Language);
        settings.Changed += (_, s) => Localizer.Instance.SetLanguage(s.Language);

        // WPF UI updates the backdrop and title bar colors of Application.MainWindow when the theme changes.
        // It must be the real main window, never the transparent bar.
        MainWindow = _services.GetRequiredService<Shell.MainWindow>();
        _services.GetRequiredService<ThemeService>().Apply();
        MacroLibraryService library = _services.GetRequiredService<MacroLibraryService>();
        library.EmptyOldTrash();
        library.Refresh();

        _services.GetRequiredService<NotchBarWindow>().Start();
        TrayIcon tray = _services.GetRequiredService<TrayIcon>();
        RecordingController recording = _services.GetRequiredService<RecordingController>();
        recording.MacroSaved += (_, macro) => tray.ShowInfo(Localizer.Instance.Format("Recording_Saved", macro.Macro.Name));
        recording.NothingRecorded += (_, _) => tray.ShowInfo(Localizer.Instance["Recording_Nothing"]);
        _services.GetRequiredService<PlaybackNotifier>();
        _services.GetRequiredService<RunHistoryService>();
        RegisterHotkeys(tray, recording, _services.GetRequiredService<NotchBarViewModel>());

        MainWindowService mainWindow = _services.GetRequiredService<MainWindowService>();
        MacrosViewModel macros = _services.GetRequiredService<MacrosViewModel>();
        _singleInstance.RequestReceived += (_, filePath) => Dispatcher.BeginInvoke(() =>
        {
            if (filePath is null)
            {
                mainWindow.Show();
            }
            else
            {
                macros.OpenFile(filePath);
            }
        });
        if (fileToOpen is not null)
        {
            Dispatcher.BeginInvoke(() => macros.OpenFile(fileToOpen));
        }

        Log.Information("Macro Studio {Version} started", typeof(App).Assembly.GetName().Version);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Macro Studio stopped");
        _services?.Dispose();
        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static ServiceProvider CreateServices()
    {
        var store = new SettingsStore(AppDataPaths.SettingsFile);
        AppSettings initial = store.Load(() => AppSettings.CreateDefault(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName));

        var services = new ServiceCollection();
        services.AddSingleton(new SettingsService(store, initial));
        services.AddSingleton<ThemeService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<TrayIcon>();

        services.AddSingleton(new MacroLibrary(AppDataPaths.MacrosDirectory));
        services.AddSingleton<MacroLibraryService>();
        services.AddSingleton(new RunHistoryStore(AppDataPaths.RunHistoryFile));
        services.AddSingleton<RunHistoryService>();
        services.AddSingleton<IInputCapture, InputCapture>();
        services.AddSingleton<IWindowInspector, WindowInspector>();
        services.AddSingleton<IFocusInspector, FocusInspector>();
        services.AddSingleton<MacroRecorder>();
        services.AddSingleton<RecordingController>();

        services.AddSingleton<IInputSimulator, InputSimulator>();
        services.AddSingleton<IWindowSystem, WindowSystem>();
        services.AddSingleton<IClipboard, ClipboardAccess>();
        services.AddSingleton<IPlaybackClock, StopwatchClock>();
        services.AddSingleton<IUserActivityMonitor, UserActivityMonitor>();
        services.AddSingleton<MacroPlayer>();
        services.AddSingleton<PlaybackController>();
        services.AddSingleton<PlaybackNotifier>();

        services.AddSingleton<Shell.MainWindow>();
        services.AddSingleton<MainWindowService>();
        services.AddSingleton<NotchBarViewModel>();
        services.AddSingleton<NotchBarWindow>();

        services.AddSingleton<HomePage>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<MacrosPage>();
        services.AddSingleton<MacrosViewModel>();
        services.AddSingleton<EditorPage>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutPage>();
        services.AddSingleton<AboutViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private void StartLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(AppDataPaths.LogsDirectory, "macrostudio-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        DispatcherUnhandledException += OnUnhandledUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Log.Error(args.Exception, "Unobserved task exception");
    }

    private void RegisterHotkeys(TrayIcon tray, RecordingController recording, NotchBarViewModel bar)
    {
        HotkeyService hotkeys = _services!.GetRequiredService<HotkeyService>();
        MainWindowService mainWindow = _services!.GetRequiredService<MainWindowService>();

        Register(AppShortcuts.OpenMainWindow, mainWindow.Show);
        Register(AppShortcuts.ToggleRecording, () => _ = recording.ToggleAsync());
        Register(AppShortcuts.PlayPause, () =>
        {
            if (bar.PlayPauseCommand.CanExecute(null))
            {
                bar.PlayPauseCommand.Execute(null);
            }
        });

        void Register(AppShortcut shortcut, Action action)
        {
            if (hotkeys.TryRegister(shortcut, action) is null)
            {
                Log.Warning("Shortcut {Shortcut} is used by another program", shortcut.ToString());
                tray.ShowWarning(Localizer.Instance.Format("Hotkey_InUse", shortcut.ToString()));
            }
        }
    }

    // Keeps the app alive after an unexpected error, writes it to the log and tells the user.
    private void OnUnhandledUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        e.Handled = true;
        MessageBox.Show(
            Localizer.Instance.Format("Error_Unexpected", e.Exception.Message),
            "Macro Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

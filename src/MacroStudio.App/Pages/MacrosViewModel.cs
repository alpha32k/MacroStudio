using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.App.Playback;
using MacroStudio.App.Services;
using MacroStudio.App.Shell;
using MacroStudio.Core.Macros;
using Microsoft.Win32;
using Serilog;

namespace MacroStudio.App.Pages;

public sealed record MacroItem(MacroFile File, string Name, string Description, string Summary, string? LastRun);

public sealed record TrashItem(TrashEntry Entry, string Name, string Deleted);

/// <summary>All saved macros: search, play, edit, duplicate, rename, export, import, delete to the trash and restore.</summary>
public sealed partial class MacrosViewModel : ObservableObject
{
    private const string FileFilterExtension = "*" + MacroLibrary.FileExtension;

    private readonly MacroLibraryService _library;
    private readonly RunHistoryService _runs;
    private readonly PlaybackController _playback;
    private readonly EditorViewModel _editor;
    private readonly MainWindowService _mainWindow;

    public MacrosViewModel(
        MacroLibraryService library,
        RunHistoryService runs,
        PlaybackController playback,
        EditorViewModel editor,
        MainWindowService mainWindow)
    {
        _library = library;
        _runs = runs;
        _playback = playback;
        _editor = editor;
        _mainWindow = mainWindow;
        library.Refreshed += (_, _) => Show();
        runs.Changed += (_, _) => Show();
        Localizer.Instance.PropertyChanged += (_, _) => Show();
        playback.PropertyChanged += (_, _) => PlayCommand.NotifyCanExecuteChanged();
        Show();
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    public partial IReadOnlyList<MacroItem> Items { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrashButtonText))]
    public partial IReadOnlyList<TrashItem> TrashItems { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsShowingTrash { get; set; }

    [ObservableProperty]
    public partial string? UnreadableWarning { get; private set; }

    public bool HasMacros => _library.Macros.Count > 0;

    public bool HasNoMatches => HasMacros && Items.Count == 0;

    public string TrashButtonText => Localizer.Instance.Format("Macros_Trash", TrashItems.Count);

    partial void OnSearchTextChanged(string value) => Show();

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task Play(MacroItem item)
    {
        // Out of the way while the macro clicks; back when it ends.
        _mainWindow.Minimize();
        try
        {
            await _playback.PlayAsync(item.File.Macro, item.File.Macro.Loop, item.File.Macro.SpeedMultiplier);
        }
        finally
        {
            _mainWindow.Show();
        }
    }

    private bool CanPlay(MacroItem? item) => _playback.State == PlaybackState.Idle;

    [RelayCommand]
    private void Edit(MacroItem item)
    {
        _editor.Open(item.File.Path);
        _mainWindow.ShowPage(typeof(EditorPage));
    }

    [RelayCommand]
    private void Duplicate(MacroItem item)
    {
        string name = Localizer.Instance.Format("Macros_CopyName", item.Name);
        if (name.Length > MacroValidator.MaxNameLength)
        {
            name = name[..MacroValidator.MaxNameLength];
        }

        TryChange(() => _library.Duplicate(item.File, name));
    }

    [RelayCommand]
    private void Rename(MacroItem item)
    {
        string? name = InputDialog.AskText(Localizer.Instance["Macros_Rename"], Localizer.Instance["Macros_RenamePrompt"], item.Name)?.Trim();
        if (name is null || name == item.Name)
        {
            return;
        }

        if (name.Length is 0 or > MacroValidator.MaxNameLength)
        {
            ShowWarning(Localizer.Instance.Format("Macros_InvalidName", MacroValidator.MaxNameLength));
            return;
        }

        TryChange(() => _library.Rename(item.File, name));
    }

    [RelayCommand]
    private void Export(MacroItem item)
    {
        var dialog = new SaveFileDialog
        {
            Title = Localizer.Instance["Macros_Export"],
            Filter = FileFilter(),
            FileName = Path.GetFileName(item.File.Path),
        };
        if (dialog.ShowDialog() == true)
        {
            TryChange(() => MacroLibraryService.Export(item.File, dialog.FileName));
        }
    }

    /// <summary>The macro goes to the trash; it can be restored for 30 days.</summary>
    [RelayCommand]
    private void Delete(MacroItem item)
    {
        MessageBoxResult answer = MessageBox.Show(
            Localizer.Instance.Format("Macros_DeleteConfirm", item.Name),
            "Macro Studio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            TryChange(() => _library.MoveToTrash(item.File));
        }
    }

    [RelayCommand]
    private void Restore(TrashItem item) => TryChange(() => _library.Restore(item.Entry));

    /// <summary>Each chosen file is checked and its steps are shown before it is added.</summary>
    [RelayCommand]
    private void Import()
    {
        var dialog = new OpenFileDialog
        {
            Title = Localizer.Instance["Macros_Import"],
            Filter = FileFilter(),
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (string path in dialog.FileNames)
        {
            ImportFile(path);
        }
    }

    /// <summary>
    /// A double-clicked .msmacro file. One that is already in the library opens in the editor; any other is
    /// imported, after its steps were shown.
    /// </summary>
    public void OpenFile(string path)
    {
        if (_library.Macros.FirstOrDefault(m => string.Equals(m.Path, path, StringComparison.OrdinalIgnoreCase)) is { } file)
        {
            _editor.Open(file.Path);
            _mainWindow.ShowPage(typeof(EditorPage));
            return;
        }

        _mainWindow.ShowPage(typeof(MacrosPage));
        ImportFile(path);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(_library.Folder);
        Process.Start(new ProcessStartInfo(_library.Folder) { UseShellExecute = true });
    }

    private void ImportFile(string path)
    {
        MacroDefinition macro;
        try
        {
            macro = MacroLibrary.Load(path);
        }
        catch (Exception ex) when (ex is InvalidMacroFileException or IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Macro file could not be imported: {Path}", path);
            ShowWarning(Localizer.Instance.Format("Macros_ImportFailed", Path.GetFileName(path), ex.Message));
            return;
        }

        new SaveMacroWindow(new SaveMacroViewModel(macro, _library, isImport: true)).ShowDialog();
    }

    private static string FileFilter() => $"{Localizer.Instance["Macros_FileType"]} ({FileFilterExtension})|{FileFilterExtension}";

    private static void ShowWarning(string message) =>
        MessageBox.Show(message, "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Warning);

    private static void TryChange(Action change)
    {
        try
        {
            change();
        }
        catch (Exception ex) when (ex is InvalidMacroFileException or IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Macro file could not be changed");
            ShowWarning(Localizer.Instance.Format("Macros_ActionFailed", ex.Message));
        }
    }

    private void Show()
    {
        string search = SearchText.Trim();
        Items = _library.Macros
            .Where(file => search.Length == 0
                || file.Macro.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || file.Macro.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Select(file => new MacroItem(
                file,
                file.Macro.Name,
                file.Macro.Description,
                MacroSummary.Of(file),
                MacroSummary.LastRunOf(_runs.Of(file.Macro.Id))))
            .ToList();

        Localizer text = Localizer.Instance;
        TrashItems = _library.Trash
            .Select(entry => new TrashItem(
                entry,
                entry.Name,
                text.Format(
                    "Macros_DeletedAt",
                    entry.DeletedAt.ToString("g", text.Culture),
                    Math.Max(0, (int)Math.Ceiling((entry.DeletedAt + MacroLibrary.TrashKeepTime - DateTime.Now).TotalDays)))))
            .ToList();

        UnreadableWarning = _library.UnreadableFiles.Count == 0
            ? null
            : text.Format("Macros_Unreadable", string.Join(", ", _library.UnreadableFiles.Select(Path.GetFileName)));
        OnPropertyChanged(nameof(HasMacros));
    }
}

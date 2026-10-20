using System.ComponentModel;
using System.Windows;
using MacroStudio.App.Controls;
using MacroStudio.App.Localization;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace MacroStudio.App.Macros;

/// <summary>Opens after a recording or for an imported file: name it, look at the steps, save or cancel.</summary>
public partial class SaveMacroWindow : FluentWindow
{
    private readonly SaveMacroViewModel _viewModel;
    private bool _mayClose;

    public SaveMacroWindow(SaveMacroViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        TemplateButtonNames.NameWhenLoaded(this);
        viewModel.Saved += (_, _) =>
        {
            _mayClose = true;
            Close();
        };
        Loaded += (_, _) =>
        {
            Activate();
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    // A recording can take minutes to make, so closing it without saving asks first (Discard, X, Alt+F4).
    // An import is only a file copy and closes at once. WPF does not allow opening a window while this one
    // is closing, so the close is cancelled and the question is asked right after.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_mayClose && !_viewModel.IsImport)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(AskToDiscard);
        }

        base.OnClosing(e);
    }

    private void AskToDiscard()
    {
        MessageBoxResult answer = MessageBox.Show(
            this,
            Localizer.Instance["Save_DiscardConfirm"],
            "Macro Studio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            _mayClose = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}

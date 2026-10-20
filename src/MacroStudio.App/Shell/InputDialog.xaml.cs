using System.Windows;
using System.Windows.Automation;
using MacroStudio.App.Controls;
using MacroStudio.App.Localization;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Shell;

/// <summary>A small question window: either a line of text or a choice from a list.</summary>
public partial class InputDialog : FluentWindow
{
    private InputDialog(string title, string prompt)
    {
        InitializeComponent();
        Title = title;
        DialogTitle.Title = title;
        PromptText.Text = prompt;

        // Screen readers read the question as the name of the box that answers it.
        AutomationProperties.SetName(AnswerBox, prompt);
        AutomationProperties.SetName(ChoiceBox, prompt);
        TemplateButtonNames.NameWhenLoaded(this);
        Owner = Application.Current.MainWindow is { IsVisible: true } main ? main : null;
        WindowStartupLocation = Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
    }

    /// <returns>The typed text, or null when cancelled.</returns>
    public static string? AskText(string title, string prompt, string initialText = "")
    {
        var dialog = new InputDialog(title, prompt);
        dialog.ChoiceBox.Visibility = Visibility.Collapsed;
        dialog.AnswerBox.Text = initialText;
        dialog.Loaded += (_, _) =>
        {
            dialog.AnswerBox.Focus();
            dialog.AnswerBox.SelectAll();
        };
        return dialog.ShowDialog() == true ? dialog.AnswerBox.Text : null;
    }

    /// <returns>The chosen value, or default when cancelled.</returns>
    public static T? AskChoice<T>(string title, string prompt, IReadOnlyList<LocalizedOption<T>> choices)
    {
        var dialog = new InputDialog(title, prompt);
        dialog.AnswerBox.Visibility = Visibility.Collapsed;
        dialog.ChoiceBox.ItemsSource = choices;
        dialog.ChoiceBox.SelectedIndex = 0;
        dialog.Loaded += (_, _) => dialog.ChoiceBox.Focus();
        return dialog.ShowDialog() == true && dialog.ChoiceBox.SelectedItem is LocalizedOption<T> choice ? choice.Value : default;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}

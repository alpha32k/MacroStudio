using System.Globalization;
using System.Windows;
using MacroStudio.App.Controls;
using MacroStudio.App.Localization;
using MacroStudio.Core.Macros;
using Wpf.Ui.Controls;

namespace MacroStudio.App.Macros;

/// <summary>Asks when a macro should play by itself: once, every day or on weekdays, at a time.</summary>
public partial class ScheduleWindow : FluentWindow
{
    private static readonly IReadOnlyList<LocalizedOption<ScheduleFrequency>> Frequencies =
    [
        LocalizedOption<ScheduleFrequency>.FromKey(ScheduleFrequency.Daily, "Schedule_Daily"),
        LocalizedOption<ScheduleFrequency>.FromKey(ScheduleFrequency.Weekdays, "Schedule_Weekdays"),
        LocalizedOption<ScheduleFrequency>.FromKey(ScheduleFrequency.Once, "Schedule_Once"),
    ];

    private MacroSchedule? _answer;
    private bool _isRemoved;

    private ScheduleWindow(string macroName, MacroSchedule? current)
    {
        InitializeComponent();
        Title = Localizer.Instance.Format("Schedule_Title", macroName);
        DialogTitle.Title = Title;
        TemplateButtonNames.NameWhenLoaded(this);
        Owner = Application.Current.MainWindow is { IsVisible: true } main ? main : null;
        WindowStartupLocation = Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;

        FrequencyBox.ItemsSource = Frequencies;
        FrequencyBox.SelectedItem = Frequencies.First(f => f.Value == (current?.Frequency ?? ScheduleFrequency.Daily));
        DateBox.SelectedDate = (current?.Date ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        TimeBox.Text = (current?.Time ?? new TimeOnly(9, 0)).ToString("HH:mm", CultureInfo.InvariantCulture);
        RemoveButton.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <returns>Changed = false when cancelled; otherwise the new schedule, null when it was removed.</returns>
    public static (bool Changed, MacroSchedule? Schedule) Ask(string macroName, MacroSchedule? current)
    {
        var window = new ScheduleWindow(macroName, current);
        bool changed = window.ShowDialog() == true;
        return (changed, window._isRemoved ? null : window._answer);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string[] formats = ["H:mm", "HH:mm", "H.mm", "HH.mm"];
        if (!TimeOnly.TryParseExact(TimeBox.Text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
        {
            ErrorText.Text = Localizer.Instance["Schedule_InvalidTime"];
            return;
        }

        var frequency = ((LocalizedOption<ScheduleFrequency>)FrequencyBox.SelectedItem).Value;
        DateOnly date = DateOnly.FromDateTime(DateBox.SelectedDate ?? DateTime.Today);
        if (frequency == ScheduleFrequency.Once && date.ToDateTime(time) <= DateTime.Now)
        {
            ErrorText.Text = Localizer.Instance["Schedule_InThePast"];
            return;
        }

        _answer = new MacroSchedule(frequency, time, date);
        DialogResult = true;
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        _isRemoved = true;
        DialogResult = true;
    }
}

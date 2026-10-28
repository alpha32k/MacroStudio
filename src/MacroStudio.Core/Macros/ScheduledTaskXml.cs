using System.Globalization;
using System.Xml.Linq;

namespace MacroStudio.Core.Macros;

/// <summary>
/// The Windows Task Scheduler definition for a scheduled macro. Written as XML because the "schtasks" date options
/// follow the Windows date format of each computer; the XML format is the same everywhere.
/// </summary>
public static class ScheduledTaskXml
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <param name="executable">Macro Studio's own program file.</param>
    /// <param name="arguments">For example: --play "1f0c…"</param>
    public static string Build(MacroSchedule schedule, string description, string executable, string arguments)
    {
        string start = schedule.Date.ToDateTime(schedule.Time).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        XElement trigger = schedule.Frequency switch
        {
            ScheduleFrequency.Daily => new XElement(Ns + "CalendarTrigger",
                new XElement(Ns + "StartBoundary", start),
                new XElement(Ns + "ScheduleByDay", new XElement(Ns + "DaysInterval", 1))),
            ScheduleFrequency.Weekdays => new XElement(Ns + "CalendarTrigger",
                new XElement(Ns + "StartBoundary", start),
                new XElement(Ns + "ScheduleByWeek",
                    new XElement(Ns + "WeeksInterval", 1),
                    new XElement(Ns + "DaysOfWeek",
                        new XElement(Ns + "Monday"),
                        new XElement(Ns + "Tuesday"),
                        new XElement(Ns + "Wednesday"),
                        new XElement(Ns + "Thursday"),
                        new XElement(Ns + "Friday")))),
            _ => new XElement(Ns + "TimeTrigger", new XElement(Ns + "StartBoundary", start)),
        };

        var task = new XElement(Ns + "Task",
            new XAttribute("version", "1.2"),
            new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", description)),
            new XElement(Ns + "Triggers", trigger),

            // Only while the person is signed in, with their normal rights: a macro needs their screen and keyboard.
            new XElement(Ns + "Principals",
                new XElement(Ns + "Principal",
                    new XAttribute("id", "Author"),
                    new XElement(Ns + "LogonType", "InteractiveToken"),
                    new XElement(Ns + "RunLevel", "LeastPrivilege"))),
            new XElement(Ns + "Settings",
                new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(Ns + "DisallowStartIfOnBatteries", false),
                new XElement(Ns + "StopIfGoingOnBatteries", false),
                new XElement(Ns + "StartWhenAvailable", false),
                new XElement(Ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(Ns + "Enabled", true)),
            new XElement(Ns + "Actions",
                new XAttribute("Context", "Author"),
                new XElement(Ns + "Exec",
                    new XElement(Ns + "Command", executable),
                    new XElement(Ns + "Arguments", arguments))));

        return new XDocument(new XDeclaration("1.0", "UTF-16", null), task).ToString();
    }

    /// <summary>One task per macro, named by its id, so renaming the macro keeps its schedule.</summary>
    public static string TaskName(Guid macroId) => "Macro Studio " + macroId.ToString("D");
}

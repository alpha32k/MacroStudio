using System.Xml.Linq;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Tests.Macros;

public sealed class ScheduleAndLaunchTests : IDisposable
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(new string[0], LaunchKind.ShowMainWindow, null)]
    [InlineData(new[] { @"C:\m\Fatura.msmacro" }, LaunchKind.OpenFile, @"C:\m\Fatura.msmacro")]
    [InlineData(new[] { "--play", "Fatura girişi" }, LaunchKind.Play, "Fatura girişi")]
    [InlineData(new[] { "--PLAY", " 1f0c " }, LaunchKind.Play, "1f0c")]
    [InlineData(new[] { "--play" }, LaunchKind.ShowMainWindow, null)]
    [InlineData(new[] { "--unknown" }, LaunchKind.ShowMainWindow, null)]
    public void Parse_ReadsTheCommandLine(string[] args, LaunchKind kind, string? value)
    {
        Assert.Equal(new LaunchRequest(kind, value), LaunchRequest.Parse(args));
    }

    [Theory]
    [InlineData(LaunchKind.ShowMainWindow, null)]
    [InlineData(LaunchKind.OpenFile, @"C:\m\Fatura.msmacro")]
    [InlineData(LaunchKind.Play, "Fatura girişi")]
    public void Message_ToTheRunningCopy_ReadsBackTheSame(LaunchKind kind, string? value)
    {
        var request = new LaunchRequest(kind, value);

        Assert.Equal(request, LaunchRequest.FromMessage(request.ToMessage()));
    }

    [Fact]
    public void Message_FromAnOlderVersion_IsStillAFileOrTheMainWindow()
    {
        Assert.Equal(new LaunchRequest(LaunchKind.OpenFile, @"C:\a.msmacro"), LaunchRequest.FromMessage(@"C:\a.msmacro"));
        Assert.Equal(LaunchKind.ShowMainWindow, LaunchRequest.FromMessage("").Kind);
    }

    [Fact]
    public void TaskXml_Weekdays_RunsMondayToFridayAtTheTime_OnlyWhileSignedIn()
    {
        var schedule = new MacroSchedule(ScheduleFrequency.Weekdays, new TimeOnly(9, 5), new DateOnly(2026, 11, 2));

        XDocument task = XDocument.Parse(ScheduledTaskXml.Build(schedule, "Macro Studio: Rapor", @"C:\P\MacroStudio.exe", "--play \"x\""));

        Assert.Equal("2026-11-02T09:05:00", task.Descendants(Ns + "StartBoundary").Single().Value);
        Assert.Equal(
            ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
            task.Descendants(Ns + "DaysOfWeek").Single().Elements().Select(e => e.Name.LocalName));
        Assert.Equal("InteractiveToken", task.Descendants(Ns + "LogonType").Single().Value);
        Assert.Equal("LeastPrivilege", task.Descendants(Ns + "RunLevel").Single().Value);
        Assert.Equal(@"C:\P\MacroStudio.exe", task.Descendants(Ns + "Command").Single().Value);
        Assert.Equal("--play \"x\"", task.Descendants(Ns + "Arguments").Single().Value);
    }

    [Theory]
    [InlineData(ScheduleFrequency.Once, "TimeTrigger")]
    [InlineData(ScheduleFrequency.Daily, "ScheduleByDay")]
    public void TaskXml_HasTheTriggerOfItsFrequency(ScheduleFrequency frequency, string element)
    {
        var schedule = new MacroSchedule(frequency, new TimeOnly(18, 30), new DateOnly(2026, 12, 31));

        XDocument task = XDocument.Parse(ScheduledTaskXml.Build(schedule, "d", "e", "a"));

        Assert.Single(task.Descendants(Ns + element));
    }

    [Fact]
    public void Schedule_IsSavedWithTheMacro()
    {
        var schedule = new MacroSchedule(ScheduleFrequency.Daily, new TimeOnly(7, 45), new DateOnly(2026, 10, 29));
        var macro = new MacroDefinition { Name = "Sabah", Steps = [new CopyStep()], Schedule = schedule };

        string path = new MacroLibrary(_folder).SaveNew(macro);

        Assert.Equal(schedule, MacroLibrary.Load(path).Schedule);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}

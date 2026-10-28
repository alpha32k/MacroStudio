namespace MacroStudio.Core.Macros;

public enum ScheduleFrequency
{
    /// <summary>One time, on <see cref="MacroSchedule.Date"/>.</summary>
    Once,
    Daily,

    /// <summary>Monday to Friday.</summary>
    Weekdays,
}

/// <summary>When Windows plays a macro by itself. The person must be signed in; the macro needs the screen.</summary>
/// <param name="Date">The day for <see cref="ScheduleFrequency.Once"/>; the first day for the others.</param>
public sealed record MacroSchedule(ScheduleFrequency Frequency, TimeOnly Time, DateOnly Date);

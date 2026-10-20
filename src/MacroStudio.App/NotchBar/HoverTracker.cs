namespace MacroStudio.App.NotchBar;

public enum HoverDecision
{
    None,
    Expand,
    Collapse,
}

/// <summary>
/// Decides when the bar opens and closes. The pointer must rest on the bar for the open delay,
/// so a mouse passing on its way to browser tabs does not open it by accident.
/// </summary>
public sealed class HoverTracker
{
    public static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(600);

    private long? _enteredAtMs;
    private long? _leftAtMs;

    public TimeSpan OpenDelay { get; set; }

    /// <summary>A pinned bar opens at once and stays open until the user unpins it.</summary>
    public bool IsPinned { get; set; }

    public bool IsExpanded { get; private set; }

    public HoverDecision Update(bool isPointerOverBar, long nowMs)
    {
        if (IsPinned)
        {
            _enteredAtMs = null;
            _leftAtMs = null;
            return IsExpanded ? HoverDecision.None : SetExpanded(true);
        }

        if (isPointerOverBar)
        {
            _leftAtMs = null;
            _enteredAtMs ??= nowMs;
            return !IsExpanded && nowMs - _enteredAtMs >= OpenDelay.TotalMilliseconds ? SetExpanded(true) : HoverDecision.None;
        }

        _enteredAtMs = null;
        _leftAtMs ??= nowMs;
        return IsExpanded && nowMs - _leftAtMs >= CloseDelay.TotalMilliseconds ? SetExpanded(false) : HoverDecision.None;
    }

    /// <summary>Closes at once, for example when the bar is hidden.</summary>
    public void Reset()
    {
        IsExpanded = false;
        _enteredAtMs = null;
        _leftAtMs = null;
    }

    private HoverDecision SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        return expanded ? HoverDecision.Expand : HoverDecision.Collapse;
    }
}

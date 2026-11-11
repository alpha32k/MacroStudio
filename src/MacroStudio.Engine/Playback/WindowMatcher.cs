using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Playback;

/// <summary>
/// Finds the open window that a recorded step belongs to. Excel workbook windows require an exact title
/// to avoid sending input to another workbook. Other titles can change while people work.
/// </summary>
public static class WindowMatcher
{
    // Standard dialogs share one window kind, so for them the title is what tells "Save" from "Open".
    private const string DialogClassName = "#32770";
    private const double MinDialogTitleSimilarity = 0.6;

    /// <param name="windows">Open windows, front to back; Excel workbooks must have a unique match.</param>
    public static WindowCandidate? FindBest(WindowTarget target, IReadOnlyList<WindowCandidate> windows)
    {
        if (IsExcelWorkbook(target))
        {
            return ExcelWorkbookMatches(target, windows).Take(2).ToArray() is [var match] ? match : null;
        }

        WindowCandidate? best = null;
        double bestSimilarity = -1;
        foreach (WindowCandidate window in windows)
        {
            if (!MatchesWindowKind(target, window))
            {
                continue;
            }

            double similarity = TitleSimilarity(window.Title, target.Title);
            if (similarity > bestSimilarity)
            {
                best = window;
                bestSimilarity = similarity;
            }
        }

        bool isWrongDialog = target.ClassName == DialogClassName && bestSimilarity < MinDialogTitleSimilarity;
        return isWrongDialog ? null : best;
    }

    /// <summary>Checks whether a target is open, including Excel workbooks with indistinguishable titles.</summary>
    public static bool IsOpen(WindowTarget target, IReadOnlyList<WindowCandidate> windows) =>
        IsExcelWorkbook(target) ? ExcelWorkbookMatches(target, windows).Any() : FindBest(target, windows) is not null;

    private static bool IsExcelWorkbook(WindowTarget target) =>
        string.Equals(target.ProcessName, "EXCEL", StringComparison.OrdinalIgnoreCase) && target.ClassName == "XLMAIN";

    private static IEnumerable<WindowCandidate> ExcelWorkbookMatches(WindowTarget target, IReadOnlyList<WindowCandidate> windows) =>
        string.IsNullOrWhiteSpace(target.Title) ? [] : windows.Where(window => MatchesWindowKind(target, window)
            && string.Equals(window.Title, target.Title, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesWindowKind(WindowTarget target, WindowCandidate window) =>
        string.Equals(window.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(window.ClassName, target.ClassName, StringComparison.Ordinal);

    /// <summary>1 for the same title, 0 for nothing in common (based on the number of characters to change).</summary>
    public static double TitleSimilarity(string a, string b)
    {
        int longer = Math.Max(a.Length, b.Length);
        return longer == 0 ? 1 : 1 - (EditDistance(a, b) / (double)longer);
    }

    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int replace = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(replace, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

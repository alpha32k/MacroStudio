using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Playback;

/// <summary>
/// Finds the open window that a recorded step belongs to. Titles change while people work (Notepad adds "*",
/// Excel shows the file name), so the program and the window kind decide; the title only picks between
/// several windows of the same kind.
/// </summary>
public static class WindowMatcher
{
    // Standard dialogs share one window kind, so for them the title is what tells "Save" from "Open".
    private const string DialogClassName = "#32770";
    private const double MinDialogTitleSimilarity = 0.6;

    /// <param name="windows">Open windows, front to back; when two match equally well the front one wins.</param>
    public static WindowCandidate? FindBest(WindowTarget target, IReadOnlyList<WindowCandidate> windows)
    {
        WindowCandidate? best = null;
        double bestSimilarity = -1;
        foreach (WindowCandidate window in windows)
        {
            if (!string.Equals(window.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(window.ClassName, target.ClassName, StringComparison.Ordinal))
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

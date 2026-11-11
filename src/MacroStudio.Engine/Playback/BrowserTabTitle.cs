using System.Text.RegularExpressions;
using MacroStudio.Core.Macros;

namespace MacroStudio.Engine.Playback;

internal static partial class BrowserTabTitle
{
    private const string ChromeSuffix = " - Google Chrome";
    private const string EdgeSuffix = " - Microsoft Edge";
    private const string EdgeWithProfileSuffix = " - Microsoft\u200B Edge";
    private const string ProfileSeparator = " - ";

    public static string? ExpectedHost(WindowTarget target, string title) =>
        target.BrowserHost ?? (Matches("WhatsApp", title) ? "web.whatsapp.com" : null);

    public static string FromWindow(string processName, string title)
    {
        title = title.Trim();
        string? suffix = BrowserSuffix(processName);
        if (suffix == EdgeSuffix && title.EndsWith(EdgeWithProfileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // Edge's Windows title may include a profile name and a summary of the other tabs.
            title = title[..^EdgeWithProfileSuffix.Length];
            int profile = title.LastIndexOf(ProfileSeparator, StringComparison.Ordinal);
            if (profile >= 0)
            {
                title = title[..profile];
            }
        }
        else if (suffix is not null && title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            title = title[..^suffix.Length];
        }

        return (suffix == EdgeSuffix ? EdgeOtherPages().Replace(title, "") : title).Trim();
    }

    public static bool Matches(string expected, string actual) => expected.Length != 0
        && string.Equals(WithoutUnreadCount(expected), WithoutUnreadCount(actual), StringComparison.OrdinalIgnoreCase);

    private static string? BrowserSuffix(string processName) =>
        string.Equals(processName, "chrome", StringComparison.OrdinalIgnoreCase) ? ChromeSuffix
        : string.Equals(processName, "msedge", StringComparison.OrdinalIgnoreCase) ? EdgeSuffix
        : null;

    [GeneratedRegex(@" (?:ve diğer [0-9]+ sayfa|and [0-9]+ more pages?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EdgeOtherPages();

    private static string WithoutUnreadCount(string title)
    {
        title = title.Trim();
        int closing = title.IndexOf(')');
        if (closing > 1 && title[0] == '(' && int.TryParse(title.AsSpan(1, closing - 1), out _))
        {
            string pageTitle = title[(closing + 1)..].TrimStart();
            if (string.Equals(pageTitle, "WhatsApp", StringComparison.OrdinalIgnoreCase))
            {
                return pageTitle;
            }
        }

        return title;
    }
}

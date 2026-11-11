using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace MacroStudio.Native.Accessibility;

/// <summary>Reads and selects tabs in the browser's own UI, without interacting with page controls.</summary>
internal static class BrowserTabs
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromMilliseconds(750);

    public static IReadOnlyList<string> List(nint window) => Read(window).Select(tab => tab.Title).ToArray();

    public static string? ActiveHost(nint window, TimeSpan? timeout = null)
    {
        Task<string?> read = Task.Run(() => ReadActiveHost(window));
        return read.Wait(timeout ?? ReadTimeout) ? read.Result : null;
    }

    private static string? ReadActiveHost(nint window)
    {
        try
        {
            AutomationElement[] sources = AddressSources(AutomationElement.FromHandle(window)).ToArray();
            AutomationElement[] documents = sources.Where(source => source.Current.ControlType == ControlType.Document).ToArray();
            if (documents is [var document] && ReadValue(document) is { Length: > 0 } address)
            {
                return HostOf(address, allowHiddenScheme: false);
            }

            // The address bar identifies the current tab when a sidebar exposes another document.
            // Ignore a value being edited, and require a matching document if more than one is visible.
            if (sources.Where(source => source.Current.ControlType == ControlType.Edit).Take(2).ToArray() is not [var bar]
                || bar.Current.HasKeyboardFocus || ReadValue(bar) is not { } value || HostOf(value, allowHiddenScheme: true) is not { } host)
            {
                return null;
            }

            return documents.Length < 2 || documents.Any(candidate => ReadValue(candidate) is { } url
                && string.Equals(host, HostOf(url, allowHiddenScheme: false), StringComparison.OrdinalIgnoreCase)) ? host : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadValue(AutomationElement element) =>
        element.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern) && pattern is ValuePattern value ? value.Current.Value : null;

    private static string? HostOf(string value, bool allowHiddenScheme)
    {
        string address = value.Trim();
        if (address.Length == 0 || address.Any(char.IsWhiteSpace))
        {
            return null;
        }

        if (allowHiddenScheme && !address.Contains("://", StringComparison.Ordinal))
        {
            address = "https://" + address;
        }

        return Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
            && uri.UserInfo.Length == 0 ? uri.IdnHost.TrimEnd('.') : null;
    }

    private static IEnumerable<AutomationElement> AddressSources(AutomationElement parent)
    {
        TreeWalker walker = TreeWalker.ControlViewWalker;
        for (AutomationElement? child = walker.GetFirstChild(parent); child is not null; child = walker.GetNextSibling(child))
        {
            AutomationElement.AutomationElementInformation info = child.Current;
            if (info.ControlType == ControlType.Document || info.ControlType == ControlType.Edit)
            {
                // Never descend into a web document, where a page can define its own controls.
                if (!info.IsOffscreen && (info.ControlType == ControlType.Document
                    || info.ClassName == "OmniboxViewViews" || info.AutomationId == "addressEditBox"))
                {
                    yield return child;
                }

                continue;
            }

            foreach (AutomationElement source in AddressSources(child))
            {
                yield return source;
            }
        }
    }

    public static bool TrySelect(nint window, string title)
    {
        var matches = Read(window).Where(tab => string.Equals(tab.Title, title, StringComparison.Ordinal)).Take(2).ToArray();
        if (matches is not [var match])
        {
            return false;
        }

        try
        {
            if (match.Element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selected)
                && selected is SelectionItemPattern selection)
            {
                if (!selection.Current.IsSelected)
                {
                    selection.Select();
                }

                return true;
            }

            if (match.Element.TryGetCurrentPattern(InvokePattern.Pattern, out object invoked) && invoked is InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or ElementNotEnabledException or COMException or InvalidOperationException or ArgumentException)
        {
            // The tab or window may have closed while it was being selected.
        }

        return false;
    }

    private static Tab[] Read(nint window)
    {
        // A hung browser may never answer UI Automation. A timed-out read cannot switch a tab later.
        Task<Tab[]> read = Task.Run(() => ReadCore(window));
        return read.Wait(ReadTimeout) ? read.Result : [];
    }

    private static Tab[] ReadCore(nint window)
    {
        try
        {
            return ReadChildren(AutomationElement.FromHandle(window), insideTabStrip: false).ToArray();
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            return [];
        }
    }

    private static IEnumerable<Tab> ReadChildren(AutomationElement parent, bool insideTabStrip)
    {
        TreeWalker walker = TreeWalker.ControlViewWalker;
        for (AutomationElement? child = walker.GetFirstChild(parent); child is not null; child = walker.GetNextSibling(child))
        {
            ControlType type = child.Current.ControlType;
            if (type == ControlType.Document)
            {
                continue;
            }

            if (type == ControlType.TabItem)
            {
                if (insideTabStrip && child.Current.Name is { Length: > 0 } title)
                {
                    yield return new Tab(child, title);
                }

                continue;
            }

            foreach (Tab tab in ReadChildren(child, insideTabStrip || type == ControlType.Tab))
            {
                yield return tab;
            }
        }
    }

    private sealed record Tab(AutomationElement Element, string Title);
}

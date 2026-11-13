using System.IO;
using System.Runtime.InteropServices;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;
using Microsoft.CSharp.RuntimeBinder;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

/// <summary>Uses Excel's object model for already-open workbooks without changing its macro security settings.</summary>
internal static unsafe class ExcelMacros
{
    private const uint NativeObjectModel = 0xFFFFFFF0;
    private static readonly Guid DispatchInterface = new("00020400-0000-0000-C000-000000000046");

    public static PlaybackFailure? Run(IWindowSystem windows, string path, string procedure, CancellationToken cancellation)
    {
        if (!ExcelMacroStep.IsWorkbookPath(path) || !ExcelMacroStep.IsProcedureName(procedure))
        {
            return PlaybackFailure.ExcelMacroFailed;
        }

        string fullPath = Path.GetFullPath(path);
        foreach (WindowCandidate window in windows.ListWindows())
        {
            cancellation.ThrowIfCancellationRequested();
            if (window.ClassName != "XLMAIN" || !string.Equals(window.ProcessName, "EXCEL", StringComparison.OrdinalIgnoreCase)
                || windows.IsElevatedAboveUs(window.Handle) || FindWorksheetWindow(window.Handle) is not { } worksheet)
            {
                continue;
            }

            if (RunInExcel(worksheet, fullPath, procedure, cancellation, out PlaybackFailure? failure))
            {
                return failure;
            }
        }

        return PlaybackFailure.ExcelWorkbookNotOpen;
    }

    // The Excel window supplies the Application for its own process. Looking in its Workbooks collection also
    // handles multiple workbook windows belonging to one instance without relying on their displayed captions.
    private static bool RunInExcel(nint worksheet, string path, string procedure, CancellationToken cancellation,
        out PlaybackFailure? failure)
    {
        failure = null;
        object? window = null;
        object? application = null;
        object? books = null;
        bool activating = false;
        try
        {
            Guid iid = DispatchInterface;
            if (AccessibleObjectFromWindow(worksheet, NativeObjectModel, in iid, out window) != 0 || window is null)
            {
                return false;
            }

            application = ((dynamic)window).Application;
            books = ((dynamic)application).Workbooks;
            int count = ((dynamic)books).Count;
            for (int index = 1; index <= count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                object? book = null;
                object? active = null;
                try
                {
                    book = ((dynamic)books).Item[index];
                    if (!SamePath(path, (string)((dynamic)book).FullName))
                    {
                        continue;
                    }

                    string bookName = ((dynamic)book).Name;
                    cancellation.ThrowIfCancellationRequested();
                    activating = true;
                    ((dynamic)book).Activate();
                    active = ((dynamic)application).ActiveWorkbook;
                    cancellation.ThrowIfCancellationRequested();
                    if (active is null || !SamePath(path, (string)((dynamic)active).FullName))
                    {
                        failure = PlaybackFailure.ExcelMacroFailed;
                        return true;
                    }

                    ((dynamic)application).Run("'" + bookName.Replace("'", "''", StringComparison.Ordinal) + "'!" + procedure);
                    return true;
                }
                finally
                {
                    Release(active);
                    Release(book);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException)
        {
            // Excel may have already run activation events or part of the procedure. Never invoke it again.
            if (activating)
            {
                failure = PlaybackFailure.ExcelMacroFailed;
                return true;
            }
        }
        finally
        {
            Release(books);
            Release(application);
            Release(window);
        }

        return false;
    }

    private static bool SamePath(string path, string other) => Path.IsPathFullyQualified(other)
        && string.Equals(path, Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);

    private static nint? FindWorksheetWindow(nint window)
    {
        nint worksheet = 0;
        EnumChildWindows(window, &FindExcelChild, (nint)(&worksheet));
        return worksheet != 0 ? worksheet : null;
    }

    [UnmanagedCallersOnly]
    private static int FindExcelChild(nint child, nint state)
    {
        if (WindowText.ClassNameOf(child) != "EXCEL7")
        {
            return 1;
        }

        *(nint*)state = child;
        return 0;
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    [DllImport("oleacc.dll", ExactSpelling = true)]
    private static extern int AccessibleObjectFromWindow(nint window, uint objectId, in Guid interfaceId,
        [MarshalAs(UnmanagedType.IDispatch)] out object? value);
}

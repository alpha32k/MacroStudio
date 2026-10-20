using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Core;

namespace MacroStudio.App.Pages;

public sealed record ThirdPartyComponent(string Name, string License, Uri Website);

public sealed partial class AboutViewModel
{
    public string Version { get; } = typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public IReadOnlyList<ThirdPartyComponent> Components { get; } =
    [
        new("WPF UI", "MIT", new Uri("https://github.com/lepoco/wpfui")),
        new("CommunityToolkit.Mvvm", "MIT", new Uri("https://github.com/CommunityToolkit/dotnet")),
        new("Microsoft.Extensions.DependencyInjection", "MIT", new Uri("https://github.com/dotnet/runtime")),
        new("Serilog", "Apache-2.0", new Uri("https://github.com/serilog/serilog")),
    ];

    [RelayCommand]
    private static void OpenLogFolder()
    {
        Directory.CreateDirectory(AppDataPaths.LogsDirectory);
        Process.Start(new ProcessStartInfo(AppDataPaths.LogsDirectory) { UseShellExecute = true });
    }
}

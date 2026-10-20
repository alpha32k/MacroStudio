using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MacroStudio.Core.Tests.Repository;

/// <summary>
/// Checks the app's English and Turkish text files against each other, so a text added in only one language
/// or a translation that lost a {0} placeholder is caught before anyone sees "!Key!" on screen.
/// </summary>
public sealed partial class LocalizationFilesTests
{
    private static readonly string ResourcesFolder = Path.Combine(FindRepositoryRoot(), "src", "MacroStudio.App", "Resources");

    [Fact]
    public void EnglishAndTurkish_HaveTheSameKeys()
    {
        Dictionary<string, string> english = ReadStrings("Strings.resx");
        Dictionary<string, string> turkish = ReadStrings("Strings.tr.resx");

        Assert.Empty(english.Keys.Except(turkish.Keys));
        Assert.Empty(turkish.Keys.Except(english.Keys));
    }

    [Fact]
    public void EnglishAndTurkish_UseTheSamePlaceholders()
    {
        Dictionary<string, string> english = ReadStrings("Strings.resx");
        Dictionary<string, string> turkish = ReadStrings("Strings.tr.resx");

        foreach ((string key, string englishText) in english)
        {
            Assert.True(
                Placeholders(englishText).SetEquals(Placeholders(turkish[key])),
                $"Placeholders differ for '{key}'.");
        }
    }

    private static Dictionary<string, string> ReadStrings(string fileName) =>
        XDocument.Load(Path.Combine(ResourcesFolder, fileName))
            .Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!, StringComparer.Ordinal);

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "MacroStudio.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("MacroStudio.slnx was not found above the test folder.");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}

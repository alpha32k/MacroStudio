using System.Globalization;
using System.Text;
using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Tests.Macros;

public sealed class DataListTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    public DataListTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Parse_TurkishExcelCsv_UsesSemicolonsAndQuotes()
    {
        IReadOnlyList<IReadOnlyList<string>> rows = DataListReader.Parse("Kod;Ad;Not\r\nK1;\"Şahin; Ahmet\";\"Dedi ki \"\"tamam\"\"\"\r\nK2;Çağrı;\r\n\r\n");

        Assert.Equal(3, rows.Count);
        Assert.Equal(["K1", "Şahin; Ahmet", "Dedi ki \"tamam\""], rows[1]);
        Assert.Equal(["K2", "Çağrı", ""], rows[2]);
    }

    [Fact]
    public void Parse_OneValuePerLine_KeepsCommasInsideTheValue()
    {
        IReadOnlyList<IReadOnlyList<string>> rows = DataListReader.Parse("elma\narmut\nkiraz");

        Assert.Equal([["elma"], ["armut"], ["kiraz"]], rows);
    }

    [Fact]
    public void Read_WithHeader_LeavesTheHeaderOutAndNamesTheColumns()
    {
        string path = Write("liste.csv", "Kod,Tutar\nK1,10\nK2,20\n", Encoding.UTF8);

        (IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<string> columns) = DataListReader.Read(new DataList(path, HasHeader: true));

        Assert.Equal(["Kod", "Tutar"], columns);
        Assert.Equal([["K1", "10"], ["K2", "20"]], rows);
    }

    [Fact]
    public void Read_AFileInTheTurkishWindowsCodePage_KeepsTheTurkishLetters()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string path = Write("eski.csv", "Şehir;İlçe\nİzmir;Karşıyaka\n", Encoding.GetEncoding(1254));

        (IReadOnlyList<IReadOnlyList<string>> rows, _) = DataListReader.Read(new DataList(path, HasHeader: true));

        Assert.Equal(["İzmir", "Karşıyaka"], rows[0]);
    }

    [Fact]
    public void Fill_PutsInTheRowColumnsAndTheRoundAndLeavesOtherBracesAlone()
    {
        var values = new TemplateValues(["K7", "Ahmet"], ["Kod", "Ad"], 3, new DateTime(2026, 10, 28, 9, 5, 0), CultureInfo.GetCultureInfo("tr-TR"));

        string filled = TextTemplate.Fill("{1}-{Ad} tur {tur} {tarih} {saat} {yok} {", values);

        Assert.Equal("K7-Ahmet tur 3 28.10.2026 09:05 {yok} {", filled);
    }

    [Fact]
    public void Fill_WithoutAList_LeavesColumnNumbersAsTheyAre()
    {
        var values = new TemplateValues(null, [], 1, DateTime.Now, CultureInfo.InvariantCulture);

        Assert.Equal("{1} kalır", TextTemplate.Fill("{1} kalır", values));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, string text, Encoding encoding)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, text, encoding);
        return path;
    }
}

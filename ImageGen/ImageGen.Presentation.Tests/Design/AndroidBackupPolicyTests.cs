using System.Xml.Linq;

namespace ImageGen.Presentation.Tests.Design;

/// <summary>
/// Checks the Android backup policy: the app opts out of Auto Backup, and both rules files deny every documented
/// domain. The rules are a deny-all defense in depth next to <c>allowBackup=false</c>: the Android documentation
/// does not promise that an OEM transfer tool honours the manifest flag alone.
/// </summary>
public class AndroidBackupPolicyTests
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

    /// <summary>Every domain of the documented backup schema, including the device-protected ones.</summary>
    private static readonly string[] AllDomains =
    [
        "root",
        "file",
        "database",
        "sharedpref",
        "external",
        "device_root",
        "device_file",
        "device_database",
        "device_sharedpref",
    ];

    [Fact]
    public void Manifest_OptsOutOfBackup()
    {
        var application = LoadManifest().Root!.Element("application");

        Assert.NotNull(application);
        Assert.Equal("false", AndroidAttribute(application!, "allowBackup")?.Value);
        Assert.Equal("@xml/backup_rules", AndroidAttribute(application!, "fullBackupContent")?.Value);
        Assert.Equal("@xml/data_extraction_rules", AndroidAttribute(application!, "dataExtractionRules")?.Value);
    }

    [Fact]
    public void BackupRules_DenyEveryDomain()
    {
        var rules = LoadRules("backup_rules.xml");

        Assert.Empty(rules.Elements("include"));
        AssertDeniesEveryDomain(rules);
    }

    [Theory]
    [InlineData("cloud-backup")]
    [InlineData("device-transfer")]
    public void DataExtractionRules_DenyEveryDomain(string section)
    {
        var rules = LoadRules("data_extraction_rules.xml");

        var element = rules.Element(section);
        Assert.NotNull(element);
        Assert.Empty(element!.Elements("include"));
        AssertDeniesEveryDomain(element);
    }

    private static void AssertDeniesEveryDomain(XElement parent)
    {
        var excludes = parent.Elements("exclude").ToList();

        Assert.Equal(AllDomains.Length, excludes.Count);
        var domains = excludes.Select(exclude => exclude.Attribute("domain")?.Value)
            .OrderBy(domain => domain, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(AllDomains.OrderBy(domain => domain, StringComparer.Ordinal), domains);
        foreach (var exclude in excludes)
        {
            Assert.Equal(".", exclude.Attribute("path")?.Value);
        }
    }

    private static XAttribute? AndroidAttribute(XElement element, string name)
    {
        return element.Attribute(XName.Get(name, AndroidNamespace));
    }

    private static XDocument LoadManifest()
    {
        return XDocument.Load(Path.Combine(AndroidRoot(), "Platforms", "Android", "AndroidManifest.xml"));
    }

    private static XElement LoadRules(string name)
    {
        var path = Path.Combine(AndroidRoot(), "Platforms", "Android", "Resources", "xml", name);
        Assert.True(File.Exists(path), $"Не найден файл правил бэкапа {name}");
        return XDocument.Load(path).Root!;
    }

    private static string AndroidRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "design", "tokens.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "ImageGen", "ImageGen.Maui");
    }
}

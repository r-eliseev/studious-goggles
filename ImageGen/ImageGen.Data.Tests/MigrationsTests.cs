using System.Runtime.CompilerServices;

namespace ImageGen.Data.Tests;

public class MigrationsTests
{
    [Fact]
    public void Load_UnorderedFiles_SortsByNumber()
    {
        var migrations = Migrations.Load(
        [
            ("0002_b.sql", "B"),
            ("0001_a.sql", "A"),
        ]);

        Assert.Equal(["0001_a.sql", "0002_b.sql"], migrations.Select(m => m.Name));
        Assert.Equal([1, 2], migrations.Select(m => m.Number));
    }

    [Fact]
    public void Load_NumberGap_ThrowsNamingMissingNumber()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001_a.sql", "A"),
            ("0003_c.sql", "C"),
        ]));

        Assert.Contains("0002", exception.Message);
    }

    [Fact]
    public void Load_DuplicateNumber_ThrowsNamingBothFiles()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001_a.sql", "A"),
            ("0001_b.sql", "B"),
        ]));

        Assert.Contains("0001_a.sql", exception.Message);
        Assert.Contains("0001_b.sql", exception.Message);
    }

    [Fact]
    public void Load_NameWithoutFourDigits_ThrowsNamingFile()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("1_a.sql", "A"),
        ]));

        Assert.Contains("1_a.sql", exception.Message);
    }

    [Fact]
    public void Load_NameWithUpperCaseOrDash_ThrowsInvalidName()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Migrations.Load(
        [
            ("0001-A.sql", "A"),
        ]));

        Assert.Contains("0001-A.sql", exception.Message);
    }

    [Fact]
    public void Load_EmptySet_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Migrations.Load([]));
    }

    [Fact]
    public void Load_Script_IsReturnedUnchanged()
    {
        const string script = "CREATE TABLE t (id INTEGER);\n-- комментарий\r\n";

        var migrations = Migrations.Load([("0001_a.sql", script)]);

        Assert.Equal(script, migrations[0].Script);
    }

    [Fact]
    public void All_EmbeddedScripts_ContainInitialWithNumberOne()
    {
        var initial = Assert.Single(Migrations.All, m => m.Name == "0001_initial.sql");

        Assert.Equal(1, initial.Number);
    }

    [Fact]
    public void All_Count_EqualsNumberOfSqlFilesInProject()
    {
        var migrationsFolder = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(ThisFilePath())!, "..", "ImageGen.Data", "Migrations"));

        var sqlFiles = Directory.GetFiles(migrationsFolder, "*.sql");

        Assert.Equal(sqlFiles.Length, Migrations.All.Count);
    }

    private static string ThisFilePath([CallerFilePath] string path = "") => path;
}

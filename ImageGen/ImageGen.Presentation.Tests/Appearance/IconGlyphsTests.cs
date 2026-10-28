using System.Reflection;
using ImageGen.Presentation.Appearance;

namespace ImageGen.Presentation.Tests.Appearance;

public class IconGlyphsTests
{
    private static IEnumerable<string> Glyphs() => typeof(IconGlyphs)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Glyphs_AreSingleCharactersInPrivateUseArea()
    {
        foreach (var glyph in Glyphs())
        {
            Assert.Single(glyph);
            Assert.InRange(glyph[0], '', '');
        }
    }

    [Fact]
    public void Glyphs_AreUnique()
    {
        var glyphs = Glyphs().ToList();

        Assert.Equal(glyphs.Count, glyphs.Distinct().Count());
    }

    [Fact]
    public void Plus_IsLucidePlus()
    {
        Assert.Equal("", IconGlyphs.Plus);
    }

    [Fact]
    public void AllIconsFromDesignTableArePresent()
    {
        // Rows of the table in docs/design/implementation.md, section 5.
        Assert.Equal(30, Glyphs().Count());
    }
}

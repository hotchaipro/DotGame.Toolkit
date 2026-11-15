// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.ContentPipeline.Testing;
using DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Fonts;

/// <summary>
/// The font reader on synthetic fonts, table by table, and on the sample's real one.
/// </summary>
public sealed class FontFileTests
{
    private static FontFile Load(byte[] font)
    {
        using var directory = new TestDirectory();
        string path = directory.Path_("font.ttf");
        File.WriteAllBytes(path, font);
        return FontFile.Load(path);
    }

    private static FontFile Load(SfntBuilder builder) => Load(builder.Build());

    [Theory]
    [InlineData("wOFF")]
    [InlineData("wOF2")]
    public void Load_RefusesWebFonts_WithAnActionableMessage(string tag)
    {
        var error = Assert.Throws<NotSupportedException>(() => Load([.. System.Text.Encoding.ASCII.GetBytes(tag), 0, 0, 0, 0]));

        Assert.Contains("TTF or OTF", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RefusesATruncatedFile()
    {
        byte[] font = new SfntBuilder().Build();

        Assert.Throws<InvalidDataException>(() => Load(font[..20]));
    }

    [Theory]
    [InlineData("head")]
    [InlineData("hhea")]
    public void Load_RefusesAFontWithoutItsRequiredTables(string tag)
    {
        Assert.Throws<InvalidDataException>(() => Load(new SfntBuilder().Without(tag)));
    }

    [Fact]
    public void Load_ReadsTheFirstFontOfACollection()
    {
        using var directory = new TestDirectory();
        string path = directory.Path_("font.ttc");
        byte[] collection = new SfntBuilder().Head(2048).BuildCollection();

        // Offsets in a collection are from the file's start, as this one's are
        File.WriteAllBytes(path, collection);

        Assert.Equal(2048, FontFile.Load(path).UnitsPerEm);
    }

    [Fact]
    public void Metrics_ComeFromHhea_ByDefault()
    {
        FontFile font = Load(new SfntBuilder().Head(2048).Hhea(1900, -500, 90).Os2(typoAscender: 1000, typoDescender: -1, typoLineGap: 1));

        Assert.Equal(2048, font.UnitsPerEm);
        Assert.Equal(1.0 / 2048, font.EmScale);
        Assert.Equal(1900, font.Ascender);
        Assert.Equal(-500, font.Descender);
        Assert.Equal(1900 + 500 + 90, font.LineHeight);
    }

    [Fact]
    public void Metrics_ComeFromOs2Typo_WhenTheFontSaysToUseThem()
    {
        FontFile font = Load(new SfntBuilder().Hhea(800, -200, 0).Os2(typoAscender: 750, typoDescender: -250, typoLineGap: 50, fsSelection: 128));

        Assert.Equal(750, font.Ascender);
        Assert.Equal(-250, font.Descender);
        Assert.Equal(750 + 250 + 50, font.LineHeight);
    }

    [Fact]
    public void Metrics_FallBackToOs2Typo_WhenHheasAreZero()
    {
        FontFile font = Load(new SfntBuilder().Hhea(0, 0, 0).Os2(typoAscender: 700, typoDescender: -300, typoLineGap: 10));

        Assert.Equal(700, font.Ascender);
        Assert.Equal(-300, font.Descender);
        Assert.Equal(1010, font.LineHeight);
    }

    [Fact]
    public void Metrics_FallBackToWin_WhenHheaAndTypoAreZero()
    {
        FontFile font = Load(new SfntBuilder().Hhea(0, 0, 0).Os2(winAscent: 900, winDescent: 100));

        Assert.Equal(900, font.Ascender);
        Assert.Equal(-100, font.Descender);
        Assert.Equal(1000, font.LineHeight);
    }

    [Fact]
    public void Underline_IsTheCenterOfThePostTablesEdge()
    {
        FontFile font = Load(new SfntBuilder().Post(-100, 50));

        Assert.Equal(-125, font.UnderlinePosition);
        Assert.Equal(50, font.UnderlineThickness);
    }

    [Fact]
    public void Underline_IsZeroWithNoPostTable()
    {
        Assert.Equal(0, Load(new SfntBuilder()).UnderlineThickness);
    }

    [Fact]
    public void Name_JoinsFamilyAndStyle()
    {
        FontFile font = Load(new SfntBuilder().Name((3, 1, 0x409, 1, "Hot Sans"), (3, 1, 0x409, 2, "Bold")));

        Assert.Equal("Hot Sans Bold", font.Name);
    }

    [Fact]
    public void Name_PrefersTheTypographicFamily()
    {
        FontFile font = Load(new SfntBuilder().Name((3, 1, 0x409, 1, "Hot Sans Light"), (3, 1, 0x409, 2, "Regular"), (3, 1, 0x409, 16, "Hot Sans"), (3, 1, 0x409, 17, "Light")));

        Assert.Equal("Hot Sans Light", font.Name);
    }

    [Fact]
    public void Name_PrefersEnglishWindowsRecords()
    {
        FontFile font = Load(new SfntBuilder().Name((3, 1, 0x407, 1, "Heiss"), (3, 1, 0x409, 1, "Hot")));

        Assert.Equal("Hot", font.Name);
    }

    [Fact]
    public void Name_FallsBackToMacintoshThenUnicodeRecords()
    {
        Assert.Equal("Mac Name", Load(new SfntBuilder().Name((1, 0, 0, 1, "Mac Name"))).Name);
        Assert.Equal("Uni Name", Load(new SfntBuilder().Name((0, 3, 0, 1, "Uni Name"))).Name);
    }

    [Fact]
    public void Name_ReplacesNonAsciiWithAQuestionMark()
    {
        Assert.Equal("Caf?", Load(new SfntBuilder().Name((3, 1, 0x409, 1, "Café"))).Name);
    }

    [Fact]
    public void Name_IsEmptyWithNoNameTable()
    {
        Assert.Equal("", Load(new SfntBuilder()).Name);
    }

    [Fact]
    public void GlyphIndex_IsZeroWithNoCmap()
    {
        Assert.Equal(0, Load(new SfntBuilder()).GetGlyphIndex('A'));
    }

    [Fact]
    public void GlyphIndex_ReadsFormat0()
    {
        FontFile font = Load(new SfntBuilder().Cmap((0, 3, SfntBuilder.CmapFormat0(('A', 7)))));

        Assert.Equal(7, font.GetGlyphIndex('A'));
        Assert.Equal(0, font.GetGlyphIndex('B'));
        Assert.Equal(0, font.GetGlyphIndex(300));
    }

    [Fact]
    public void GlyphIndex_ReadsFormat4Segments()
    {
        FontFile font = Load(new SfntBuilder().Cmap((3, 1, SfntBuilder.CmapFormat4(('A', 'C', 5 - 'A'), ('a', 'b', 20 - 'a')))));

        Assert.Equal(5, font.GetGlyphIndex('A'));
        Assert.Equal(7, font.GetGlyphIndex('C'));
        Assert.Equal(21, font.GetGlyphIndex('b'));
        Assert.Equal(0, font.GetGlyphIndex('D'));
        Assert.Equal(0, font.GetGlyphIndex('c'));
        Assert.Equal(0, font.GetGlyphIndex(0x1F3AE));
    }

    [Fact]
    public void GlyphIndex_ReadsFormat6()
    {
        FontFile font = Load(new SfntBuilder().Cmap((0, 3, SfntBuilder.CmapFormat6('A', 9, 8))));

        Assert.Equal(9, font.GetGlyphIndex('A'));
        Assert.Equal(8, font.GetGlyphIndex('B'));
        Assert.Equal(0, font.GetGlyphIndex('C'));
        Assert.Equal(0, font.GetGlyphIndex('@'));
    }

    [Fact]
    public void GlyphIndex_ReadsFormat12_BeyondTheBasicPlane()
    {
        FontFile font = Load(new SfntBuilder().Cmap((3, 10, SfntBuilder.CmapFormat12(12, ('A', 'Z', 1), (0x1F3AE, 0x1F3AF, 100)))));

        Assert.Equal(26, font.GetGlyphIndex('Z'));
        Assert.Equal(101, font.GetGlyphIndex(0x1F3AF));
        Assert.Equal(0, font.GetGlyphIndex(0x1F3B0));
    }

    [Fact]
    public void GlyphIndex_ReadsFormat13_ManyToOne()
    {
        FontFile font = Load(new SfntBuilder().Cmap((0, 6, SfntBuilder.CmapFormat12(13, ('A', 'Z', 3)))));

        Assert.Equal(3, font.GetGlyphIndex('A'));
        Assert.Equal(3, font.GetGlyphIndex('Z'));
    }

    [Fact]
    public void GlyphIndex_Prefers32BitSubtables_AsFreeTypeDoes()
    {
        FontFile font = Load(new SfntBuilder().Cmap(
            (3, 1, SfntBuilder.CmapFormat4(('A', 'A', 1 - 'A'))),
            (3, 10, SfntBuilder.CmapFormat12(12, ('A', 'A', 2)))));

        Assert.Equal(2, font.GetGlyphIndex('A'));
    }

    [Fact]
    public void GlyphIndex_IgnoresNonUnicodeSubtables()
    {
        Assert.Equal(0, Load(new SfntBuilder().Cmap((1, 0, SfntBuilder.CmapFormat0(('A', 7))))).GetGlyphIndex('A'));
    }

    [Fact]
    public void GlyphIndex_ReadsASymbolCmap()
    {
        FontFile font = Load(new SfntBuilder().Cmap((3, 0, SfntBuilder.CmapFormat4((0xF041, 0xF041, 7 - 0xF041)))));

        Assert.Equal(7, font.GetGlyphIndex(0xF041));
    }

    [Fact]
    public void AdvanceWidth_ComesFromHmtx_AndRepeatsTheLastMetric()
    {
        FontFile font = Load(new SfntBuilder().Hhea(800, -200, 0, metricCount: 3).Hmtx(500, 600, 700));

        Assert.Equal(600, font.GetAdvanceWidth(1));
        Assert.Equal(700, font.GetAdvanceWidth(50));
    }

    [Fact]
    public void AdvanceWidth_IsZeroWithNoHmtx()
    {
        Assert.Equal(0, Load(new SfntBuilder()).GetAdvanceWidth(1));
    }

    [Fact]
    public void CapHeight_ComesFromOs2_FromVersionTwo()
    {
        Assert.Equal(0.7, Load(new SfntBuilder().Os2(capHeight: 700)).ReadCapHeight());
    }

    [Fact]
    public void CapHeight_IsNull_WhenTheFontDoesntSay()
    {
        Assert.Null(Load(new SfntBuilder().Os2(capHeight: 700, version: 1)).ReadCapHeight());
        Assert.Null(Load(new SfntBuilder()).ReadCapHeight());
    }

    [Fact]
    public void CapHeight_FallsBackToTheTopOfH()
    {
        var loca = new byte[8];
        loca[3] = 0;
        loca[5] = 0;
        var glyf = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteInt16BigEndian(glyf.AsSpan(8), 650);
        FontFile font = Load(new SfntBuilder()
            .Cmap((0, 3, SfntBuilder.CmapFormat0(('H', 1))))
            .Table("loca", loca)
            .Table("glyf", glyf));

        Assert.Equal(0.65, font.ReadCapHeight());
    }

    [Fact]
    public void RealFont_ReadsAsExpected()
    {
        FontFile font = FontFile.Load(Path.Combine(ToolkitPaths.Root, "sample", "Content", "Fonts", "Inter-Medium.ttf"));

        Assert.Equal("Inter Medium", font.Name);
        Assert.Equal(2048, font.UnitsPerEm);
        Assert.True(font.Ascender > 0);
        Assert.True(font.Descender < 0);
        Assert.True(font.LineHeight >= font.Ascender - font.Descender);
        Assert.NotEqual(0, font.GetGlyphIndex('A'));
        Assert.NotEqual(font.GetGlyphIndex('A'), font.GetGlyphIndex('B'));
        Assert.True(font.GetAdvanceWidth(font.GetGlyphIndex('W')) > font.GetAdvanceWidth(font.GetGlyphIndex('i')));
        Assert.InRange(font.ReadCapHeight() ?? 0, 0.5, 0.9);
    }
}

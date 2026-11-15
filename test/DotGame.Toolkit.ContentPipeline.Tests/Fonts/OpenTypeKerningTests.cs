// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.ContentPipeline.Testing;
using DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Fonts;

public sealed class OpenTypeKerningTests
{
    private const int PairPosLookup = 2;
    private const int ExtensionLookup = 9;
    private const int Horizontal = 1;

    // Glyph 1 is 'A', glyph 2 'V', glyph 3 'o', glyph 4 both 'x' and 'y'
    private static List<GlyphGenGlyph> Glyphs() =>
    [
        new GlyphGenGlyph { CodePoint = 'A', GlyphIndex = 1 },
        new GlyphGenGlyph { CodePoint = 'V', GlyphIndex = 2 },
        new GlyphGenGlyph { CodePoint = 'o', GlyphIndex = 3 },
    ];

    private static Dictionary<(int, int), float> Kerning(SfntBuilder builder, List<GlyphGenGlyph>? glyphs = null, double scale = 1)
    {
        using var directory = new TestDirectory();
        string path = directory.Path_("font.ttf");
        File.WriteAllBytes(path, builder.Build());
        return OpenTypeKerning.Read(FontFile.Load(path), glyphs ?? Glyphs(), scale).ToDictionary(pair => (pair.First, pair.Second), pair => pair.Adjust);
    }

    private static byte[] AvPair(int value = -50) => SfntBuilder.PairPosFormat1([(1, [(2, value)])]);

    [Fact]
    public void Read_FindsNothingInAFontWithNoKerning()
    {
        Assert.Empty(Kerning(new SfntBuilder()));
    }

    [Fact]
    public void Read_ReadsPairPosFormat1_AndScalesIt()
    {
        Dictionary<(int, int), float> pairs = Kerning(new SfntBuilder().Gpos((PairPosLookup, [AvPair()])), scale: 0.01);

        Assert.Equal(-0.5f, pairs[('A', 'V')]);
        Assert.Single(pairs);
    }

    [Fact]
    public void Read_TakesOnlyXAdvanceFromAValueRecord()
    {
        // Value format 5: XPlacement (a fixed 7 in the builder) and XAdvance
        byte[] subTable = SfntBuilder.PairPosFormat1([(1, [(2, -40)])], valueFormat1: 5);

        Assert.Equal(-40f, Kerning(new SfntBuilder().Gpos((PairPosLookup, [subTable])))[('A', 'V')]);
    }

    [Fact]
    public void Read_SkipsPairsOfGlyphsNotInTheFont()
    {
        byte[] subTable = SfntBuilder.PairPosFormat1([(1, [(2, -50), (99, -70)]), (98, [(2, -60)])]);

        Assert.Equal([('A', 'V')], Kerning(new SfntBuilder().Gpos((PairPosLookup, [subTable]))).Keys);
    }

    [Fact]
    public void Read_ReadsPairPosFormat2Classes()
    {
        // First: A in class 1. Second: V and o in classes 1 and 2.
        byte[] subTable = SfntBuilder.PairPosFormat2(
            [1],
            [(1, 1)],
            [(2, 1), (3, 2)],
            new int[,] { { 0, 0, 0 }, { 0, -30, -10 } });

        Dictionary<(int, int), float> pairs = Kerning(new SfntBuilder().Gpos((PairPosLookup, [subTable])));

        Assert.Equal(-30f, pairs[('A', 'V')]);
        Assert.Equal(-10f, pairs[('A', 'o')]);
        Assert.False(pairs.ContainsKey(('V', 'A')), "V isn't covered by the subtable.");
    }

    [Fact]
    public void Read_FollowsExtensionLookups()
    {
        byte[] extension = SfntBuilder.Extension(PairPosLookup, AvPair(-25));

        Assert.Equal(-25f, Kerning(new SfntBuilder().Gpos((ExtensionLookup, [extension])))[('A', 'V')]);
    }

    [Fact]
    public void Read_WithinALookup_LetsTheFirstSubtableWin()
    {
        // In the real rule a pair covered by a subtable stops there, even at no adjustment
        Assert.Equal(-50f, Kerning(new SfntBuilder().Gpos((PairPosLookup, [AvPair(-50), AvPair(-999)])))[('A', 'V')]);
    }

    [Fact]
    public void Read_AcrossLookups_AddsThem()
    {
        Assert.Equal(-30f, Kerning(new SfntBuilder().Gpos((PairPosLookup, [AvPair(-10)]), (PairPosLookup, [AvPair(-20)])))[('A', 'V')]);
    }

    [Fact]
    public void Read_IgnoresOtherGposFeatures_AndUsesTheKernTable()
    {
        SfntBuilder font = new SfntBuilder()
            .GposWithFeature("mark", (PairPosLookup, [AvPair(-50)]))
            .Kern((Horizontal, [(1, 2, -15)]));

        Assert.Equal(-15f, Kerning(font)[('A', 'V')]);
    }

    [Fact]
    public void Read_PrefersGposToTheKernTable()
    {
        SfntBuilder font = new SfntBuilder()
            .Gpos((PairPosLookup, [AvPair(-50)]))
            .Kern((Horizontal, [(1, 2, -15)]));

        Assert.Equal(-50f, Kerning(font)[('A', 'V')]);
    }

    [Fact]
    public void Read_AddsKernSubtables_UnlessOneReplaces()
    {
        const int Override = 8;
        Assert.Equal(-30f, Kerning(new SfntBuilder().Kern((Horizontal, [(1, 2, -10)]), (Horizontal, [(1, 2, -20)])))[('A', 'V')]);
        Assert.Equal(-20f, Kerning(new SfntBuilder().Kern((Horizontal, [(1, 2, -10)]), (Horizontal | Override, [(1, 2, -20)])))[('A', 'V')]);
    }

    [Fact]
    public void Read_IgnoresVerticalAndCrossStreamKernSubtables()
    {
        const int CrossStream = 4;

        Assert.Empty(Kerning(new SfntBuilder().Kern((0, [(1, 2, -10)]), (Horizontal | CrossStream, [(1, 2, -10)]))));
    }

    [Fact]
    public void Read_DropsAZeroAdjustment()
    {
        Assert.Empty(Kerning(new SfntBuilder().Kern((Horizontal, [(1, 2, 0)]))));
    }

    [Fact]
    public void Read_GivesEveryCodePointOfAGlyphItsPairs()
    {
        var glyphs = new List<GlyphGenGlyph>
        {
            new() { CodePoint = 'A', GlyphIndex = 1 },
            new() { CodePoint = 'Á', GlyphIndex = 1 },
            new() { CodePoint = 'V', GlyphIndex = 2 },
        };

        Dictionary<(int, int), float> pairs = Kerning(new SfntBuilder().Gpos((PairPosLookup, [AvPair()])), glyphs);

        Assert.Equal(-50f, pairs[('A', 'V')]);
        Assert.Equal(-50f, pairs[('Á', 'V')]);
    }

    [Fact]
    public void Read_OfTheSamplesRealFont_IsPlausible()
    {
        string path = Path.Combine(ToolkitPaths.Root, "sample", "Content", "Fonts", "Inter-Medium.ttf");
        FontFile font = FontFile.Load(path);
        var glyphs = new List<GlyphGenGlyph>();
        foreach (char c in "AVTo")
        {
            glyphs.Add(new GlyphGenGlyph { CodePoint = c, GlyphIndex = font.GetGlyphIndex(c) });
        }

        List<KerningPair> pairs = OpenTypeKerning.Read(font, glyphs, font.EmScale);

        KerningPair av = Assert.Single(pairs, pair => pair.First == 'A' && pair.Second == 'V');
        Assert.InRange(av.Adjust, -0.2f, -0.001f);
    }
}

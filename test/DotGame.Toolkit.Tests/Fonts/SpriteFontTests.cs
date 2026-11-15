// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text;
using DotGame.Toolkit.Fonts;
using Xunit;

namespace DotGame.Toolkit.Tests.Fonts;

public sealed class SpriteFontTests
{
    // Measuring

    [Fact]
    public void MeasureString_AddsTheAdvances()
    {
        var font = new TestFonts().Glyphs("AB", advance: 10).Glyph('W', advance: 25).Build();

        var size = font.MeasureString("ABW");

        Assert.Equal(45, size.X);
        Assert.Equal(TestFonts.LineHeight, size.Y);
    }

    [Fact]
    public void MeasureString_EmptyOrNull_IsOneEmptyLine()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var emptyLine = new System.Numerics.Vector2(0, font.LineHeight);

        Assert.Equal(emptyLine, font.MeasureString(string.Empty));
        Assert.Equal(emptyLine, font.MeasureString((string)null!));
        Assert.Equal(emptyLine, font.MeasureString(ReadOnlySpan<char>.Empty));
        Assert.Equal(emptyLine, font.MeasureString(new System.Text.StringBuilder()));
    }

    [Fact]
    public void MeasureString_Lines_WidestLineAndEveryLinesHeight()
    {
        var font = new TestFonts().Glyphs("ABC", advance: 10).Build();

        var size = font.MeasureString("A\nABC\nAB");

        Assert.Equal(30, size.X);
        Assert.Equal(TestFonts.LineHeight * 3, size.Y);
    }

    [Fact]
    public void MeasureString_TrailingLineFeed_AddsAnEmptyLine()
    {
        var font = new TestFonts().Glyphs("A").Build();

        Assert.Equal(TestFonts.LineHeight * 2, font.MeasureString("A\n").Y);
    }

    [Fact]
    public void MeasureString_Tracking_IsAddedAfterEachCharacter()
    {
        var font = new TestFonts().Glyphs("ABC", advance: 10).Build();

        Assert.Equal(36, font.MeasureString("ABC", tracking: 2).X);
        Assert.Equal(27, font.MeasureString("ABC", tracking: -1).X);
    }

    [Fact]
    public void MeasureString_CodePoint_IsOneCharactersAdvance()
    {
        var font = new TestFonts().Glyph('A', advance: 12).Build();

        var size = font.MeasureString('A', tracking: 3);

        Assert.Equal(15, size.X);
        Assert.Equal(TestFonts.LineHeight, size.Y);
    }

    [Fact]
    public void MeasureString_StringBuilder_MatchesTheString()
    {
        var font = new TestFonts().Glyphs("AV", advance: 10).Kerning('A', 'V', -3).Build();
        var builder = new StringBuilder();
        for (int i = 0; i < 200; i++)
        {
            builder.Append("AV");
        }

        Assert.Equal(font.MeasureString(builder.ToString()), font.MeasureString(builder));
    }

    [Fact]
    public void MeasureString_SupplementaryCharacter_IsOneGlyph()
    {
        var font = new TestFonts().Glyph(0x1F600, advance: 30).Glyphs("A", advance: 10).Build();

        Assert.Equal(40, font.MeasureString("A\U0001F600").X);
    }

    // Kerning

    [Fact]
    public void Kerning_AdjustsTheAdvanceOfThePair()
    {
        var font = new TestFonts().Glyphs("AVT", advance: 10).Kerning('A', 'V', -3).Build();

        Assert.True(font.HasKerning);
        Assert.Equal(-3, font.GetKerning('A', 'V'));
        Assert.Equal(0, font.GetKerning('V', 'A'));
        Assert.Equal(17, font.MeasureString("AV").X);
        Assert.Equal(20, font.MeasureString("VA").X);
    }

    [Fact]
    public void Kerning_NoneAcrossLines()
    {
        var font = new TestFonts().Glyphs("AV", advance: 10).Kerning('A', 'V', -3).Build();

        Assert.Equal(10, font.MeasureString("A\nV").X);
    }

    [Fact]
    public void Kerning_FontWithoutPairs()
    {
        var font = new TestFonts().Glyphs("AV").Build();

        Assert.False(font.HasKerning);
        Assert.Equal(0, font.GetKerning('A', 'V'));
    }

    // Characters without a glyph

    [Fact]
    public void DefaultCharacter_PrefersTheReplacementCharacter()
    {
        var withBoth = new TestFonts().Glyphs("A?").Glyph(0xFFFD).Build();
        var withQuestionMark = new TestFonts().Glyphs("A?").Build();
        var withNeither = new TestFonts().Glyphs("A").Build();

        Assert.Equal(0xFFFD, withBoth.DefaultCharacter);
        Assert.Equal('?', withQuestionMark.DefaultCharacter);
        Assert.Null(withNeither.DefaultCharacter);
    }

    [Fact]
    public void MissingCharacter_TakesTheDefaultCharactersSpace()
    {
        var font = new TestFonts().Glyphs("A", advance: 10).Glyph('?', advance: 7).Build();

        Assert.Equal(17, font.MeasureString("Aé").X);
    }

    [Fact]
    public void MissingCharacter_WithoutDefault_TakesNoSpace()
    {
        var font = new TestFonts().Glyphs("A?", advance: 10).Build();
        font.DefaultCharacter = null;

        Assert.Equal(10, font.MeasureString("Aé").X);
    }

    [Fact]
    public void DefaultCharacter_WithoutAGlyph_Throws()
    {
        var font = new TestFonts().Glyphs("A?").Build();

        Assert.Throws<ArgumentException>(() => font.DefaultCharacter = 'B');
        Assert.Equal('?', font.DefaultCharacter);
    }

    [Theory]
    [InlineData('\r')]
    [InlineData('\t')]
    [InlineData('\0')]
    public void ControlCharacter_TakesNoSpace(
        char control)
    {
        var font = new TestFonts().Glyphs("A?", advance: 10).Build();

        Assert.Equal(20, font.MeasureString($"A{control}A").X);
    }

    [Theory]
    [InlineData(0xFE0F)] // Variation selector 16 (emoji presentation), as in "©️"
    [InlineData(0xFE0E)] // Variation selector 15 (text presentation)
    [InlineData(0x200D)] // Zero-width joiner
    [InlineData(0x200B)] // Zero-width space
    [InlineData(0x200E)] // Left-to-right mark
    [InlineData(0x2060)] // Word joiner
    [InlineData(0xFEFF)] // Zero-width no-break space (byte order mark)
    [InlineData(0x00AD)] // Soft hyphen
    [InlineData(0xE0100)] // Variation selector 17 (supplementary)
    public void InvisibleFormattingCharacter_TakesNoSpace(
        int codePoint)
    {
        var font = new TestFonts().Glyphs("A?", advance: 10).Build();

        Assert.Equal(20, font.MeasureString("A" + char.ConvertFromUtf32(codePoint) + "A").X);
    }

    [Fact]
    public void CopyrightWithEmojiPresentation_MeasuresAsThePlainSign()
    {
        var font = new TestFonts().Glyphs("© 2025?", advance: 10).Build();

        Assert.Equal(font.MeasureString("© 2025"), font.MeasureString("©️ 2025"));
    }

    [Fact]
    public void InvisibleFormattingCharacter_KeepsTheKerningAcrossIt()
    {
        var font = new TestFonts().Glyphs("AV?", advance: 10).Kerning('A', 'V', -3).Build();

        Assert.Equal(17, font.MeasureString("A️V").X);
    }

    [Fact]
    public void InvisibleFormattingCharacter_WithAGlyph_UsesIt()
    {
        var font = new TestFonts().Glyphs("A?", advance: 10).Glyph(0x00AD, advance: 6).Build();

        Assert.Equal(26, font.MeasureString("A­A").X);
    }

    // Glyphs

    [Fact]
    public void GetCharacterRegion_IsTheGlyphOrNull()
    {
        var font = new TestFonts().Glyphs("A?").Glyph(0x00E9, advance: 11).Build();

        var glyph = TestGlyphs.Of(font, 0x00E9);

        Assert.NotNull(glyph);
        Assert.Equal(0x00E9, glyph.CodePoint);
        Assert.Equal(11, glyph.Advance);

        // The default character is for drawing, not for looking up a glyph
        Assert.Null(TestGlyphs.Of(font, 'B'));
    }

    [Fact]
    public void Characters_AreTheCodePointsWithGlyphs()
    {
        var font = new TestFonts().Glyphs("AB").Glyph(0x1F600).Build();

        Assert.Equal(new[] { 'A', 'B', 0x1F600 }.OrderBy(c => c), font.Characters.OrderBy(c => c));
    }

    [Fact]
    public void Glyph_OnAMissingPage_Throws()
    {
        var builder = new TestFonts().Glyph('A', page: 1);

        Assert.Throws<InvalidDataException>(() => builder.Build());
    }

    [Fact]
    public void Glyph_OnASecondPage()
    {
        var font = new TestFonts().Pages(2).Glyph('A', page: 0).Glyph('B', page: 1).Build();

        Assert.Equal(2, font.Pages.Count);
        Assert.Same(font.Pages[1], TestGlyphs.Of(font, 'B')!.Texture);
    }

    // Metrics and format

    [Fact]
    public void Metrics_ComeFromTheFontData()
    {
        var font = new TestFonts().Glyphs("A").Build();

        Assert.Equal(TestFonts.EmSize, font.EmSize);
        Assert.Equal(TestFonts.LineHeight, font.LineHeight);
        Assert.Equal(TestFonts.Ascender, font.Ascender);
        Assert.Equal(TestFonts.Descender, font.Descender);
    }

    [Fact]
    public void BitmapFont_HasNoDistanceRange()
    {
        var bitmap = new TestFonts().Glyphs("A").Format(GlyphFormat.Bitmap).Build();
        var msdf = new TestFonts().Glyphs("A").Format(GlyphFormat.Msdf).Build();

        Assert.Null(bitmap.DistanceRange);
        Assert.Equal(0, bitmap.DistanceRangeMiddle);
        Assert.Equal(4, msdf.DistanceRange);
        Assert.Equal(0.5f, msdf.DistanceRangeMiddle);
    }
}

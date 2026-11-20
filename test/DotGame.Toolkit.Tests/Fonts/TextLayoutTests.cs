// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Fonts;
using Xunit;

namespace DotGame.Toolkit.Tests.Fonts;

public sealed class TextLayoutTests
{
    // Every character is 10 wide, with the line height of the test font
    private const string Characters = "abcdefghijklmnopqrstuvwxyz -.…世界。、「」あっー한글　";

    private static TextLayout NewLayout(
        float maxWidth = float.PositiveInfinity,
        int maxLines = 0)
    {
        var font = new TestFonts().Glyphs(Characters, advance: 10).Build();
        return new TextLayout(font) { MaxWidth = maxWidth, MaxLines = maxLines };
    }

    private static string[] Lines(
        TextLayout layout,
        string text)
    {
        layout.Text = text;
        return layout.Lines
            .Select(l => string.Concat(text.AsSpan(l.Start, l.Length), l.HasEllipsis ? "…" : string.Empty))
            .ToArray();
    }

    [Fact]
    public void WithNoWidth_OnlyLineFeedsBreak()
    {
        Assert.Equal(["one two", "three"], Lines(NewLayout(), "one two\nthree"));
    }

    [Fact]
    public void AnEmptyLineStays()
    {
        Assert.Equal(["a", string.Empty, "b"], Lines(NewLayout(), "a\n\nb"));
    }

    [Fact]
    public void EmptyTextIsOneEmptyLine()
    {
        var layout = NewLayout();
        Assert.Equal([string.Empty], Lines(layout, string.Empty));
        Assert.Equal(0, layout.Size.X);
        Assert.Equal(TestFonts.LineHeight, layout.Size.Y);
    }

    [Fact]
    public void LinesBreakAtTheLastSpaceThatFits()
    {
        Assert.Equal(["one two", "three"], Lines(NewLayout(maxWidth: 80), "one two three"));
    }

    [Fact]
    public void AWordThatEndsExactlyAtTheWidthStaysOnTheLine()
    {
        Assert.Equal(["one two", "x"], Lines(NewLayout(maxWidth: 70), "one two x"));
    }

    [Fact]
    public void SpacesAtABreakAreDropped()
    {
        Assert.Equal(["one", "two"], Lines(NewLayout(maxWidth: 40), "one    two"));
    }

    [Fact]
    public void AWordWiderThanTheWidthBreaksBetweenCharacters()
    {
        Assert.Equal(["abcd", "efgh", "ij"], Lines(NewLayout(maxWidth: 40), "abcdefghij"));
    }

    [Fact]
    public void AWidthNarrowerThanACharacterStillAdvances()
    {
        Assert.Equal(["a", "b"], Lines(NewLayout(maxWidth: 1), "ab"));
    }

    [Fact]
    public void TextPastTheLimitEndsInAnEllipsisThatFits()
    {
        var layout = NewLayout(maxWidth: 60, maxLines: 2);

        Assert.Equal(["one", "two…"], Lines(layout, "one two three four"));
        Assert.True(layout.Lines[1].Width <= 60);
    }

    [Fact]
    public void TheEllipsisReplacesWhatItNeedsRoomFor()
    {
        Assert.Equal(["abcde…"], Lines(NewLayout(maxWidth: 60, maxLines: 1), "abcdefghij"));
    }

    [Fact]
    public void AFontWithNoEllipsisGlyphUsesThreeDots()
    {
        var font = new TestFonts().Glyphs("abc.", advance: 10).Build();

        Assert.Equal("...", new TextLayout(font).Ellipsis);
    }

    [Fact]
    public void AFontWithAnEllipsisGlyphUsesIt()
    {
        Assert.Equal("…", NewLayout().Ellipsis);
    }

    [Fact]
    public void ExactlyTheLimitHasNoEllipsis()
    {
        Assert.Equal(["one", "two"], Lines(NewLayout(maxWidth: 40, maxLines: 2), "one two"));
    }

    [Fact]
    public void ALineFeedPastTheLimitCountsAsMoreText()
    {
        Assert.Equal(["one…"], Lines(NewLayout(maxLines: 1), "one\ntwo"));
    }

    [Fact]
    public void SizeIsTheWidestLineAndTheLinesHeight()
    {
        var layout = NewLayout(maxWidth: 80);
        layout.LineSpacing = 1.5f;
        layout.Text = "one two three";

        Assert.Equal(70, layout.Size.X);
        Assert.Equal((TestFonts.LineHeight * 1.5f) + TestFonts.LineHeight, layout.Size.Y);
    }

    [Fact]
    public void ScaleAppliesToWidthsAndTheHeight()
    {
        var layout = NewLayout(maxWidth: 80);
        layout.Scale = 2;
        layout.Text = "abcd ef";

        Assert.Equal(["abcd", "ef"], Lines(layout, "abcd ef"));
        Assert.Equal(80, layout.Size.X);
        Assert.Equal(TestFonts.LineHeight * 4, layout.Size.Y);
    }

    [Fact]
    public void UpdatingAgainReplacesTheLines()
    {
        var layout = NewLayout();
        layout.Text = "a\nb\nc";
        layout.Text = "d";

        Assert.Single(layout.Lines);
    }

    // CJK, in escapes: world is 2 characters, and each is 10 wide

    private const string Sekai = "\u4E16\u754C";

    [Fact]
    public void Cjk_WithNoSpaces_BreaksBetweenCharacters()
    {
        Assert.Equal([Sekai + "\u4E16", "\u754C" + Sekai], Lines(NewLayout(maxWidth: 30), Sekai + Sekai + Sekai));
    }

    [Fact]
    public void Cjk_AfterASpace_StaysOnTheLine_RatherThanMovingWhole()
    {
        Assert.Equal(["abc " + Sekai, Sekai + Sekai], Lines(NewLayout(maxWidth: 60), "abc " + Sekai + Sekai + Sekai));
    }

    [Fact]
    public void Latin_AfterCjk_BreaksBeforeTheWord_NotInsideIt()
    {
        Assert.Equal([Sekai, "abcd"], Lines(NewLayout(maxWidth: 40), Sekai + "abcd"));
    }

    [Theory]
    [InlineData("\u3002")]
    [InlineData("\u3001")]
    [InlineData("\u300D")]
    [InlineData("\u3063")]
    [InlineData("\u30FC")]
    [InlineData(".")]
    public void Cjk_ClosingPunctuation_DoesNotStartALine_ButTakesACharacterWithIt(
        string closing)
    {
        Assert.Equal([Sekai + Sekai, "\u4E16" + closing], Lines(NewLayout(maxWidth: 50), Sekai + Sekai + "\u4E16" + closing));
    }

    [Fact]
    public void Cjk_OpeningPunctuation_DoesNotEndALine()
    {
        Assert.Equal([Sekai + Sekai, "\u300C\u4E16"], Lines(NewLayout(maxWidth: 50), Sekai + Sekai + "\u300C\u4E16"));
    }

    [Fact]
    public void Korean_BreaksAtItsSpaces()
    {
        Assert.Equal(["\uD55C\uAE00", "\uD55C\uAE00"], Lines(NewLayout(maxWidth: 30), "\uD55C\uAE00 \uD55C\uAE00"));
    }

    [Fact]
    public void Korean_WithoutAFit_BreaksInsideTheWord()
    {
        Assert.Equal(["\uD55C\uAE00\uD55C", "\uAE00\uD55C"], Lines(NewLayout(maxWidth: 30), "\uD55C\uAE00\uD55C\uAE00\uD55C"));
    }

    [Fact]
    public void AnIdeographicSpace_BreaksLikeASpace_AndIsNotShown()
    {
        Assert.Equal(["ab", "cd"], Lines(NewLayout(maxWidth: 30), "ab\u3000cd"));
    }

    [Fact]
    public void AHyphen_CanEndALine()
    {
        Assert.Equal(["ab-", "cd"], Lines(NewLayout(maxWidth: 40), "ab-cd"));
    }

    [Fact]
    public void AZeroWidthSpace_MarksABreak()
    {
        Assert.Equal(["ab\u200B", "cd"], Lines(NewLayout(maxWidth: 30), "ab\u200Bcd"));
    }

    // Drawing

    [Fact]
    public void Draw_PutsEveryLinesEffectsBehindEveryLinesGlyphs()
    {
        var layout = NewLayout();
        layout.Text = "ab\ncd";
        var batch = new RecordingBatch();
        var effects = new TextEffects { Underlays = [TextUnderlay.Outline(Color.Blue, 1)] };

        layout.Draw(batch.Batch, Vector2.Zero, Color.Red, effects: effects);

        // Four outlines, then four faces: the second line's outline doesn't cover the first's glyphs
        Assert.Equal(
            [Color.Blue, Color.Blue, Color.Blue, Color.Blue, Color.Red, Color.Red, Color.Red, Color.Red],
            batch.Calls.Select(c => c.Color));
    }

    [Fact]
    public void Draw_WithNoEffectsBehind_DrawsEachLineOnce()
    {
        var layout = NewLayout();
        layout.Text = "ab\ncd";
        var batch = new RecordingBatch();

        layout.Draw(batch.Batch, Vector2.Zero, Color.Red);

        Assert.Equal(4, batch.Calls.Count);
    }
}

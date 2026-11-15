// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using System.Text;
using DotGame.Framework;
using DotGame.Toolkit.Fonts;
using Xunit;

namespace DotGame.Toolkit.Tests.Fonts;

/// <summary>
/// What drawing text queues: which glyphs, where, in which layer and color.
/// </summary>
public sealed class SpriteFontDrawTests
{
    // Where a glyph's quad starts relative to the pen at the top of its line, from TestFonts:
    // its Left, and the font's baseline plus its Top
    private const float GlyphLeft = 1;
    private const float GlyphTop = TestFonts.Ascender - 10;

    [Fact]
    public void Text_DrawsWithTheFontsShader_AndTheBatchGoesBack()
    {
        var batch = new RecordingBatch();
        var font = new TestFonts().Glyphs("A").Build();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White);

        Assert.Same(font.Shader, Assert.Single(batch.Calls).Shader);
        Assert.Null(batch.Shader);
    }

    [Fact]
    public void ABitmapFont_DrawsWithItsShaderToo()
    {
        var batch = new RecordingBatch();
        var font = new TestFonts().Format(GlyphFormat.Bitmap).Glyphs("A").Build();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White);

        Assert.Same(font.Shader, Assert.Single(batch.Calls).Shader);
    }

    [Fact]
    public void Text_InABatchUsingAnotherShader_StillDrawsWithTheFonts_AndGoesBackToIt()
    {
        var batch = new RecordingBatch();
        var other = TestShaders.Create();
        var font = new TestFonts().Glyphs("A").Build();

        using (batch.Batch.UseShader(other))
        {
            batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White);

            Assert.Same(font.Shader, Assert.Single(batch.Calls).Shader);
            Assert.Same(other, batch.Shader);
        }
    }

    [Fact]
    public void AFontsShader_CanBeReplaced_ButNotWithNone()
    {
        var batch = new RecordingBatch();
        var custom = TestShaders.Create();
        var font = new TestFonts().Glyphs("A").Build();

        font.Shader = custom;
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White);

        Assert.Same(custom, Assert.Single(batch.Calls).Shader);
        Assert.Throws<ArgumentNullException>(() => font.Shader = null!);
    }

    private static List<DrawCall> Draw(
        SpriteFont font,
        string text,
        Vector2 position = default,
        float tracking = 0)
    {
        var batch = new RecordingBatch();
        batch.Batch.DrawString(font, text, position, Color.White, tracking: tracking);
        return batch.Calls;
    }

    // Each glyph's pen position along its line
    private static float[] Pens(
        List<DrawCall> calls)
    {
        return calls.Select(c => c.Left - GlyphLeft).ToArray();
    }

    private static StringBuilder Builder(
        params string[] chunks)
    {
        // Capacity 1 makes each append after the first start a new chunk, or more than one
        var builder = new StringBuilder(1);
        foreach (var chunk in chunks)
        {
            builder.Append(chunk);
        }

        return builder;
    }

    // Placing glyphs

    [Fact]
    public void Glyphs_AdvanceAlongTheLine_WithTheirMetricsAndSources()
    {
        var font = new TestFonts().Glyph('A', advance: 12).Glyph('B', advance: 7).Build();

        var calls = Draw(font, "AB", new Vector2(100, 200));

        Assert.Equal(2, calls.Count);
        Assert.Equal(100 + GlyphLeft, calls[0].Left);
        Assert.Equal(200 + GlyphTop, calls[0].Top);
        Assert.Equal(100 + 12 + GlyphLeft, calls[1].Left);
        Assert.Equal(new Rectangle(0, 0, 8, 12), calls[0].Source);
        Assert.Equal(new Rectangle(16, 0, 8, 12), calls[1].Source);
    }

    [Fact]
    public void Glyphs_ShareThePosition_SoTheTextRotatesAndScalesAsOne()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var batch = new RecordingBatch();
        var position = new Vector2(100, 200);
        var rotation = Angle.FromDegrees(45);

        batch.Batch.DrawString(font, "AB", position, Color.White, rotation: rotation, origin: new Vector2(3, 4), scale: new Vector2(2, 2));

        Assert.All(batch.Calls, c => Assert.Equal(position, c.Position));
        Assert.All(batch.Calls, c => Assert.Equal(rotation, c.Rotation));
        Assert.All(batch.Calls, c => Assert.Equal(new Vector2(2, 2), c.Scale));
        Assert.Equal(new Vector2(3 - GlyphLeft, 4 - GlyphTop), batch.Calls[0].Origin);
        Assert.Equal(new Vector2(3 - GlyphLeft - 10, 4 - GlyphTop), batch.Calls[1].Origin);
    }

    [Fact]
    public void Lines_ReturnThePen_AndMoveDownALineHeight()
    {
        var font = new TestFonts().Glyphs("AB").Build();

        var calls = Draw(font, "AB\nA\n\nB");

        Assert.Equal(new float[] { 0, 10, 0, 0 }, Pens(calls));
        Assert.Equal(GlyphTop, calls[0].Top);
        Assert.Equal(GlyphTop + TestFonts.LineHeight, calls[2].Top);
        Assert.Equal(GlyphTop + (TestFonts.LineHeight * 3), calls[3].Top);
    }

    [Fact]
    public void CarriageReturn_DrawsNothing()
    {
        var font = new TestFonts().Glyphs("AB?").Build();

        var calls = Draw(font, "A\r\nB");

        Assert.Equal(new float[] { 0, 0 }, Pens(calls));
        Assert.Equal(GlyphTop + TestFonts.LineHeight, calls[1].Top);
    }

    [Fact]
    public void Tracking_IsAddedAfterEachCharacter()
    {
        var font = new TestFonts().Glyphs("ABC").Build();

        Assert.Equal(new float[] { 0, 12, 24 }, Pens(Draw(font, "ABC", tracking: 2)));
    }

    [Fact]
    public void Kerning_MovesTheNextGlyph()
    {
        var font = new TestFonts().Glyphs("AV").Kerning('A', 'V', -3).Build();

        Assert.Equal(new float[] { 0, 7 }, Pens(Draw(font, "AV")));
        Assert.Equal(new float[] { 0, 10 }, Pens(Draw(font, "VA")));
    }

    [Fact]
    public void Kerning_DoesNotCrossALineBreak()
    {
        var font = new TestFonts().Glyphs("AV").Kerning('A', 'V', -3).Build();

        Assert.Equal(new float[] { 0, 0 }, Pens(Draw(font, "A\nV")));
    }

    [Fact]
    public void Kerning_SpansAnInvisibleCharacter()
    {
        var font = new TestFonts().Glyphs("AV?").Kerning('A', 'V', -3).Build();

        Assert.Equal(new float[] { 0, 7 }, Pens(Draw(font, "A‍V")));
    }

    [Fact]
    public void Kerning_OfSupplementaryCharacters()
    {
        var font = new TestFonts().Glyph(0x1F600).Glyphs("A").Kerning(0x1F600, 'A', -4).Kerning('A', 0x1F600, 2).Build();

        Assert.Equal(-4, font.GetKerning(0x1F600, 'A'));
        Assert.Equal(new float[] { 0, 6, 18 }, Pens(Draw(font, "\U0001F600A\U0001F600")));
    }

    [Fact]
    public void AGlyphWithNoPixels_AdvancesWithoutDrawing()
    {
        var font = new TestFonts().Glyphs("AB").Glyph(' ', advance: 5, width: 0).Build();

        Assert.Equal(new float[] { 0, 15 }, Pens(Draw(font, "A B")));
    }

    [Fact]
    public void Drawing_MatchesMeasuring()
    {
        var font = new TestFonts().Glyphs("AVW").Glyph('M', advance: 17).Kerning('A', 'V', -3).Kerning('W', 'A', 1.5f).Build();
        const string Text = "AVWAM";
        const float Tracking = 0.5f;

        var calls = Draw(font, Text, tracking: Tracking);

        // The last glyph starts where the text's width less its own advance puts it
        float width = font.MeasureString(Text, Tracking).X;
        Assert.Equal(width - 17 - Tracking, Pens(calls)[^1], 0.001f);
    }

    [Fact]
    public void EmptyText_DrawsNothing()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, string.Empty, Vector2.Zero, Color.White);
        batch.Batch.DrawString(font, ReadOnlySpan<char>.Empty, Vector2.Zero, Color.White);
        batch.Batch.DrawString(font, new StringBuilder(), Vector2.Zero, Color.White);
        batch.Batch.DrawString(font, (StringBuilder)null!, Vector2.Zero, Color.White);

        Assert.Empty(batch.Calls);
    }

    [Fact]
    public void Draw_RejectsANullBatch()
    {
        var font = new TestFonts().Glyphs("A").Build();

        Assert.Throws<ArgumentNullException>(() => font.DrawString(null!, "A", Vector2.Zero, Color.White));
        Assert.Throws<ArgumentNullException>(() => font.DrawString(null!, new StringBuilder("A"), Vector2.Zero, Color.White));
    }

    [Fact]
    public void Draw_OutsideABatch_Throws()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var recording = new RecordingBatch();
        recording.Scope.Dispose();

        Assert.Throws<InvalidOperationException>(() => recording.Batch.DrawString(font, "A", Vector2.Zero, Color.White));
    }

    // Characters from fallback fonts, which the pipeline packs onto further pages

    [Fact]
    public void Glyphs_DrawFromTheirOwnPages_InTextOrder()
    {
        var font = new TestFonts().Pages(2).Glyph('A', page: 0).Glyph(0x3042, page: 1).Glyph(0x1F600, page: 1).Build();

        var calls = Draw(font, "あA\U0001F600A");

        Assert.Same(font.Pages[1], calls[0].Texture);
        Assert.Same(font.Pages[0], calls[1].Texture);
        Assert.Same(font.Pages[1], calls[2].Texture);
        Assert.Same(font.Pages[0], calls[3].Texture);
        Assert.Equal(new float[] { 0, 10, 20, 30 }, Pens(calls));
    }

    // Missing characters

    [Fact]
    public void MissingCharacter_DrawsTheDefaultCharacter()
    {
        var font = new TestFonts().Glyphs("A").Glyph('?', advance: 7).Build();
        var questionMark = TestGlyphs.Of(font, '?')!.SourceRectangle;

        var calls = Draw(font, "éA");

        Assert.Equal((Rectangle)questionMark, calls[0].Source);
        Assert.Equal(new float[] { 0, 7 }, Pens(calls));
    }

    [Fact]
    public void MissingCharacter_WithoutADefault_DrawsNothing_AndTakesNoSpace()
    {
        var font = new TestFonts().Glyphs("A?").Build();
        font.DefaultCharacter = null;

        Assert.Equal(new float[] { 0, 10 }, Pens(Draw(font, "AéA")));
    }

    [Fact]
    public void DefaultCharacter_CanBeAnyGlyph()
    {
        var font = new TestFonts().Glyphs("AB").Glyph('X', advance: 3).Build();
        font.DefaultCharacter = 'X';

        Assert.Equal(3, font.MeasureString("é").X);
        Assert.Equal((Rectangle)TestGlyphs.Of(font, 'X')!.SourceRectangle, Assert.Single(Draw(font, "é")).Source);
    }

    [Fact]
    public void DefaultCharacter_SubstitutedForKerning_KernsAsItself()
    {
        var font = new TestFonts().Glyphs("A?").Kerning('?', 'A', -2).Build();

        Assert.Equal(18, font.MeasureString("éA").X);
        Assert.Equal(new float[] { 0, 8 }, Pens(Draw(font, "éA")));
    }

    // The ASCII table and the dictionary agree on either side of the boundary between them

    [Theory]
    [InlineData(0x20)]
    [InlineData(0x7E)]
    [InlineData(0xA0)]
    [InlineData(0xFFFF)]
    [InlineData(0x10000)]
    public void Glyph_IsFound_WhereverItsLookedUp(
        int codePoint)
    {
        var font = new TestFonts().Glyph(codePoint, advance: 9).Build();

        Assert.Equal(9, font.MeasureString(codePoint).X);
        Assert.Equal(9, font.MeasureString(char.ConvertFromUtf32(codePoint)).X);
        Assert.Single(Draw(font, char.ConvertFromUtf32(codePoint)));
    }

    // Colors and layers

    [Fact]
    public void Color_IsFadedByOpacity()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red, opacity: 0.5f);

        Assert.Equal(Color.Red * 0.5f, Assert.Single(batch.Calls).Color);
    }

    private static readonly TextEffects Everything = new()
    {
        Shadow = new TextShadow(Color.Black, new Vector2(2, 3)),
        Glow = new TextGlow(Color.Yellow, Width: 1, Softness: 1),
        Outline = new TextOutline(Color.Blue, Width: 1),
    };

    [Fact]
    public void Effects_DrawEveryGlyphOfEachLayerInTurn()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red, effects: Everything);

        Assert.Equal(
            new[] { Color.Black, Color.Black, Color.Yellow, Color.Yellow, Color.Blue, Color.Blue, Color.Red, Color.Red },
            batch.Calls.Select(c => c.Color));
    }

    [Fact]
    public void Shadow_IsDrawnAtItsOffset_AndTheOthersInPlace()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", new Vector2(100, 200), Color.Red, effects: Everything);

        Assert.Equal(100 + GlyphLeft + 2, batch.Calls[0].Left);
        Assert.Equal(200 + GlyphTop + 3, batch.Calls[0].Top);
        Assert.Equal(100 + GlyphLeft, batch.Calls[3].Left);
        Assert.Equal(200 + GlyphTop, batch.Calls[3].Top);
    }

    [Fact]
    public void Effects_CarryTheirSettingsToTheShader()
    {
        var font = new TestFonts().Format(GlyphFormat.Mtsdf).DistanceRange(16).Glyphs("A").Build();
        var batch = new RecordingBatch();
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 2) };

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White, effects: effects);

        Assert.Equal(2, (float)batch.Calls[0].CustomData.Values.X, 0.01f);
        Assert.Equal(0, (float)batch.Calls[1].CustomData.Values.X, 0.01f);
    }

    [Fact]
    public void Opacity_FadesEffectsToo()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red, opacity: 0.5f, effects: effects);

        Assert.Equal(Color.Blue * 0.5f, batch.Calls[0].Color);
        Assert.Equal(Color.Red * 0.5f, batch.Calls[1].Color);
    }

    [Fact]
    public void Layers_SelectWhatsDrawn()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red, effects: Everything, layers: TextLayers.Effects);
        Assert.Equal(new[] { Color.Black, Color.Yellow, Color.Blue }, batch.Calls.Select(c => c.Color));

        batch.Calls.Clear();
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red, effects: Everything, layers: TextLayers.Face);
        Assert.Equal(Color.Red, Assert.Single(batch.Calls).Color);
    }

    [Fact]
    public void BitmapFont_DrawsOnlyItsFace_WithEffects()
    {
        var font = new TestFonts().Format(GlyphFormat.Bitmap).Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red, effects: Everything);

        var call = Assert.Single(batch.Calls);
        Assert.Equal(Color.Red, call.Color);
        Assert.Equal(default, call.CustomData);
    }

    // Character attributes

    private sealed class RecordingAttributes(Func<CharacterCursor, Color?>? color = null) : ICharacterAttributes
    {
        private CharacterCursor _cursor;

        public List<CharacterCursor> Cursors { get; } = [];

        public void MoveTo(
            in CharacterCursor cursor)
        {
            this._cursor = cursor;
            this.Cursors.Add(cursor);
        }

        public Color? Color => color?.Invoke(this._cursor);
    }

    [Fact]
    public void Attributes_AreToldEachGlyphsPosition_InCharsAndGlyphs()
    {
        var font = new TestFonts().Glyphs("AB").Glyph(0x1F600).Build();
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, "A\U0001F600B", attributes, CharacterCursor.Start, Vector2.Zero, Color.White);

        Assert.Equal([0, 1, 3], attributes.Cursors.Select(c => c.CharIndex));
        Assert.Equal([0, 1, 2], attributes.Cursors.Select(c => c.GlyphIndex));
    }

    [Fact]
    public void Attributes_CursorOffset_PositionsAPieceOfALongerText()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, "AB", attributes, new CharacterCursor(10, 7), Vector2.Zero, Color.White);

        Assert.Equal([10, 11], attributes.Cursors.Select(c => c.CharIndex));
        Assert.Equal([7, 8], attributes.Cursors.Select(c => c.GlyphIndex));
    }

    [Fact]
    public void Attributes_SkipWhatDrawsNothing_WithoutLosingTheirPlace()
    {
        var font = new TestFonts().Glyphs("AB").Glyph(' ', width: 0).Build();
        font.DefaultCharacter = null;
        var attributes = new RecordingAttributes();

        // A blank, a line feed and a missing character
        new RecordingBatch().Batch.DrawString(font, "A \néB", attributes, CharacterCursor.Start, Vector2.Zero, Color.White);

        Assert.Equal([0, 4], attributes.Cursors.Select(c => c.CharIndex));
    }

    [Fact]
    public void Attributes_ColorOnlyTheFace_AndOnlyWhereTheySaySo()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var batch = new RecordingBatch();
        var attributes = new RecordingAttributes(cursor => cursor.CharIndex == 1 ? Color.Green : null);
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        batch.Batch.DrawString(font, "AB", attributes, CharacterCursor.Start, Vector2.Zero, Color.Red, opacity: 0.5f, effects: effects);

        Assert.Equal(
            new[] { Color.Blue * 0.5f, Color.Blue * 0.5f, Color.Red * 0.5f, Color.Green * 0.5f },
            batch.Calls.Select(c => c.Color));
    }

    [Fact]
    public void Attributes_AreVisitedAgain_ForEachLayer_FromTheOffset()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var attributes = new RecordingAttributes();
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        new RecordingBatch().Batch.DrawString(font, "AB", attributes, new CharacterCursor(5, 5), Vector2.Zero, Color.White, effects: effects);

        Assert.Equal([5, 6, 5, 6], attributes.Cursors.Select(c => c.CharIndex));
    }

    [Fact]
    public void DefaultAttributes_LeaveTheColorAlone()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", default(DefaultCharacterAttributes), CharacterCursor.Start, Vector2.Zero, Color.Red);

        Assert.Equal(Color.Red, Assert.Single(batch.Calls).Color);
    }

    // StringBuilder text is drawn a chunk at a time

    [Fact]
    public void Builder_InManyChunks_DrawsLikeTheString()
    {
        var font = new TestFonts().Glyphs("AVB").Glyph(0x1F600).Kerning('A', 'V', -3).Build();
        var builder = Builder("A", "V", "\nB", "\U0001F600", "A", "V");
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, builder, new Vector2(1, 2), Color.Red);

        Assert.Equal(Draw(font, builder.ToString(), new Vector2(1, 2)).Select(c => (c.Origin, c.Source)), batch.Calls.Select(c => (c.Origin, c.Source)));
    }

    [Fact]
    public void Builder_CarriesKerningAcrossAChunkBoundary()
    {
        var font = new TestFonts().Glyphs("AV").Kerning('A', 'V', -3).Build();
        var builder = Builder("A", "V");
        Assert.True(CountChunks(builder) > 1);
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, builder, Vector2.Zero, Color.White);

        Assert.Equal(new float[] { 0, 7 }, Pens(batch.Calls));
        Assert.Equal(17, font.MeasureString(builder).X);
    }

    [Fact]
    public void Builder_CarriesAttributePositionsAcrossAChunkBoundary()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var builder = Builder("A", "B", "A");
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, builder, attributes, new CharacterCursor(2, 2), Vector2.Zero, Color.White);

        Assert.Equal([2, 3, 4], attributes.Cursors.Select(c => c.CharIndex));
    }

    [Fact]
    public void Builder_DrawsEachLayerFromTheStart()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var batch = new RecordingBatch();
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        batch.Batch.DrawString(font, Builder("A", "B"), Vector2.Zero, Color.Red, effects: effects);

        Assert.Equal(new float[] { 0, 10, 0, 10 }, Pens(batch.Calls));
    }

    private static int CountChunks(
        StringBuilder builder)
    {
        int count = 0;
        foreach (var chunk in builder.GetChunks())
        {
            count += 1;
        }

        return count;
    }

    // Finding T2: a surrogate pair split between two chunks

    private static StringBuilder BuilderSplittingAPair()
    {
        var builder = Builder("A", "\uD83D", "\uDE00", "A");

        // Check the premise: the high surrogate ends a chunk
        foreach (var chunk in builder.GetChunks())
        {
            if ((chunk.Length > 0) && (chunk.Span[^1] == '\uD83D'))
            {
                return builder;
            }
        }

        throw new InvalidOperationException("The builder's chunks don't split the pair.");
    }

    [Fact]
    public void Builder_PairSplitAcrossChunks_MeasuresAsOneCharacter()
    {
        var font = new TestFonts().Glyphs("A?", advance: 10).Glyph(0x1F600, advance: 30).Build();

        Assert.Equal(50, font.MeasureString(BuilderSplittingAPair()).X);
    }

    [Fact]
    public void Builder_PairSplitAcrossChunks_DrawsAsOneGlyph()
    {
        var font = new TestFonts().Glyphs("A?").Glyph(0x1F600, advance: 30).Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, BuilderSplittingAPair(), Vector2.Zero, Color.White);

        Assert.Equal(3, batch.Calls.Count);
        Assert.Equal((Rectangle)TestGlyphs.Of(font, 0x1F600)!.SourceRectangle, batch.Calls[1].Source);
        Assert.Equal(new float[] { 0, 10, 40 }, Pens(batch.Calls));
    }

    [Fact]
    public void Builder_PairSplitAcrossChunks_CountsAsOneGlyph()
    {
        var font = new TestFonts().Glyphs("A?").Glyph(0x1F600).Build();
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, BuilderSplittingAPair(), attributes, CharacterCursor.Start, Vector2.Zero, Color.White);

        Assert.Equal([0, 1, 3], attributes.Cursors.Select(c => c.CharIndex));
        Assert.Equal([0, 1, 2], attributes.Cursors.Select(c => c.GlyphIndex));
    }

    // Unpaired surrogates are invalid text, but a pair cut in two by Substring is an easy way to
    // get them: they take the default character's place

    [Fact]
    public void UnpairedSurrogate_IsADefaultCharacter()
    {
        var font = new TestFonts().Glyphs("A").Glyph('?', advance: 7).Build();

        // Not theory data, whose serialization can't carry an unpaired surrogate
        foreach (var text in new[] { "\uD83D", "\uDE00", "\uD83DA", "A\uDE00", "\uDE00\uD83D" })
        {
            int defaults = text.Count(char.IsSurrogate);
            int letters = text.Length - defaults;

            Assert.Equal((defaults * 7) + (letters * 10), font.MeasureString(text).X);
            Assert.Equal(text.Length, Draw(font, text).Count);
        }
    }

    [Fact]
    public void UnpairedSurrogate_CountsAsOneChar()
    {
        var font = new TestFonts().Glyphs("A?").Build();
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, "\uD83DA", attributes, CharacterCursor.Start, Vector2.Zero, Color.White);

        Assert.Equal([0, 1], attributes.Cursors.Select(c => c.CharIndex));
    }

    // Finding 8 from the review: every overload agrees on empty text
    [Fact]
    public void MeasureString_EmptyBuilder_AfterClearing_IsOneEmptyLine()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var builder = new StringBuilder("AAAA");
        builder.Clear();

        Assert.Equal(new Vector2(0, TestFonts.LineHeight), font.MeasureString(builder));
    }

    [Fact]
    public void MeasureString_RejectsANullBuilder()
    {
        var font = new TestFonts().Glyphs("A").Build();

        Assert.Throws<ArgumentNullException>(() => font.MeasureString((StringBuilder)null!));
    }

    [Fact]
    public void MeasureString_CodePoint_OfAMissingCharacter()
    {
        var withDefault = new TestFonts().Glyph('?', advance: 7).Build();
        var without = new TestFonts().Glyphs("A").Build();

        Assert.Equal(new Vector2(7, TestFonts.LineHeight), withDefault.MeasureString(0x00E9));
        Assert.Equal(new Vector2(0, TestFonts.LineHeight), without.MeasureString(0x00E9));
    }

    [Fact]
    public void MeasureString_AgreesForEveryKindOfText()
    {
        var font = new TestFonts().Glyphs("AV").Glyph(0x1F600, advance: 30).Kerning('A', 'V', -3).Build();
        const string Text = "AV\nVA\U0001F600\n";

        var expected = font.MeasureString(Text, 2);

        Assert.Equal(expected, font.MeasureString(Text.AsSpan(), 2));
        Assert.Equal(expected, font.MeasureString(new StringBuilder(Text), 2));
    }

    // What counts as drawing nothing

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x0001)]
    [InlineData(0x0009)]
    [InlineData(0x000D)]
    [InlineData(0x001F)]
    public void ControlCharacters_TakeNoSpace_AndDrawNothing(
        int codePoint)
    {
        var font = new TestFonts().Glyphs("A?").Build();
        string text = $"A{char.ConvertFromUtf32(codePoint)}A";

        Assert.Equal(20, font.MeasureString(text).X);
        Assert.Equal(2, Draw(font, text).Count);
    }

    [Theory]
    [InlineData(0x007F)]
    [InlineData(0x0080)]
    [InlineData(0x0085)]
    [InlineData(0x009F)]
    public void DeleteAndC1Controls_TakeNoSpace_AndDrawNothing(
        int codePoint)
    {
        var font = new TestFonts().Glyphs("A?").Build();
        string text = $"A{char.ConvertFromUtf32(codePoint)}A";

        Assert.Equal(20, font.MeasureString(text).X);
        Assert.Equal(2, Draw(font, text).Count);
    }

    [Theory]
    [InlineData(0x00AD)]
    [InlineData(0x034F)]
    [InlineData(0x061C)]
    [InlineData(0x115F)]
    [InlineData(0x1160)]
    [InlineData(0x17B4)]
    [InlineData(0x17B5)]
    [InlineData(0x180B)]
    [InlineData(0x180F)]
    [InlineData(0x200B)]
    [InlineData(0x200F)]
    [InlineData(0x202A)]
    [InlineData(0x202E)]
    [InlineData(0x2060)]
    [InlineData(0x206F)]
    [InlineData(0x3164)]
    [InlineData(0xFE00)]
    [InlineData(0xFE0F)]
    [InlineData(0xFEFF)]
    [InlineData(0xFFA0)]
    [InlineData(0xFFF0)]
    [InlineData(0xFFF8)]
    [InlineData(0x1BCA0)]
    [InlineData(0x1BCA3)]
    [InlineData(0x1D173)]
    [InlineData(0x1D17A)]
    [InlineData(0xE0000)]
    [InlineData(0xE0FFF)]
    public void DefaultIgnorableCharacters_DrawNothing(
        int codePoint)
    {
        var font = new TestFonts().Glyphs("A?").Build();
        string text = $"A{char.ConvertFromUtf32(codePoint)}A";

        Assert.Equal(20, font.MeasureString(text).X);
        Assert.Equal(2, Draw(font, text).Count);
    }

    [Theory]
    [InlineData(0x00AC)]
    [InlineData(0x00AE)]
    [InlineData(0x034E)]
    [InlineData(0x0350)]
    [InlineData(0x115E)]
    [InlineData(0x1161)]
    [InlineData(0x180A)]
    [InlineData(0x200A)]
    [InlineData(0x2010)]
    [InlineData(0x2029)]
    [InlineData(0x202F)]
    [InlineData(0x205F)]
    [InlineData(0x2070)]
    [InlineData(0xFDFF)]
    [InlineData(0xFE10)]
    [InlineData(0xFEFE)]
    [InlineData(0xFF00)]
    [InlineData(0xFFEF)]
    [InlineData(0xFFF9)]
    [InlineData(0x1BC9F)]
    [InlineData(0x1BCA4)]
    [InlineData(0x1D172)]
    [InlineData(0x1D17B)]
    [InlineData(0xDFFFF)]
    [InlineData(0xE1000)]
    public void TheirNeighbors_DrawTheDefaultCharacter(
        int codePoint)
    {
        var font = new TestFonts().Glyphs("A?").Build();
        string text = $"A{char.ConvertFromUtf32(codePoint)}A";

        Assert.Equal(30, font.MeasureString(text).X);
        Assert.Equal(3, Draw(font, text).Count);
    }
}

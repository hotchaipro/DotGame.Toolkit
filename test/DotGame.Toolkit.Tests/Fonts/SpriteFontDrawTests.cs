// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using System.Text;
using DotGame.Framework;
using DotGame.Framework.Graphics;
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

    // Attributes that move and turn every character the same way
    private sealed class MovedAttributes(Vector2 translation, Angle rotation) : ICharacterAttributes
    {
        public void MoveTo(
            in CharacterCursor cursor)
        {
        }

        public Vector2 Translation => translation;

        public Angle Rotation => rotation;
    }

    [Fact]
    public void Attributes_AreToldTheMiddleOfEachCharactersAdvance()
    {
        var font = new TestFonts().Glyphs("AB", advance: 10).Build();
        var attributes = new RecordingAttributes();

        new RecordingBatch().Batch.DrawString(font, "AB", attributes, CharacterCursor.Start, Vector2.Zero, Color.White);

        Assert.Equal([5f, 15f], attributes.Cursors.Select(c => c.X));
    }

    [Fact]
    public void ATranslation_MovesTheCharacter_InEveryLayerAlike()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var plain = new RecordingBatch();
        var moved = new RecordingBatch();
        var effects = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        plain.Batch.DrawString(font, "A", new Vector2(100, 200), Color.Red, effects: effects);
        moved.Batch.DrawString(font, "A", new MovedAttributes(new Vector2(3, -4), default), CharacterCursor.Start, new Vector2(100, 200), Color.Red, effects: effects);

        Assert.Equal(2, moved.Calls.Count);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(plain.Calls[i].Left + 3, moved.Calls[i].Left, 3);
            Assert.Equal(plain.Calls[i].Top - 4, moved.Calls[i].Top, 3);
            Assert.Equal(plain.Calls[i].Color, moved.Calls[i].Color);
        }
    }

    [Fact]
    public void ATranslation_IsInTheTextsOwnSpace_SoItScalesAndTurnsWithTheText()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var still = new RecordingBatch();
        var moved = new RecordingBatch();

        // Twice the size and a quarter turn: 3 along the text is 6 down the screen
        still.Batch.DrawString(font, "A", new MovedAttributes(new Vector2(0.001f, 0), default), CharacterCursor.Start, Vector2.Zero, Color.White,
            rotation: Angle.FromDegrees(90), scale: new Vector2(2));
        moved.Batch.DrawString(font, "A", new MovedAttributes(new Vector2(3.001f, 0), default), CharacterCursor.Start, Vector2.Zero, Color.White,
            rotation: Angle.FromDegrees(90), scale: new Vector2(2));

        var from = Assert.Single(still.Calls);
        var to = Assert.Single(moved.Calls);
        Assert.Equal(from.Position.X, to.Position.X, 2);
        Assert.Equal(from.Position.Y + 6, to.Position.Y, 2);
    }

    [Fact]
    public void ARotation_TurnsTheCharacter_AboutTheMiddleOfItsAdvanceOnTheBaseline()
    {
        var font = new TestFonts().Glyphs("A", advance: 10).Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", new MovedAttributes(Vector2.Zero, Angle.FromDegrees(30)), CharacterCursor.Start, new Vector2(100, 200), Color.White);

        var call = Assert.Single(batch.Calls);
        Assert.Equal(30, call.Rotation.Degrees, 3);

        // The origin of the sprite is that point of the glyph, and it is drawn where that point is
        Assert.Equal(new Vector2(5 - GlyphLeft, TestFonts.Ascender - GlyphTop), call.Origin);
        Assert.Equal(new Vector2(105, 200 + TestFonts.Ascender), call.Position);
    }

    // The gradient of the face

    private static readonly TextEffects TwoColors = new()
    {
        Gradient = new TextGradient(Color.Red, Color.Blue),
    };

    private static Vertex[] Corners(
        RecordingBatch batch)
    {
        return batch.Triangles.SelectMany(t => new[] { t.A, t.B, t.C }).ToArray();
    }

    [Fact]
    public void AGradient_DrawsTheFaceAsTriangles_InItsTwoColors_AndTheEffectsAsSprites()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();
        var effects = TwoColors with { Outline = new TextOutline(Color.Green, Width: 1) };

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White, effects: effects);

        // The outline is a sprite in its own color; the face is triangles
        Assert.Equal(Color.Green, Assert.Single(batch.Calls).Color);
        Assert.NotEmpty(batch.Triangles);

        // The rectangle of the glyph runs from 10 above the baseline to 2 below it. The gradient
        // runs down the ascent of the font (it has no cap height) to the baseline, so the
        // rectangle begins two thirds of the way down it and ends in the bottom color.
        var corners = Corners(batch);
        float top = corners.Min(c => c.Position.Y);
        float bottom = corners.Max(c => c.Position.Y);
        Assert.Equal(GlyphTop, top, 3);
        Assert.Equal(GlyphTop + 12, bottom, 3);

        var atTop = corners.First(c => c.Position.Y == top).Color;
        Assert.Equal(1f / 3, atTop.R, 3);
        Assert.Equal(2f / 3, atTop.B, 3);
        Assert.All(corners.Where(c => c.Position.Y >= TestFonts.Ascender), c => Assert.Equal(Color.Blue, c.Color));
    }

    [Fact]
    public void TheTrianglesOfAGradient_CoverTheRectangleOfTheGlyph_AndCarryTheShaderDataOfTheFace()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var plain = new RecordingBatch();
        var batch = new RecordingBatch();

        plain.Batch.DrawString(font, "A", new Vector2(100, 200), Color.White, effects: new TextEffects { Embolden = 0.5f });
        batch.Batch.DrawString(font, "A", new Vector2(100, 200), Color.White, effects: TwoColors with { Embolden = 0.5f });

        var sprite = Assert.Single(plain.Calls);
        var corners = Corners(batch);
        Assert.Equal(sprite.Left, corners.Min(c => c.Position.X), 3);
        Assert.Equal(sprite.Right, corners.Max(c => c.Position.X), 3);
        Assert.Equal(sprite.Top, corners.Min(c => c.Position.Y), 3);
        Assert.Equal(sprite.Bottom, corners.Max(c => c.Position.Y), 3);
        Assert.All(batch.Triangles, t => Assert.Equal(sprite.CustomData, t.CustomData));
        Assert.All(batch.Triangles, t => Assert.Same(sprite.Texture, t.Texture));
    }

    [Fact]
    public void AGradient_FadesWithTheOpacityOfTheText()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White, opacity: 0.5f, effects: TwoColors);

        Assert.All(Corners(batch), c => Assert.Equal(0.5f, c.Color.A, 3));

        // (Its colors fade as a sprite's would, and no further: they were faded twice)
        Assert.All(Corners(batch).Where(c => c.Position.Y >= TestFonts.Ascender), c => Assert.Equal(Color.Blue * 0.5f, c.Color));
    }

    [Fact]
    public void ACharacterWithAColorOfItsOwn_KeepsIt_InAGradient()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var batch = new RecordingBatch();
        var attributes = new RecordingAttributes(c => (c.GlyphIndex == 1) ? Color.Green : null);

        batch.Batch.DrawString(font, "AB", attributes, CharacterCursor.Start, Vector2.Zero, Color.White, effects: TwoColors);

        Assert.Equal(Color.Green, Assert.Single(batch.Calls).Color);
        Assert.NotEmpty(batch.Triangles);
    }

    [Fact]
    public void AGradient_MovesWithItsCharacter()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var plain = new RecordingBatch();
        var moved = new RecordingBatch();

        plain.Batch.DrawString(font, "A", Vector2.Zero, Color.White, effects: TwoColors);
        moved.Batch.DrawString(font, "A", new MovedAttributes(new Vector2(7, 9), default), CharacterCursor.Start, Vector2.Zero, Color.White, effects: TwoColors);

        Assert.Equal(plain.Triangles.Count, moved.Triangles.Count);
        for (int i = 0; i < plain.Triangles.Count; i++)
        {
            Assert.Equal(plain.Triangles[i].A.Position + new Vector2(7, 9), moved.Triangles[i].A.Position);
        }
    }

    // The band of light across the face
    //
    // The test glyph's rectangle is 8 wide from 1 along its advance of 10, and 12 high from 10
    // above the baseline to 2 below it.

    private static float Area(
        RecordingBatch batch)
    {
        return batch.Triangles.Sum(t =>
            MathF.Abs(((t.B.Position.X - t.A.Position.X) * (t.C.Position.Y - t.A.Position.Y))
                - ((t.C.Position.X - t.A.Position.X) * (t.B.Position.Y - t.A.Position.Y))) / 2);
    }

    [Theory]
    [InlineData(-1000f, 8f, 1f)]    // off the start of the line
    [InlineData(1000f, 8f, 1f)]     // off its end
    [InlineData(5f, 0f, 1f)]        // of no width
    [InlineData(5f, 8f, 0f)]        // of no strength
    [InlineData(float.NaN, 8f, 1f)]
    public void ASheenThatFallsOnNothing_DrawsTheTextAsItIsWithoutOne(
        float position,
        float width,
        float strength)
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var plain = new RecordingBatch();
        var batch = new RecordingBatch();
        var outline = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        plain.Batch.DrawString(font, "AB", new Vector2(100, 200), Color.Red, effects: outline);
        batch.Batch.DrawString(font, "AB", new Vector2(100, 200), Color.Red,
            effects: outline with { Sheen = new TextSheen(Color.White, position, width, Strength: strength) });

        Assert.Empty(batch.Triangles);
        Assert.Equal(plain.Calls, batch.Calls);
    }

    [Fact]
    public void ASheen_CutsOnlyTheCharactersItFallsOn_AndLeavesTheEffectsAsSprites()
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var plain = new RecordingBatch();
        var batch = new RecordingBatch();
        var outline = new TextEffects { Outline = new TextOutline(Color.Blue, Width: 1) };

        // The band is over the second character alone: 12 to 18 along the line
        plain.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red, effects: outline);
        batch.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red,
            effects: outline with { Sheen = new TextSheen(Color.White, 15, 6) });

        // Both outlines and the first face are the sprites they were; the second face is triangles
        Assert.Equal(plain.Calls.Take(3), batch.Calls);
        Assert.NotEmpty(batch.Triangles);
        Assert.All(batch.Triangles, t => Assert.Equal(plain.Calls[3].CustomData, t.CustomData));
        Assert.All(Corners(batch), c => Assert.InRange(c.Position.X, plain.Calls[3].Left - 0.001f, plain.Calls[3].Right + 0.001f));

        // (and the triangles cover the rectangle of the glyph, once)
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Fact]
    public void ASheen_IsItsColorAlongItsMiddle_AndFadesToTheTextsColorAtItsEdges()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        // Upright, 4 wide about 5 along the line: from 3 to 7 of a rectangle from 1 to 9
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 5, 4) });

        var corners = Corners(batch);
        Assert.Contains(corners, c => MathF.Abs(c.Position.X - 5) < 0.001f);
        Assert.All(corners.Where(c => MathF.Abs(c.Position.X - 5) < 0.001f), c => Assert.Equal(Color.White, c.Color));
        Assert.All(corners.Where(c => MathF.Abs(c.Position.X - 5) >= 2 - 0.001f), c => Assert.Equal(Color.Red, c.Color));
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Theory]
    [InlineData(0f)]    // only the band's falling half reaches the glyph, from before it
    [InlineData(0.5f)]
    [InlineData(9.5f)]  // only its rising half, from after it
    [InlineData(10f)]
    public void ASheenThatFallsOnPartOfAGlyph_StillCoversTheGlyphOnce(
        float position)
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        // 4 wide: at these places one edge of it is on the rectangle (1 to 9) and its middle isn't
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, position, 4) });

        Assert.NotEmpty(batch.Triangles);
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Fact]
    public void TheStrengthOfASheen_IsHowFarItsMiddleGoesTowardItsColor()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Black,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 5, 4, Strength: 0.5f) });

        var brightest = Corners(batch).Max(c => c.Color.R);
        Assert.Equal(0.5f, brightest, 3);
    }

    [Fact]
    public void ASlantedSheen_LeansToTheRightAsItRises()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        // Its middle crosses the baseline 3 along the line, and leans half a pixel for each it
        // rises (an angle of about 26.6 degrees from upright)
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 3, 2, Angle.FromRadians(MathF.Atan(0.5f))) });

        // Where its middle crosses the top and the bottom of the rectangle (10 above the
        // baseline, and 2 below it)
        var lit = Corners(batch).Where(c => c.Color == Color.White).ToArray();
        var top = lit.Where(c => MathF.Abs(c.Position.Y - GlyphTop) < 0.001f).ToArray();
        var bottom = lit.Where(c => MathF.Abs(c.Position.Y - (GlyphTop + 12)) < 0.001f).ToArray();
        Assert.NotEmpty(top);
        Assert.NotEmpty(bottom);
        Assert.All(top, c => Assert.Equal(8f, c.Position.X, 3));
        Assert.All(bottom, c => Assert.Equal(2f, c.Position.X, 3));
    }

    [Fact]
    public void TheAngleOfASheen_IsFromUpright_AndNegativeLeansLeft()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        // 45 degrees to the left: a pixel back for each it rises
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 8, 2, Angle.FromDegrees(-45)) });

        // Its middle is 8 along at the baseline and a pixel further back for each it rises, so
        // where it meets the rectangle's left edge (1 along) it is 7 above the baseline
        var lit = Corners(batch).Where(c => c.Color == Color.White).ToArray();
        Assert.Contains(lit, c => (MathF.Abs(c.Position.X - 1) < 0.001f) && (MathF.Abs(c.Position.Y - (TestFonts.Ascender - 7)) < 0.001f));
        Assert.All(lit, c => Assert.Equal(8f, c.Position.X + (TestFonts.Ascender - c.Position.Y), 3));
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Fact]
    public void TheSoftnessOfASheen_IsHowMuchOfItIsFadingEdge()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        // 6 wide about 5, half of it fading: at full strength from 3.5 to 6.5, gone by 2 and 8
        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 5, 6, Softness: 0.5f) });

        var corners = Corners(batch);
        Assert.Contains(corners, c => MathF.Abs(c.Position.X - 3.5f) < 0.001f);
        Assert.Contains(corners, c => MathF.Abs(c.Position.X - 6.5f) < 0.001f);
        Assert.All(corners.Where(c => MathF.Abs(c.Position.X - 5) <= 1.5f + 0.001f), c => Assert.Equal(Color.White, c.Color));
        Assert.All(corners.Where(c => MathF.Abs(c.Position.X - 5) >= 3 - 0.001f), c => Assert.Equal(Color.Red, c.Color));
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Fact]
    public void AHardEdgedSheen_StillCoversTheGlyphOnce()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.Red,
            effects: new TextEffects { Sheen = new TextSheen(Color.White, 5, 4, Softness: 0) });

        Assert.Equal(8f * 12f, Area(batch), 2);
        Assert.Contains(Corners(batch), c => c.Color.G > 0.999f);
    }

    [Fact]
    public void SheenAcross_PutsAnUprightBandJustOffTheStartAt0_AndJustOffTheEndAt1()
    {
        // Two characters, each advancing 10: a line 20 wide
        var font = new TestFonts().Glyphs("AB").Build();
        var band = new TextSheen(Color.White, 0, 6);

        Assert.Equal(-3f, font.SheenAcross("AB", 0, band).Position, 3);
        Assert.Equal(23f, font.SheenAcross("AB", 1, band).Position, 3);
        Assert.Equal(10f, font.SheenAcross("AB", 0.5f, band).Position, 3);

        // (and it keeps everything else about the band)
        Assert.Equal(band with { Position = 10 }, font.SheenAcross("AB", 0.5f, band));
    }

    [Theory]
    [InlineData(45f)]
    [InlineData(-45f)]
    public void SheenAcross_AllowsForTheLeanOfTheBand_SoNoLetterIsLitAtEitherEnd(
        float degrees)
    {
        var font = new TestFonts().Glyphs("AB").Build();
        var band = new TextSheen(Color.White, 0, 6, Angle.FromDegrees(degrees));
        var plain = new RecordingBatch();

        plain.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red);

        foreach (var progress in new[] { 0f, 1f })
        {
            var batch = new RecordingBatch();
            batch.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red, effects: new TextEffects { Sheen = font.SheenAcross("AB", progress, band) });

            // Nothing is cut: the band is off the letters
            Assert.Empty(batch.Triangles);
            Assert.Equal(plain.Calls, batch.Calls);
        }

        // and it is on them half way across
        var across = new RecordingBatch();
        across.Batch.DrawString(font, "AB", Vector2.Zero, Color.Red, effects: new TextEffects { Sheen = font.SheenAcross("AB", 0.5f, band) });
        Assert.NotEmpty(across.Triangles);
    }

    [Fact]
    public void ASheen_LiesOverAGradient_AndFadesWithTheOpacityOfTheText()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var batch = new RecordingBatch();

        batch.Batch.DrawString(font, "A", Vector2.Zero, Color.White, opacity: 0.5f,
            effects: TwoColors with { Sheen = new TextSheen(Color.White, 5, 4) });

        var corners = Corners(batch);
        Assert.All(corners, c => Assert.Equal(0.5f, c.Color.A, 3));

        // Away from the band the gradient is as it is without it: its bottom color from the
        // baseline down
        Assert.All(corners.Where(c => (MathF.Abs(c.Position.X - 5) >= 2 - 0.001f) && (c.Position.Y >= TestFonts.Ascender)),
            c => Assert.Equal(Color.Blue * 0.5f, c.Color));
        Assert.All(corners.Where(c => MathF.Abs(c.Position.X - 5) < 0.001f), c => Assert.Equal(Color.White * 0.5f, c.Color));
        Assert.Equal(8f * 12f, Area(batch), 2);
    }

    [Fact]
    public void ASheen_LiesOverACharactersOwnColor_AndMovesWithTheCharacter()
    {
        var font = new TestFonts().Glyphs("A").Build();
        var still = new RecordingBatch();
        var moved = new RecordingBatch();
        var effects = new TextEffects { Sheen = new TextSheen(Color.White, 5, 4) };

        still.Batch.DrawString(font, "A", new RecordingAttributes(_ => Color.Green), CharacterCursor.Start, Vector2.Zero, Color.Red, effects: effects);
        moved.Batch.DrawString(font, "A", new MovedAttributes(new Vector2(7, 9), default), CharacterCursor.Start, Vector2.Zero, Color.Red, effects: effects);

        Assert.Contains(Corners(still), c => c.Color == Color.Green);
        Assert.DoesNotContain(Corners(still), c => c.Color == Color.Red);

        Assert.Equal(still.Triangles.Count, moved.Triangles.Count);
        for (int i = 0; i < still.Triangles.Count; i++)
        {
            Assert.Equal(still.Triangles[i].A.Position + new Vector2(7, 9), moved.Triangles[i].A.Position);
        }
    }

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

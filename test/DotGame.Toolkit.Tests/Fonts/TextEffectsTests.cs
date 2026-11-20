// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Fonts;
using Xunit;

namespace DotGame.Toolkit.Tests.Fonts;

public sealed class TextEffectsTests
{
    // Half floats keep about three significant digits
    private const float Precision = 0.01f;

    private static SpriteFont Font(
        GlyphFormat format = GlyphFormat.Mtsdf,
        float range = 16,
        float middle = 0)
    {
        return new TestFonts().Format(format).DistanceRange(range, middle).Glyphs("AV").Build();
    }

    private static TextPass[] Passes(
        SpriteFont font,
        TextEffects? effects,
        TextLayers include = TextLayers.All)
    {
        var passes = new TextPass[TextPass.GetMaxCount(effects)];
        var count = font.GetPasses(effects, passes, include);
        return passes[..count];
    }

    private static void AssertData(
        ShaderCustomData data,
        float distanceOffset,
        float softness,
        float miterLimit,
        float signedRange)
    {
        Assert.Equal(distanceOffset, (float)data.Values.X, Precision);
        Assert.Equal(softness, (float)data.Values.Y, Precision);
        Assert.Equal(miterLimit, (float)data.Values.Z, Precision);
        Assert.Equal(signedRange, (float)data.Values.W, Precision);
    }

    private static readonly Color Brown = Color.FromRgb(0x902000);
    private static readonly Color Gold = Color.FromRgb(0xFFCC00);
    private static readonly Color Orange = Color.FromRgb(0xFF8000);
    private static readonly Color DarkRed = Color.FromRgb(0x800000);
    private static readonly Color OrangeRed = Color.FromRgb(0xFF4000);

    // Passes

    [Fact]
    public void NoEffects_DrawsOnlyTheFace()
    {
        var font = Font(GlyphFormat.Msdf, range: 4, middle: 0.5f);

        var passes = Passes(font, null);

        var face = Assert.Single(passes);
        Assert.True(face.IsFace);
        AssertData(face.Data, distanceOffset: 0.5f, softness: 0, miterLimit: 4, signedRange: 4);
        Assert.Equal(Passes(font, TextEffects.None)[0].Data, face.Data);
        Assert.Equal(Passes(font, new TextEffects())[0].Data, face.Data);
    }

    [Fact]
    public void Underlays_AreDrawnFarthestFirst_ThenTheFace()
    {
        var effects = new TextEffects
        {
            Underlays = [TextUnderlay.Outline(Color.Red, 2), TextUnderlay.Glow(Color.Yellow, 3, softness: 2), TextUnderlay.Shadow(Color.Black, new Vector2(2, 3))],
        };

        var passes = Passes(Font(), effects);

        Assert.Equal(4, passes.Length);
        Assert.Equal(Color.Black, passes[0].Color);
        Assert.Equal(new Vector2(2, 3), passes[0].Offset);
        Assert.Equal(Color.Yellow, passes[1].Color);
        Assert.Equal(Color.Red, passes[2].Color);
        Assert.Equal(Vector2.Zero, passes[2].Offset);
        Assert.Equal([false, false, false, true], passes.Select(l => l.IsFace));
    }

    // A title's: the line next to the face, the depth under it, a band in two colors, the depth under that
    private static readonly TextEffects Banner = new()
    {
        Underlays =
        [
            TextUnderlay.Outline(OrangeRed, 3, TextCorners.Round),
            TextUnderlay.Outline(DarkRed, 3) with { Offset = new Vector2(0, 2) },
            TextUnderlay.Outline(Gold, 6, TextCorners.Round) with { Gradient = new TextGradient(Color.Yellow, Orange) },
            TextUnderlay.Outline(Brown, 6, TextCorners.Round) with { Offset = new Vector2(0, 3) },
        ],
    };

    [Fact]
    public void Underlays_EachExtendFromTheEmboldenedFace_WithTheirOwnCornersOffsetsAndGradients()
    {
        var passes = Passes(Font(range: 32, middle: 0.5f), Banner with { Embolden = 1 });

        Assert.Equal(new[] { Brown, Gold, DarkRed, OrangeRed }, passes[..4].Select(p => p.Color));
        AssertData(passes[0].Data, distanceOffset: 0.5f + 1 + 6, softness: 0, miterLimit: 1, signedRange: -32);
        AssertData(passes[1].Data, distanceOffset: 0.5f + 1 + 6, softness: 0, miterLimit: 1, signedRange: -32);
        AssertData(passes[2].Data, distanceOffset: 0.5f + 1 + 3, softness: 0, miterLimit: 4, signedRange: -32);
        AssertData(passes[3].Data, distanceOffset: 0.5f + 1 + 3, softness: 0, miterLimit: 1, signedRange: -32);
        AssertData(passes[4].Data, distanceOffset: 0.5f + 1, softness: 0, miterLimit: 4, signedRange: -32);
        Assert.Equal(new Vector2(0, 3), passes[0].Offset);
        Assert.Equal(new Vector2(0, 2), passes[2].Offset);
        Assert.Null(passes[0].Gradient);
        Assert.Equal(new TextGradient(Color.Yellow, Orange), passes[1].Gradient);
    }

    // Layer selection

    [Fact]
    public void Layers_Underlays_DrawsEveryUnderlayButNotTheFace()
    {
        var passes = Passes(Font(range: 32), Banner, TextLayers.Underlays);

        Assert.Equal(new[] { Brown, Gold, DarkRed, OrangeRed }, passes.Select(l => l.Color));
        Assert.DoesNotContain(passes, l => l.IsFace);
    }

    [Fact]
    public void Layers_Face_DrawsOnlyTheFace()
    {
        var face = Assert.Single(Passes(Font(), Banner, TextLayers.Face));

        Assert.True(face.IsFace);
    }

    [Fact]
    public void Layers_Underlays_ThenFace_DrawTheSameAsAll()
    {
        var font = Font(range: 32);

        var split = Passes(font, Banner, TextLayers.Underlays).Concat(Passes(font, Banner, TextLayers.Face));

        Assert.Equal(Passes(font, Banner), split);
    }

    [Fact]
    public void Layers_Underlays_OfTextWithoutThem_DrawNothing()
    {
        Assert.Empty(Passes(Font(), null, TextLayers.Underlays));
        Assert.Empty(Passes(Font(), new TextEffects { Embolden = 1 }, TextLayers.Underlays));
        Assert.Empty(Passes(Font(GlyphFormat.Bitmap), Banner, TextLayers.Underlays));
        Assert.Empty(Passes(Font(), Banner, TextLayers.None));
    }

    // The usual underlays

    [Fact]
    public void Outline_IsTheGlyphsGrown_InPlace()
    {
        var outline = TextUnderlay.Outline(Color.Black, 3);

        Assert.Equal(new TextUnderlay(Color.Black, 3), outline);
        Assert.Equal(TextCorners.Sharp, outline.Corners);
        Assert.Equal(4, outline.MiterLimit);
        Assert.Equal(TextCorners.Round, TextUnderlay.Outline(Color.Black, 3, TextCorners.Round).Corners);
    }

    [Fact]
    public void Shadow_IsTheGlyphsMoved_SoftenedAndSpread()
    {
        var shadow = TextUnderlay.Shadow(Color.Black, new Vector2(2, 3), softness: 1, spread: 2);

        Assert.Equal(new Vector2(2, 3), shadow.Offset);
        Assert.Equal(1, shadow.Softness);
        Assert.Equal(2, shadow.Width);
        Assert.Equal(TextCorners.Sharp, shadow.Corners);
    }

    [Fact]
    public void Glow_IsRoundAndSoft()
    {
        var passes = Passes(Font(), new TextEffects { Underlays = [TextUnderlay.Glow(Color.White, width: 2, softness: 3)] });

        AssertData(passes[0].Data, distanceOffset: 2, softness: 3, miterLimit: 1, signedRange: -16);
    }

    // Per-quad settings

    [Fact]
    public void Underlay_ExtendsFromTheEmboldenedFace()
    {
        var font = Font(middle: 0.5f);
        var effects = new TextEffects { Embolden = 1, Underlays = [new TextUnderlay(Color.Black, 3) { MiterLimit = 2, Softness = 1 }] };

        var passes = Passes(font, effects);

        AssertData(passes[0].Data, distanceOffset: 0.5f + 1 + 3, softness: 1, miterLimit: 2, signedRange: -16);
        AssertData(passes[1].Data, distanceOffset: 0.5f + 1, softness: 0, miterLimit: 4, signedRange: -16);
    }

    [Fact]
    public void Underlay_RoundCorners_HaveAMiterLimitOfOne()
    {
        var passes = Passes(Font(), new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 2, TextCorners.Round) with { MiterLimit = 10 }] });

        Assert.Equal(1, (float)passes[0].Data.Values.Z, Precision);
    }

    [Fact]
    public void Underlay_UnlimitedMiter_StaysWithinHalfFloats()
    {
        var passes = Passes(Font(), new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 2) with { MiterLimit = float.PositiveInfinity }] });

        Assert.True(float.IsFinite((float)passes[0].Data.Values.Z));
        Assert.True((float)passes[0].Data.Values.Z >= 1000);
    }

    [Fact]
    public void Shadow_SpreadsFromTheFace()
    {
        var passes = Passes(Font(), new TextEffects { Embolden = -1, Underlays = [TextUnderlay.Shadow(Color.Black, Vector2.One, softness: 1, spread: 2)] });

        AssertData(passes[0].Data, distanceOffset: 1, softness: 1, miterLimit: 4, signedRange: -16);
    }

    // Formats

    [Theory]
    [InlineData((int)GlyphFormat.Msdf, 16)]
    [InlineData((int)GlyphFormat.Sdf, 16)]
    [InlineData((int)GlyphFormat.Mtsdf, -16)]
    public void SignedRange_SelectsHowTheShaderReadsDistances(
        int format,
        float signedRange)
    {
        var font = Font((GlyphFormat)format);

        Assert.Equal(signedRange, (float)Passes(font, null)[0].Data.Values.W, Precision);
    }

    [Fact]
    public void BitmapFont_DrawsOnlyTheFace_WithNoDistanceSettings()
    {
        var face = Assert.Single(Passes(Font(GlyphFormat.Bitmap), Banner));

        Assert.True(face.IsFace);
        Assert.Equal(default, face.Data);
    }

    // Distance range

    [Fact]
    public void Underlay_BeyondTheRange_IsClampedToIt()
    {
        // Half the range of 16, less a pixel for the anti-aliasing
        var passes = Passes(Font(range: 16), new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 10)] });

        Assert.Equal(7, (float)passes[0].Data.Values.X, Precision);
    }

    [Fact]
    public void Underlay_Softness_LeavesRoomForItsFade()
    {
        var passes = Passes(Font(range: 16), new TextEffects { Underlays = [TextUnderlay.Glow(Color.White, width: 10, softness: 4)] });

        Assert.Equal(5, (float)passes[0].Data.Values.X, Precision);
        Assert.Equal(4, (float)passes[0].Data.Values.Y, Precision);
    }

    [Fact]
    public void Underlay_WithinTheRange_IsUnchanged()
    {
        var passes = Passes(Font(range: 16), new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 6)] });

        Assert.Equal(6, (float)passes[0].Data.Values.X, Precision);
    }

    // Extent

    [Fact]
    public void GetExtent_None_IsZero()
    {
        Assert.Equal(Thickness.Zero, TextEffects.None.GetExtent());
    }

    [Fact]
    public void GetExtent_CoversTheWidestUnderlayOnEachSide()
    {
        var effects = new TextEffects
        {
            Embolden = 1,
            Underlays = [TextUnderlay.Outline(Color.Black, 2), TextUnderlay.Shadow(Color.Black, new Vector2(4, -1), softness: 2)],
        };

        var extent = effects.GetExtent();

        // The outline reaches 1 + 2 everywhere; the shadow reaches 1 + 1, moved right 4 and up 1
        Assert.Equal(3, extent.Left, Precision);
        Assert.Equal(3, extent.Top, Precision);
        Assert.Equal(6, extent.Right, Precision);
        Assert.Equal(3, extent.Bottom, Precision);
    }

    [Fact]
    public void GetExtent_CoversTheDepths_AtTheirOffsets()
    {
        var extent = Banner.GetExtent();

        // The widest reach 6 all round, and the deepest, 6 moved down 3, reaches 9 below
        Assert.Equal(6, extent.Left, Precision);
        Assert.Equal(6, extent.Top, Precision);
        Assert.Equal(6, extent.Right, Precision);
        Assert.Equal(9, extent.Bottom, Precision);
    }

    [Fact]
    public void HasUnderlays_OnlyWithSome()
    {
        Assert.True(Banner.HasUnderlays);
        Assert.False(new TextEffects { Underlays = [] }.HasUnderlays);
        Assert.False(new TextEffects { Embolden = 1, Gradient = new TextGradient(Color.Red, Color.Blue) }.HasUnderlays);
    }

    // Values

    [Fact]
    public void Effects_AreValues()
    {
        var a = new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 2)] };
        var b = new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 2)] };

        Assert.Equal(a, b);
        Assert.Null((a with { Underlays = null }).Underlays);
    }

    [Fact]
    public void Effects_WithTheSameUnderlays_AreEqual_WhetherOrNotTheyShareTheList()
    {
        TextUnderlay[] underlays = [TextUnderlay.Outline(Color.Black, 2) with { Offset = Vector2.One }, TextUnderlay.Outline(Color.Red, 1)];
        var a = new TextEffects { Underlays = underlays };
        var b = new TextEffects { Underlays = [TextUnderlay.Outline(Color.Black, 2) with { Offset = Vector2.One }, TextUnderlay.Outline(Color.Red, 1)] };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { Underlays = [TextUnderlay.Outline(Color.Red, 1), TextUnderlay.Outline(Color.Black, 2) with { Offset = Vector2.One }] });
        Assert.NotEqual(a, a with { Underlays = [TextUnderlay.Outline(Color.Black, 2), TextUnderlay.Outline(Color.Red, 1)] });
        Assert.Equal(new TextEffects(), new TextEffects { Underlays = [] });
        Assert.NotEqual(a, a with { Embolden = 1 });
        Assert.NotEqual(a, a with { Gradient = new TextGradient(Color.Red, Color.Blue) });
    }

    [Fact]
    public void Underlays_DifferInEverySetting()
    {
        var plain = TextUnderlay.Outline(Color.Black, 2);

        Assert.NotEqual(plain, plain with { Gradient = new TextGradient(Color.Red, Color.Blue) });
        Assert.NotEqual(plain, plain with { Offset = Vector2.One });
        Assert.NotEqual(plain, plain with { Softness = 1 });
        Assert.NotEqual(plain, plain with { Corners = TextCorners.Round });
        Assert.NotEqual(plain, plain with { MiterLimit = 2 });
        Assert.Equal(plain with { Gradient = new TextGradient(Color.Red, Color.Blue) }, plain with { Gradient = new TextGradient(Color.Red, Color.Blue) });
    }
}

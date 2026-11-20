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

    private static TextLayer[] Layers(
        SpriteFont font,
        TextEffects? effects,
        TextLayers include = TextLayers.All)
    {
        var layers = new TextLayer[TextLayer.GetMaxCount(effects)];
        var count = font.GetLayers(effects, layers, include);
        return layers[..count];
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

    // Layers

    [Fact]
    public void NoEffects_DrawsOnlyTheFace()
    {
        var font = Font(GlyphFormat.Msdf, range: 4, middle: 0.5f);

        var layers = Layers(font, null);

        var face = Assert.Single(layers);
        Assert.True(face.IsFace);
        AssertData(face.Data, distanceOffset: 0.5f, softness: 0, miterLimit: 4, signedRange: 4);
        Assert.Equal(Layers(font, TextEffects.None)[0].Data, face.Data);
    }

    [Fact]
    public void EveryEffect_DrawsShadowGlowOutlineThenFace()
    {
        var font = Font();
        var effects = new TextEffects
        {
            Shadow = new TextShadow(Color.Black, new Vector2(2, 3)),
            Glow = new TextGlow(Color.Yellow, Width: 3, Softness: 2),
            Outline = new TextOutline(Color.Red, Width: 2),
        };

        var layers = Layers(font, effects);

        Assert.Equal(4, layers.Length);
        Assert.Equal(Color.Black, layers[0].Color);
        Assert.Equal(new Vector2(2, 3), layers[0].Offset);
        Assert.Equal(Color.Yellow, layers[1].Color);
        Assert.Equal(Color.Red, layers[2].Color);
        Assert.Equal(Vector2.Zero, layers[2].Offset);
        Assert.Equal([false, false, false, true], layers.Select(l => l.IsFace));
    }

    // Layer selection

    private static readonly TextEffects Everything = new()
    {
        Shadow = new TextShadow(Color.Black, Vector2.One),
        Glow = new TextGlow(Color.Yellow, Width: 2, Softness: 1),
        Outline = new TextOutline(Color.Red, Width: 1),
    };

    [Fact]
    public void Layers_Effects_DrawsEveryEffectButNotTheFace()
    {
        var layers = Layers(Font(), Everything, TextLayers.Effects);

        Assert.Equal(new[] { Color.Black, Color.Yellow, Color.Red }, layers.Select(l => l.Color));
        Assert.DoesNotContain(layers, l => l.IsFace);
    }

    [Fact]
    public void Layers_Face_DrawsOnlyTheFace()
    {
        var face = Assert.Single(Layers(Font(), Everything, TextLayers.Face));

        Assert.True(face.IsFace);
    }

    [Fact]
    public void Layers_Effects_ThenFace_DrawTheSameAsAll()
    {
        var font = Font();

        var split = Layers(font, Everything, TextLayers.Effects).Concat(Layers(font, Everything, TextLayers.Face));

        Assert.Equal(Layers(font, Everything), split);
    }

    [Fact]
    public void Layers_OneEffect_DrawsOnlyIt()
    {
        var outline = Assert.Single(Layers(Font(), Everything, TextLayers.Outline));

        Assert.Equal(Color.Red, outline.Color);
    }

    [Fact]
    public void Layers_Effects_OfTextWithoutEffects_DrawNothing()
    {
        Assert.Empty(Layers(Font(), null, TextLayers.Effects));
        Assert.Empty(Layers(Font(GlyphFormat.Bitmap), Everything, TextLayers.Effects));
    }

    // Per-quad settings

    [Fact]
    public void Outline_ExtendsFromTheEmboldenedFace()
    {
        var font = Font(middle: 0.5f);
        var effects = new TextEffects { Embolden = 1, Outline = new TextOutline(Color.Black, Width: 3, MiterLimit: 2, Softness: 1) };

        var layers = Layers(font, effects);

        AssertData(layers[0].Data, distanceOffset: 0.5f + 1 + 3, softness: 1, miterLimit: 2, signedRange: -16);
        AssertData(layers[1].Data, distanceOffset: 0.5f + 1, softness: 0, miterLimit: 4, signedRange: -16);
    }

    [Fact]
    public void Outline_RoundCorners_HaveAMiterLimitOfOne()
    {
        var font = Font();

        var layers = Layers(font, new TextEffects { Outline = new TextOutline(Color.Black, 2, TextCorners.Round, MiterLimit: 10) });

        Assert.Equal(1, (float)layers[0].Data.Values.Z, Precision);
    }

    [Fact]
    public void Outline_UnlimitedMiter_StaysWithinHalfFloats()
    {
        var font = Font();

        var layers = Layers(font, new TextEffects { Outline = new TextOutline(Color.Black, 2, MiterLimit: float.PositiveInfinity) });

        Assert.True(float.IsFinite((float)layers[0].Data.Values.Z));
        Assert.True((float)layers[0].Data.Values.Z >= 1000);
    }

    [Fact]
    public void Glow_IsRound()
    {
        var font = Font();

        var layers = Layers(font, new TextEffects { Glow = new TextGlow(Color.White, Width: 2, Softness: 3) });

        AssertData(layers[0].Data, distanceOffset: 2, softness: 3, miterLimit: 1, signedRange: -16);
    }

    [Fact]
    public void Shadow_SpreadsFromTheFace()
    {
        var font = Font();

        var layers = Layers(font, new TextEffects { Embolden = -1, Shadow = new TextShadow(Color.Black, Vector2.One, Spread: 2, Softness: 1) });

        AssertData(layers[0].Data, distanceOffset: 1, softness: 1, miterLimit: 4, signedRange: -16);
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

        Assert.Equal(signedRange, (float)Layers(font, null)[0].Data.Values.W, Precision);
    }

    [Fact]
    public void BitmapFont_DrawsOnlyTheFace_WithNoDistanceSettings()
    {
        var font = Font(GlyphFormat.Bitmap);
        var effects = new TextEffects { Outline = new TextOutline(Color.Black, 2), Shadow = new TextShadow(Color.Black, Vector2.One) };

        var face = Assert.Single(Layers(font, effects));

        Assert.True(face.IsFace);
        Assert.Equal(default, face.Data);
    }

    // Distance range

    [Fact]
    public void Effect_BeyondTheRange_IsClampedToIt()
    {
        // Half the range of 16, less a pixel for the anti-aliasing
        var font = Font(range: 16);

        var layers = Layers(font, new TextEffects { Outline = new TextOutline(Color.Black, Width: 10) });

        Assert.Equal(7, (float)layers[0].Data.Values.X, Precision);
    }

    [Fact]
    public void Effect_Softness_LeavesRoomForItsFade()
    {
        var font = Font(range: 16);

        var layers = Layers(font, new TextEffects { Glow = new TextGlow(Color.White, Width: 10, Softness: 4) });

        Assert.Equal(5, (float)layers[0].Data.Values.X, Precision);
        Assert.Equal(4, (float)layers[0].Data.Values.Y, Precision);
    }

    [Fact]
    public void Effect_WithinTheRange_IsUnchanged()
    {
        var font = Font(range: 16);

        var layers = Layers(font, new TextEffects { Outline = new TextOutline(Color.Black, Width: 6) });

        Assert.Equal(6, (float)layers[0].Data.Values.X, Precision);
    }

    // Extent

    [Fact]
    public void GetExtent_None_IsZero()
    {
        Assert.Equal(Thickness.Zero, TextEffects.None.GetExtent());
    }

    [Fact]
    public void GetExtent_CoversTheWidestEffectOnEachSide()
    {
        var effects = new TextEffects
        {
            Embolden = 1,
            Outline = new TextOutline(Color.Black, Width: 2),
            Shadow = new TextShadow(Color.Black, new Vector2(4, -1), Spread: 0, Softness: 2),
        };

        var extent = effects.GetExtent();

        // The outline reaches 1 + 2 everywhere; the shadow reaches 1 + 1, moved right 4 and up 1
        Assert.Equal(3, extent.Left, Precision);
        Assert.Equal(3, extent.Top, Precision);
        Assert.Equal(6, extent.Right, Precision);
        Assert.Equal(3, extent.Bottom, Precision);
    }

    // Values

    [Fact]
    public void Effects_AreValues()
    {
        var a = new TextEffects { Outline = new TextOutline(Color.Black, 2) };
        var b = new TextEffects { Outline = new TextOutline(Color.Black, 2) };

        Assert.Equal(a, b);
        Assert.Null((a with { Outline = null }).Outline);
    }

    // Outlines

    private static readonly Color Brown = Color.FromRgb(0x902000);
    private static readonly Color Gold = Color.FromRgb(0xFFCC00);
    private static readonly Color Orange = Color.FromRgb(0xFF8000);
    private static readonly Color DarkRed = Color.FromRgb(0x800000);
    private static readonly Color OrangeRed = Color.FromRgb(0xFF4000);

    // A title's: a depth, a band in two colors, a depth, then the outline round the face
    private static readonly TextEffects Title = new()
    {
        Outlines =
        [
            new TextOutline(Brown, Width: 6, TextCorners.Round) { Offset = new Vector2(0, 3) },
            new TextOutline(Gold, Width: 6, TextCorners.Round) { Gradient = new TextGradient(Color.Yellow, Orange) },
            new TextOutline(DarkRed, Width: 3) { Offset = new Vector2(0, 2) },
        ],
        Outline = new TextOutline(OrangeRed, Width: 3, TextCorners.Round),
    };

    [Fact]
    public void Outlines_AreDrawnInTurn_AfterTheGlow_AndBeforeTheOutline()
    {
        var effects = Title with
        {
            Shadow = new TextShadow(Color.Black, Vector2.One),
            Glow = new TextGlow(Color.White, Width: 1, Softness: 0),
        };

        var layers = Layers(Font(range: 32), effects);

        Assert.Equal(
            new[] { Color.Black, Color.White, Brown, Gold, DarkRed, OrangeRed, default },
            layers.Select(l => l.IsFace ? default : l.Color));
        Assert.True(layers[^1].IsFace);
    }

    [Fact]
    public void Outlines_EachExtendFromTheEmboldenedFace_WithTheirOwnCorners()
    {
        var layers = Layers(Font(range: 32, middle: 0.5f), Title with { Embolden = 1 });

        AssertData(layers[0].Data, distanceOffset: 0.5f + 1 + 6, softness: 0, miterLimit: 1, signedRange: -32);
        AssertData(layers[1].Data, distanceOffset: 0.5f + 1 + 6, softness: 0, miterLimit: 1, signedRange: -32);
        AssertData(layers[2].Data, distanceOffset: 0.5f + 1 + 3, softness: 0, miterLimit: 4, signedRange: -32);
        AssertData(layers[3].Data, distanceOffset: 0.5f + 1 + 3, softness: 0, miterLimit: 1, signedRange: -32);
    }

    [Fact]
    public void AnOutline_IsDrawnAtItsOffset_InItsGradient()
    {
        var layers = Layers(Font(range: 32), Title);

        Assert.Equal(new Vector2(0, 3), layers[0].Offset);
        Assert.Equal(Vector2.Zero, layers[1].Offset);
        Assert.Equal(new Vector2(0, 2), layers[2].Offset);
        Assert.Null(layers[0].Gradient);
        Assert.Equal(new TextGradient(Color.Yellow, Orange), layers[1].Gradient);
    }

    [Fact]
    public void Layers_Outline_DrawsEveryOutline()
    {
        var layers = Layers(Font(range: 32), Title, TextLayers.Outline);

        Assert.Equal(new[] { Brown, Gold, DarkRed, OrangeRed }, layers.Select(l => l.Color));
        Assert.Empty(Layers(Font(range: 32), Title, TextLayers.Shadow | TextLayers.Glow));
    }

    [Fact]
    public void Outlines_Alone_AreBackgroundEffects()
    {
        Assert.True(new TextEffects { Outlines = [new TextOutline(Color.Black, 1)] }.HasBackgroundEffects);
        Assert.False(new TextEffects { Outlines = [] }.HasBackgroundEffects);
    }

    [Fact]
    public void GetExtent_CoversTheOutlines_AtTheirOffsets()
    {
        var extent = Title.GetExtent();

        // The widest reach 6 all round, and the deepest, 6 moved down 3, reaches 9 below
        Assert.Equal(6, extent.Left, Precision);
        Assert.Equal(6, extent.Top, Precision);
        Assert.Equal(6, extent.Right, Precision);
        Assert.Equal(9, extent.Bottom, Precision);
    }

    [Fact]
    public void GetExtent_CoversTheOutlinesOffset()
    {
        var effects = new TextEffects { Outline = new TextOutline(Color.Black, Width: 2) { Offset = new Vector2(-1, 4) } };

        var extent = effects.GetExtent();

        Assert.Equal(3, extent.Left, Precision);
        Assert.Equal(6, extent.Bottom, Precision);
    }

    [Fact]
    public void Effects_WithTheSameOutlines_AreEqual_WhetherOrNotTheyShareTheList()
    {
        TextOutline[] outlines = [new TextOutline(Color.Black, 2) { Offset = Vector2.One }, new TextOutline(Color.Red, 1)];
        var a = new TextEffects { Outlines = outlines };
        var b = new TextEffects { Outlines = [new TextOutline(Color.Black, 2) { Offset = Vector2.One }, new TextOutline(Color.Red, 1)] };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, b with { Outlines = [new TextOutline(Color.Red, 1), new TextOutline(Color.Black, 2) { Offset = Vector2.One }] });
        Assert.NotEqual(a, a with { Outlines = [new TextOutline(Color.Black, 2), new TextOutline(Color.Red, 1)] });
        Assert.Equal(new TextEffects(), new TextEffects { Outlines = [] });
        Assert.NotEqual(a, a with { Embolden = 1 });
    }

    [Fact]
    public void Outlines_DifferInTheirGradients()
    {
        var plain = new TextOutline(Color.Black, 2);

        Assert.NotEqual(plain, plain with { Gradient = new TextGradient(Color.Red, Color.Blue) });
        Assert.Equal(plain with { Gradient = new TextGradient(Color.Red, Color.Blue) }, plain with { Gradient = new TextGradient(Color.Red, Color.Blue) });
    }
}

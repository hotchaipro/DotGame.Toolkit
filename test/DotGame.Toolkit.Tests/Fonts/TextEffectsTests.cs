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
        var layers = new TextLayer[TextLayer.MaxCount];
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
}

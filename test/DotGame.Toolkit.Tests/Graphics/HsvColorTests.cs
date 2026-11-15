// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// <see cref="HsvColor"/>, checked against the values CSS gives the same colors.
/// </summary>
public sealed class HsvColorTests
{
    private const float Tolerance = 1e-3f;
    private const float AngleTolerance = 0.05f;

    private static void AssertColor(
        float r,
        float g,
        float b,
        float a,
        Color actual)
    {
        Assert.Equal(r, actual.R, Tolerance);
        Assert.Equal(g, actual.G, Tolerance);
        Assert.Equal(b, actual.B, Tolerance);
        Assert.Equal(a, actual.A, Tolerance);
    }

    public static TheoryData<float, float, float, float, float, float> Colors => new()
    {
        { 0f, 1f, 1f, 1f, 0f, 0f },
        { 60f, 1f, 1f, 1f, 1f, 0f },
        { 120f, 1f, 1f, 0f, 1f, 0f },
        { 180f, 1f, 1f, 0f, 1f, 1f },
        { 240f, 1f, 1f, 0f, 0f, 1f },
        { 300f, 1f, 1f, 1f, 0f, 1f },
        { 30f, 1f, 1f, 1f, 0.5f, 0f },
        { 330f, 1f, 1f, 1f, 0f, 0.5f },

        // Hues past either end of the wheel
        { 360f, 1f, 1f, 1f, 0f, 0f },
        { 480f, 1f, 1f, 0f, 1f, 0f },
        { -60f, 1f, 1f, 1f, 0f, 1f },
        { -120f, 1f, 1f, 0f, 0f, 1f },
        { -180f, 1f, 1f, 0f, 1f, 1f },
        { -300f, 1f, 1f, 1f, 1f, 0f },

        // Saturation and value, clamped to their range
        { 200f, 0.5f, 0.8f, 0.4f, 2f / 3f, 0.8f },
        { 0f, 0f, 0.5f, 0.5f, 0.5f, 0.5f },
        { 77f, 1f, 0f, 0f, 0f, 0f },
        { 0f, 2f, 1f, 1f, 0f, 0f },
        { 0f, -1f, 1f, 1f, 1f, 1f },
        { 0f, 1f, 2f, 1f, 0f, 0f },
        { 0f, 1f, -1f, 0f, 0f, 0f },
    };

    [Theory]
    [MemberData(nameof(Colors))]
    public void ToColor_GivesTheExpectedColor_OpaqueByDefault(
        float h,
        float s,
        float v,
        float r,
        float g,
        float b)
    {
        AssertColor(r, g, b, 1f, new HsvColor(h, s, v).ToColor());
    }

    [Fact]
    public void FromColor_ReportsDegreesAndFractions()
    {
        var hsv = HsvColor.FromColor(new Color(0.4f, 2f / 3f, 0.8f));

        Assert.Equal(200f, hsv.H, AngleTolerance);
        Assert.Equal(0.5f, hsv.S, Tolerance);
        Assert.Equal(0.8f, hsv.V, Tolerance);
        Assert.Equal(1f, hsv.A, Tolerance);
    }

    [Fact]
    public void FromColor_OfATranslucentColor_GivesWhatItsOpaqueFormDoes_AndItsAlpha()
    {
        var hsv = HsvColor.FromColor(new Color(0.4f, 2f / 3f, 0.8f, 0.25f));

        Assert.Equal(200f, hsv.H, AngleTolerance);
        Assert.Equal(0.5f, hsv.S, Tolerance);
        Assert.Equal(0.8f, hsv.V, Tolerance);
        Assert.Equal(0.25f, hsv.A, Tolerance);
    }

    [Fact]
    public void ToColor_TakesStraightAlpha()
    {
        AssertColor(0.5f, 0f, 0f, 0.5f, new HsvColor(0f, 1f, 1f, 0.5f).ToColor());
        AssertColor(0.25f, 0.25f, 0.25f, 0.5f, new HsvColor(0f, 0f, 0.5f, 0.5f).ToColor());
    }

    [Fact]
    public void ChangingOneComponent_KeepsTheOthers_AndTheAlpha()
    {
        var color = new Color(0.5f, 0f, 0f, 0.5f);

        var bright = (HsvColor.FromColor(color) with { V = 1f }).ToColor();

        // A half-transparent red, half as bright and at full brightness, premultiplied
        AssertColor(0.25f, 0f, 0f, 0.5f, color);
        AssertColor(0.5f, 0f, 0f, 0.5f, bright);
        AssertColor(1f, 0f, 0f, 1f, (HsvColor.FromColor(new Color(0.5f, 0f, 0f)) with { V = 1f }).ToColor());
    }

    [Fact]
    public void Colors_RoundTrip_TranslucentOnesToo()
    {
        for (float h = 0f; h < 360f; h += 15f)
        {
            foreach (float s in new[] { 0.25f, 0.5f, 0.75f, 1f })
            {
                foreach (float v in new[] { 0.25f, 0.5f, 1f })
                {
                    foreach (float a in new[] { 0.25f, 1f })
                    {
                        var color = new HsvColor(h, s, v, a).ToColor();

                        AssertColor(color.R, color.G, color.B, a, HsvColor.FromColor(color).ToColor());
                    }
                }
            }
        }
    }
}

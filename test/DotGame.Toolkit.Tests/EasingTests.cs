// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Animation;
using Xunit;

namespace DotGame.Toolkit.Tests;

public sealed class EasingTests
{
    private const float Tolerance = 1e-4f;

    public static TheoryData<Easing> BuiltIn =>
    [
        Easing.Linear,
        Easing.QuadIn,
        Easing.QuadOut,
        Easing.QuadInOut,
        Easing.CubicIn,
        Easing.CubicOut,
        Easing.CubicInOut,
        Easing.SineIn,
        Easing.SineOut,
        Easing.SineInOut,
        Easing.QuartIn,
        Easing.QuartOut,
        Easing.QuartInOut,
        Easing.QuintIn,
        Easing.QuintOut,
        Easing.QuintInOut,
        Easing.ExpoIn,
        Easing.ExpoOut,
        Easing.ExpoInOut,
        Easing.CircIn,
        Easing.CircOut,
        Easing.CircInOut,
        Easing.BackIn,
        Easing.BackOut,
        Easing.BackInOut,
        Easing.ElasticIn,
        Easing.ElasticOut,
        Easing.ElasticInOut,
        Easing.BounceIn,
        Easing.BounceOut,
        Easing.BounceInOut,
        new ExponentialEase(),
        new CubicBezierEase(0.25f, 0.1f, 0.25f, 1f),
    ];

    [Theory]
    [MemberData(nameof(BuiltIn))]
    public void Ease_StartsAtZeroAndEndsAtOne(
        Easing easing)
    {
        Assert.Equal(0, easing.Evaluate(0), Tolerance);
        Assert.Equal(1, easing.Evaluate(1), Tolerance);
    }

    [Fact]
    public void EaseOut_IsEaseInReflected()
    {
        var easeIn = new CubicEase(EasingMode.In);
        var easeOut = new CubicEase(EasingMode.Out);

        for (float t = 0; t <= 1; t += 0.125f)
        {
            Assert.Equal(1 - easeIn.Evaluate(1 - t), easeOut.Evaluate(t), Tolerance);
        }
    }

    [Fact]
    public void EaseInOut_IsHalfwayAtTheMiddle()
    {
        Assert.Equal(0.5f, Easing.CubicInOut.Evaluate(0.5f), Tolerance);
        Assert.Equal(0.5f, Easing.QuadInOut.Evaluate(0.5f), Tolerance);
    }

    [Fact]
    public void Linear_LeavesProgressUnchanged()
    {
        Assert.Equal(0.3f, Easing.Linear.Evaluate(0.3f), Tolerance);
    }

    [Fact]
    public void BackOut_Overshoots()
    {
        float max = 0;
        for (float t = 0; t <= 1; t += 0.01f)
        {
            max = MathF.Max(max, Easing.BackOut.Evaluate(t));
        }

        // About 10% with the default amplitude
        Assert.InRange(max, 1.08f, 1.12f);
    }

    [Fact]
    public void CubicBezier_WithLinearControlPoints_IsLinear()
    {
        var easing = new CubicBezierEase(0.3f, 0.3f, 0.7f, 0.7f);

        Assert.Equal(0.42f, easing.Evaluate(0.42f), Tolerance);
    }

    [Fact]
    public void CubicBezier_RejectsControlPointsOutsideZeroToOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CubicBezierEase(-0.1f, 0, 0.5f, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CubicBezierEase(0.5f, 0, 1.1f, 1));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(1.5f, 1f)]
    public void Evaluate_SettlesAtItsEnds_OutsideThem(
        float time,
        float expected)
    {
        Assert.Equal(expected, Easing.CubicOut.Evaluate(time), Tolerance);
        Assert.Equal(expected, Easing.BackOut.Evaluate(time), Tolerance);
    }

    [Fact]
    public void BackInOut_FollowsPennersCurve()
    {
        const float Widened = 1.70158f * 1.525f;

        for (float t = 0; t < 0.5f; t += 0.05f)
        {
            float doubled = 2 * t;
            float expected = (doubled * doubled * (((Widened + 1) * doubled) - Widened)) / 2;
            Assert.Equal(expected, Easing.BackInOut.Evaluate(t), Tolerance);
        }
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(0.6f)]
    public void ElasticOut_FollowsPennersCurve(
        float t)
    {
        float expected = (MathF.Pow(2, -10 * t) * MathF.Sin(((t * 10) - 0.75f) * (MathF.Tau / 3))) + 1;

        Assert.Equal(expected, Easing.ElasticOut.Evaluate(t), Tolerance);
    }

    [Theory]
    [InlineData(EasingMode.In)]
    [InlineData(EasingMode.Out)]
    [InlineData(EasingMode.InOut)]
    public void Elastic_WithTheDefaults_IsTheStandardCurve(
        EasingMode mode)
    {
        var standard = mode switch
        {
            EasingMode.In => Easing.ElasticIn,
            EasingMode.Out => Easing.ElasticOut,
            _ => Easing.ElasticInOut,
        };

        var easing = Easing.Elastic(mode);
        for (int i = 0; i <= 20; i++)
        {
            Assert.Equal(standard.Evaluate(i / 20f), easing.Evaluate(i / 20f), Tolerance);
        }
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.3f)]
    [InlineData(0.7f)]
    public void Elastic_FollowsPennersCurve_WithItsSettings(
        float t)
    {
        // Penner's easeInElastic with amplitude 1.2 and period 0.6, and going out, that curve
        // mirrored, as every easing's out is. (Penner's own easeOutElastic is the mirror only at
        // amplitude 1.)
        const float Amplitude = 1.2f;
        const float Period = 0.6f;
        float shift = Period / MathF.Tau * MathF.Asin(1 / Amplitude);
        float EaseIn(float u) => -(Amplitude * MathF.Pow(2, 10 * (u - 1)) * MathF.Sin((u - 1 - shift) * (MathF.Tau / Period)));

        Assert.Equal(EaseIn(t), Easing.Elastic(EasingMode.In, Amplitude, Period).Evaluate(t), Tolerance);
        Assert.Equal(1 - EaseIn(1 - t), Easing.Elastic(EasingMode.Out, Amplitude, Period).Evaluate(t), Tolerance);
    }

    [Theory]
    [InlineData(EasingMode.In)]
    [InlineData(EasingMode.Out)]
    [InlineData(EasingMode.InOut)]
    public void Elastic_EndsAtItsEnds_WithAnySettings(
        EasingMode mode)
    {
        var easing = Easing.Elastic(mode, 1.7f, 0.8f);

        Assert.Equal(0f, easing.Evaluate(0f), Tolerance);
        Assert.Equal(1f, easing.Evaluate(1f), Tolerance);
        Assert.Equal(1f, easing.Evaluate(0.999f), 0.01f);
    }

    [Theory]
    [InlineData(0.9f, 0.3f)]
    [InlineData(float.NaN, 0.3f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, -0.3f)]
    [InlineData(1f, float.NaN)]
    [InlineData(1f, float.PositiveInfinity)]
    public void Elastic_ThrowsForSettingsOutOfRange(
        float amplitude,
        float period)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.Elastic(EasingMode.Out, amplitude, period));
    }

    [Fact]
    public void Steps_HoldsEachValue_FromZeroToOne()
    {
        // Four values, 0, 1/3, 2/3 and 1, each for a quarter of the time
        var easing = Easing.Steps(4);

        Assert.Equal(0f, easing.Evaluate(0f), Tolerance);
        Assert.Equal(0f, easing.Evaluate(0.2f), Tolerance);
        Assert.Equal(1f / 3f, easing.Evaluate(0.3f), Tolerance);
        Assert.Equal(2f / 3f, easing.Evaluate(0.6f), Tolerance);
        Assert.Equal(1f, easing.Evaluate(1f), Tolerance);
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.Steps(1));
    }

    // Regression: Steps reached 1 only at exactly the end, as CSS's steps() does, so a looping or
    // ping-pong animation, whose frames almost never land on the end, never showed its last value

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Steps_HoldsTheLastValue_BeforeTheEnd(
        int count)
    {
        var easing = Easing.Steps(count);

        Assert.Equal(1f, easing.Evaluate(0.999f), Tolerance);
        Assert.Equal(1f, easing.Evaluate(1f - (0.5f / count)), Tolerance);
    }

    [Fact]
    public void Steps_PingPongTween_ReachesTheEnd()
    {
        // A frame at 60 frames a second never lands on the turn exactly
        var tween = Tween.Create(0f, 1f, TimeSpan.FromSeconds(1.5), Easing.Steps(6), playback: PlaybackMode.PingPong);
        float highest = 0f;
        for (int frame = 0; frame < 180; frame++)
        {
            tween.Update(TimeSpan.FromSeconds(1.0 / 60.0) + TimeSpan.FromTicks(7));
            highest = MathF.Max(highest, tween.Value);
        }

        Assert.Equal(1f, highest, Tolerance);
    }

    [Theory]
    [InlineData(0.2f, true)]
    [InlineData(0.5f, true)]
    [InlineData(1f, false)]
    public void Spring_StartsAndEndsExactly_AndOvershootsUnlessFullyDamped(
        float damping,
        bool overshoots)
    {
        var easing = Easing.Spring(damping);
        float max = 0;
        for (float t = 0; t <= 1; t += 0.005f)
        {
            max = MathF.Max(max, easing.Evaluate(t));
        }

        Assert.Equal(0f, easing.Evaluate(0f), Tolerance);
        Assert.Equal(1f, easing.Evaluate(1f), Tolerance);
        Assert.Equal(overshoots, max > 1.001f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.5f)]
    [InlineData(float.NaN)]
    public void Spring_RefusesADampingOutOfItsRange(
        float damping)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.Spring(damping));
    }

    [Fact]
    public void Custom_FollowsItsCurve_InItsMode()
    {
        var easeIn = Easing.Custom(t => t * t, EasingMode.In);
        var easeOut = Easing.Custom(t => t * t, EasingMode.Out);

        Assert.Equal(0.25f, easeIn.Evaluate(0.5f), Tolerance);
        Assert.Equal(0.75f, easeOut.Evaluate(0.5f), Tolerance);
    }

    [Fact]
    public void Reversed_OfAnInCurve_IsItsOutCurve()
    {
        var reversed = Easing.CubicIn.Reversed();

        for (float t = 0; t <= 1; t += 0.125f)
        {
            Assert.Equal(Easing.CubicOut.Evaluate(t), reversed.Evaluate(t), Tolerance);
        }
    }
}

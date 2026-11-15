// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Animation;
using Xunit;

// NOTE: These test the plain wait. One with a token takes another path, and says so
#pragma warning disable xUnit1051

namespace DotGame.Toolkit.Tests;

public sealed class TweenTests
{
    private const int Tolerance = 4;

    private static TimeSpan Ms(
        int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    [Fact]
    public void AValue_FollowsItsCurve_AndStopsExactlyAtItsEnd()
    {
        var tween = Tween.Create(0f, 10f, Ms(100), Easing.QuadIn);

        Assert.Equal(0f, tween.Value);
        Assert.False(tween.IsFinished);

        tween.Update(Ms(50));
        Assert.Equal(2.5f, tween.Value, Tolerance);
        Assert.Equal(0.5f, tween.Progress, Tolerance);

        tween.Update(Ms(500));
        Assert.Equal(10f, tween.Value);
        Assert.Equal(1f, tween.Progress);
        Assert.True(tween.IsFinished);
    }

    [Fact]
    public void Each_BuiltInType_Moves()
    {
        var position = Tween.Create(Vector2.Zero, new Vector2(10, 20), Ms(100));
        var color = Tween.Create(Color.Black, Color.White, Ms(100));
        var angle = Tween.Create(Angle.FromDegrees(350), Angle.FromDegrees(10), Ms(100));
        var custom = Tween.Create(0, 10, Ms(100), static (a, b, amount) => (int)(a + ((b - a) * amount)));

        position.Update(Ms(50));
        color.Update(Ms(50));
        angle.Update(Ms(50));
        custom.Update(Ms(50));

        Assert.Equal(new Vector2(5, 10), position.Value);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), color.Value);
        Assert.Equal(0f, Angle.Wrap(angle.Value).Degrees, 2);
        Assert.Equal(5, custom.Value);
    }

    [Fact]
    public void AnOvershootingCurve_GoesPastItsEnd_ThenLandsOnIt()
    {
        var tween = Tween.Create(0f, 10f, Ms(100), Easing.BackOut);

        tween.Update(Ms(60));
        Assert.True(tween.Value > 10f);

        tween.Update(Ms(40));
        Assert.Equal(10f, tween.Value);
    }

    [Fact]
    public void ADelay_HoldsItsStart()
    {
        var tween = Tween.Create(0f, 10f, Ms(100), delay: Ms(50));

        tween.Update(Ms(50));
        Assert.Equal(0f, tween.Value);

        tween.Update(Ms(50));
        Assert.Equal(5f, tween.Value, Tolerance);
    }

    [Fact]
    public void ALoop_StartsOver_AndNeverFinishes()
    {
        var tween = Tween.Create(0f, 10f, Ms(100), playback: PlaybackMode.Loop);

        tween.Update(Ms(250));

        Assert.Equal(5f, tween.Value, Tolerance);
        Assert.False(tween.IsFinished);
    }

    [Theory]
    [InlineData(50, 5f)]
    [InlineData(150, 5f)]
    [InlineData(200, 0f)]
    [InlineData(250, 5f)]
    public void APingPong_GoesThereAndBack(
        int milliseconds,
        float expected)
    {
        var tween = Tween.Create(0f, 10f, Ms(100), playback: PlaybackMode.PingPong);

        tween.Update(Ms(milliseconds));

        Assert.Equal(expected, tween.Value, Tolerance);
    }

    [Fact]
    public void Restart_GoesBackToItsStart_OrANewJourney()
    {
        var tween = Tween.Create(0f, 10f, Ms(100));
        tween.Update(Ms(200));

        tween.Restart();
        Assert.Equal(0f, tween.Value);
        Assert.False(tween.IsFinished);

        tween.Restart(20f, 40f);
        tween.Update(Ms(50));
        Assert.Equal(30f, tween.Value, Tolerance);
    }

    [Fact]
    public void Finish_JumpsToItsEnd_EvenInALoop()
    {
        var tween = Tween.Create(0f, 10f, Ms(100), playback: PlaybackMode.Loop);
        tween.Update(Ms(50));

        tween.Finish();
        tween.Update(Ms(50));

        Assert.True(tween.IsFinished);
        Assert.Equal(10f, tween.Value);
    }

    [Fact]
    public void AZeroDuration_FinishesOnTheFirstUpdate()
    {
        var tween = Tween.Create(0f, 10f, TimeSpan.Zero);

        tween.Update(TimeSpan.Zero);

        Assert.True(tween.IsFinished);
        Assert.Equal(10f, tween.Value);
    }

    [Fact]
    public async Task WhenFinished_Completes_OrIsCanceled()
    {
        var tween = Tween.Create(0f, 10f, Ms(100));
        var finished = tween.WhenFinishedAsync();
        Assert.False(finished.IsCompleted);

        tween.Update(Ms(100));
        await finished;
        Assert.True(tween.WhenFinishedAsync().IsCompleted);

        tween.Restart();
        using var canceled = new CancellationTokenSource();
        var waiting = tween.WhenFinishedAsync(canceled.Token);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public void NegativeTimes_AreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tween.Create(0f, 1f, Ms(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tween.Create(0f, 1f, Ms(1), delay: Ms(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Tween.Create(0f, 1f, Ms(1)).Update(Ms(-1)));
    }

    [Fact]
    public void Playing_AllocatesNothing()
    {
        var tween = Tween.Create(Vector2.Zero, Vector2.One, Ms(100), Easing.CubicOut, playback: PlaybackMode.PingPong);
        tween.Update(Ms(1));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            tween.Update(Ms(1));
        }

        tween.Restart(Vector2.One, Vector2.Zero);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
    }
}

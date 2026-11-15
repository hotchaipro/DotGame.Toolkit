// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Toolkit.Animation;
using Xunit;
using static DotGame.Toolkit.Tests.Graphics.TestRegions;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class SpriteAnimationTests
{
    [Fact]
    public void Loop_PlaysFramesInOrderAndStartsOver()
    {
        var animation = new SpriteAnimation(Regions(4), 10);

        Assert.Equal(Ms(400), animation.Duration);
        Assert.Equal([0, 1, 2, 3, 0, 1, 2, 3], Sequence(animation, 8, 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-50, 0)]
    [InlineData(99.9, 0)]
    [InlineData(100, 1)]
    [InlineData(399.9, 3)]
    [InlineData(400, 0)]
    public void Loop_FrameBoundaries(
        double ms,
        int expected)
    {
        var animation = new SpriteAnimation(Regions(4), 10);

        Assert.Equal(expected, animation.GetFrameIndex(Ms(ms)));
    }

    [Fact]
    public void Once_HoldsLastFrame()
    {
        var animation = new SpriteAnimation(Regions(4), 10, PlaybackMode.Once);

        Assert.Equal([0, 1, 2, 3, 3, 3], Sequence(animation, 6, 100));
        Assert.Equal(3, animation.GetFrameIndex(TimeSpan.FromHours(5)));
    }

    [Fact]
    public void PingPong_DoesNotRepeatEndFrames()
    {
        var animation = new SpriteAnimation(Regions(4), 10, PlaybackMode.PingPong);

        Assert.Equal(Ms(600), animation.Duration);
        Assert.Equal([0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 2, 1, 0], Sequence(animation, 13, 100));
    }

    [Fact]
    public void PingPong_WithTwoFramesAlternates()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.PingPong);

        Assert.Equal([0, 1, 0, 1], Sequence(animation, 4, 100));
    }

    [Fact]
    public void PingPong_WithOneFrameShowsIt()
    {
        var animation = new SpriteAnimation(Regions(1), 10, PlaybackMode.PingPong);

        Assert.Equal(0, animation.GetFrameIndex(Ms(12345)));
    }

    [Fact]
    public void Holds_LengthenFramesRelativeToFrameRate()
    {
        var animation = HeldAnimation(10, PlaybackMode.Loop);

        Assert.Equal(Ms(550), animation.Duration);
        Assert.Equal(Ms(100), animation.GetFrameDuration(0));
        Assert.Equal(Ms(400), animation.GetFrameDuration(1));
        Assert.Equal(Ms(50), animation.GetFrameDuration(2));
        Assert.Equal(1, animation.GetFrameIndex(Ms(499)));
        Assert.Equal(2, animation.GetFrameIndex(Ms(500)));
        Assert.Equal(0, animation.GetFrameIndex(Ms(550)));
    }

    [Fact]
    public void Holds_KeepRhythmWhenFrameRateChanges()
    {
        Assert.Equal(Ms(275), HeldAnimation(20, PlaybackMode.Loop).Duration);
    }

    [Fact]
    public void Holds_PingPong()
    {
        var r = Regions(3);
        var animation = new SpriteAnimation(
            [new SpriteAnimationFrame(r[0]), new SpriteAnimationFrame(r[1], 4), new SpriteAnimationFrame(r[2], 2)],
            10,
            PlaybackMode.PingPong);

        // Forward: 0 (100 ms), 1 (400 ms), 2 (200 ms); back: 1 (400 ms).
        Assert.Equal(Ms(1100), animation.Duration);
        Assert.Equal(1, animation.GetFrameIndex(Ms(750)));
        Assert.Equal(1, animation.GetFrameIndex(Ms(1099)));
        Assert.Equal(0, animation.GetFrameIndex(Ms(1100)));
    }

    [Fact]
    public void Size_IsTheFramesLogicalSize()
    {
        Assert.Equal(new Vector2Int(48, 64), new SpriteAnimation([Region(48, 64), Region(48, 64)], 10).Size);
    }

    [Fact]
    public void Frames_AreCopied()
    {
        var frames = new List<DotGame.Toolkit.Graphics.TextureRegion>(Regions(2));
        var animation = new SpriteAnimation(frames, 10);

        frames.Add(Region());

        Assert.Equal(2, animation.Frames.Count);
    }

    [Fact]
    public void Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => new SpriteAnimation(Array.Empty<DotGame.Toolkit.Graphics.TextureRegion>(), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimation(Regions(2), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimation(Regions(2), float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimation(Regions(2), 10, (PlaybackMode)42));
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([new SpriteAnimationFrame(Region()), new SpriteAnimationFrame(Region(), 0)], 10));
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([new SpriteAnimationFrame(Region()), new SpriteAnimationFrame(Region(), float.PositiveInfinity)], 10));
    }

    [Fact]
    public void Constructor_RejectsMixedFrameSizes()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SpriteAnimation([Region(32, 32), Region(16, 32)], 10));

        Assert.Contains("same logical size", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFrameDuration_RejectsOutOfRangeIndex()
    {
        var animation = new SpriteAnimation(Regions(2), 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => animation.GetFrameDuration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => animation.GetFrameDuration(2));
    }

    internal static TimeSpan Ms(
        double milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    // Frames 0, 1 held for 4, and 2 held for 0.5.
    private static SpriteAnimation HeldAnimation(
        float framesPerSecond,
        PlaybackMode playback)
    {
        var r = Regions(3);
        return new SpriteAnimation(
            [new SpriteAnimationFrame(r[0]), new SpriteAnimationFrame(r[1], 4), new SpriteAnimationFrame(r[2], 0.5f)],
            framesPerSecond,
            playback);
    }

    // The frame index in the middle of each step.
    private static int[] Sequence(
        SpriteAnimation animation,
        int count,
        double stepMilliseconds)
    {
        return Enumerable.Range(0, count)
            .Select(i => animation.GetFrameIndex(Ms((i * stepMilliseconds) + (stepMilliseconds / 2))))
            .ToArray();
    }
}

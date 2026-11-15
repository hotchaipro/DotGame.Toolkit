// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Graphics;
using Xunit;
using static DotGame.Toolkit.Tests.Graphics.TestRegions;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class SpriteAnimationExtensionsTests
{
    private readonly TextureAtlas _atlas = Atlas(
        "run_2", "run_10", "run_1", "RUN_3",
        "runner_1", "run_x", "run",
        "idle_left_0", "idle_left_1", "idle_0",
        "coin");

    [Fact]
    public void GetAnimationFrames_OrdersFramesNumerically()
    {
        var frames = this._atlas.GetAnimationFrames("run");

        Assert.Equal(
            [this._atlas.GetRegion("run_1"), this._atlas.GetRegion("run_2"), this._atlas.GetRegion("RUN_3"), this._atlas.GetRegion("run_10")],
            frames);
    }

    [Fact]
    public void GetAnimationFrames_SplitsOnLastUnderscore()
    {
        Assert.Equal(2, this._atlas.GetAnimationFrames("idle_left").Count);
        Assert.Single(this._atlas.GetAnimationFrames("idle"));
    }

    [Fact]
    public void GetAnimationFrames_IgnoresCase()
    {
        Assert.Single(this._atlas.GetAnimationFrames("Runner"));
    }

    [Theory]
    [InlineData("coin")]
    [InlineData("jump")]
    public void GetAnimationFrames_ThrowsWhenThereAreNoFrames(
        string name)
    {
        Assert.Throws<KeyNotFoundException>(() => this._atlas.GetAnimationFrames(name));
    }

    [Fact]
    public void GetAnimationFrames_IgnoresSignsAndSpaces()
    {
        var atlas = Atlas("run_-1", "run_+2", "run_ 3");

        Assert.Throws<KeyNotFoundException>(() => atlas.GetAnimationFrames("run"));
    }

    [Fact]
    public void GetAnimationFrames_RejectsDuplicateNumbers()
    {
        var atlas = Atlas("run_1", "run_01");

        Assert.Throws<InvalidDataException>(() => atlas.GetAnimationFrames("run"));
    }

    [Fact]
    public void GetAnimation_BuildsAnimationFromFrames()
    {
        var animation = this._atlas.CreateAnimation("run", 12, PlaybackMode.PingPong);

        Assert.Equal(4, animation.Frames.Count);
        Assert.Equal(12f, animation.FramesPerSecond);
        Assert.Equal(PlaybackMode.PingPong, animation.Playback);
    }

    [Fact]
    public void RegionNames_ListsEveryRegion()
    {
        Assert.Equal(11, this._atlas.RegionNames.Count);
        Assert.Contains("coin", this._atlas.RegionNames);
    }
}

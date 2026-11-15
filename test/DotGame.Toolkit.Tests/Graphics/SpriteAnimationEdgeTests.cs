// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Animation;
using Xunit;
using static DotGame.Toolkit.Tests.Graphics.TestRegions;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// The edges of animation timing: whether the frame showing and the frames entered agree at
/// every playback and hold, long and empty updates, and actions that change the play mid-update.
/// </summary>
public sealed class SpriteAnimationEdgeTests
{
    public static TheoryData<PlaybackMode, float[]> Timings()
    {
        var data = new TheoryData<PlaybackMode, float[]>();
        foreach (var playback in new[] { PlaybackMode.Loop, PlaybackMode.Once, PlaybackMode.PingPong })
        {
            data.Add(playback, [1]);
            data.Add(playback, [1, 1]);
            data.Add(playback, [1, 3]);
            data.Add(playback, [1, 1, 1]);
            data.Add(playback, [0.5f, 3, 1.25f]);
            data.Add(playback, [2, 0.1f, 0.7f, 5, 1]);
        }

        return data;
    }

    private static SpriteAnimation Timed(
        PlaybackMode playback,
        float[] holds)
    {
        var frames = new SpriteAnimationFrame[holds.Length];
        for (int i = 0; i < holds.Length; i++)
        {
            frames[i] = new SpriteAnimationFrame(Region(x: i * 32), holds[i]);
        }

        return new SpriteAnimation(frames, 7, playback);
    }

    // The frames a pass enters

    [Theory]
    [MemberData(nameof(Timings))]
    public void EveryFrameEntered_IsTheFrameShowing_FromItsFirstTick(
        PlaybackMode playback,
        float[] holds)
    {
        var animation = Timed(playback, holds);
        var starts = animation.PassStarts;
        var frames = animation.PassFrames;

        for (int i = 0; i < starts.Length; i++)
        {
            Assert.Equal(frames[i], animation.GetFrameIndex(new TimeSpan(starts[i])));

            if (i > 0)
            {
                // Up to the tick before, the frame before was showing
                Assert.Equal(frames[i - 1], animation.GetFrameIndex(new TimeSpan(starts[i] - 1)));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Timings))]
    public void APass_LastsAsLongAsItsFrames(
        PlaybackMode playback,
        float[] holds)
    {
        var animation = Timed(playback, holds);

        var total = TimeSpan.Zero;
        foreach (var frame in animation.PassFrames)
        {
            total += animation.GetFrameDuration(frame);
        }

        Assert.Equal(animation.Duration, total);
    }

    [Theory]
    [MemberData(nameof(Timings))]
    public void Looping_Wraps_ToTheSameFrames(
        PlaybackMode playback,
        float[] holds)
    {
        var animation = Timed(playback, holds);
        if (playback == PlaybackMode.Once)
        {
            Assert.Equal(holds.Length - 1, animation.GetFrameIndex(animation.Duration * 3));
            return;
        }

        foreach (var start in animation.PassStarts)
        {
            Assert.Equal(
                animation.GetFrameIndex(new TimeSpan(start)),
                animation.GetFrameIndex(new TimeSpan(start + (animation.Duration.Ticks * 3))));
        }
    }

    [Theory]
    [MemberData(nameof(Timings))]
    public void Player_ShowsWhatTheAnimationSays_AtEveryStep(
        PlaybackMode playback,
        float[] holds)
    {
        var animation = Timed(playback, holds);
        var player = new SpriteAnimationPlayer(animation);
        var step = TimeSpan.FromTicks((animation.Duration.Ticks / 13) + 1);
        var time = TimeSpan.Zero;

        for (int i = 0; i < 60; i++)
        {
            player.Update(step);
            time += step;
            Assert.Equal(animation.GetFrameIndex(time), player.FrameIndex);
        }
    }

    [Theory]
    [MemberData(nameof(Timings))]
    public void Actions_RunForEachFrameEntered_InOrder_AtAnyStep(
        PlaybackMode playback,
        float[] holds)
    {
        var animation = Timed(playback, holds);
        var starts = animation.PassStarts;
        var passFrames = animation.PassFrames;
        long pass = animation.Duration.Ticks;

        foreach (var parts in new[] { 3, 13, 101 })
        {
            var entered = new List<int>();
            var player = new SpriteAnimationPlayer();
            var handle = player.Play(animation);
            for (int f = 0; f < holds.Length; f++)
            {
                int frame = f;
                handle = handle.When(frame, () => entered.Add(frame));
            }

            long step = (pass / parts) + 1;
            long time = 0;
            for (int i = 0; i < parts * 2; i++)
            {
                player.Update(new TimeSpan(step));
                time += step;
            }

            // Every entry up to the time, the way a frame-by-frame walk would see them
            var expected = new List<int>();
            for (long passIndex = 0; (passIndex * pass) <= time; passIndex++)
            {
                for (int i = 0; i < starts.Length; i++)
                {
                    if ((passIndex * pass) + starts[i] <= time)
                    {
                        expected.Add(passFrames[i]);
                    }
                }

                if (playback == PlaybackMode.Once)
                {
                    break;
                }
            }

            Assert.Equal(expected, entered);
        }
    }

    // Constructing

    [Fact]
    public void Constructor_RejectsAnUnknownPlayback()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimation(Regions(2), 10, (PlaybackMode)99));
    }

    [Fact]
    public void Constructor_RejectsAFrameTooShortToShow()
    {
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([new SpriteAnimationFrame(Region(), 1e-9f)], 60));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_RejectsAnImpossibleHold(
        float hold)
    {
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([new SpriteAnimationFrame(Region(), hold)], 10));
    }

    [Fact]
    public void Constructor_RejectsAFrameWithNoRegion()
    {
        Assert.Throws<ArgumentException>(() => new SpriteAnimation([default(SpriteAnimationFrame)], 10));
    }

    [Fact]
    public void Holds_ManyShortFrames_DontAccumulateRounding()
    {
        // A third of a tick's error per frame would add up to thousands of ticks over this many
        var frames = new SpriteAnimationFrame[10000];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = new SpriteAnimationFrame(Region(), 1);
        }

        var animation = new SpriteAnimation(frames, 3);

        long expected = (long)Math.Round(frames.Length * (double)TimeSpan.TicksPerSecond / 3);
        Assert.InRange(animation.Duration.Ticks, expected - 1, expected + 1);
    }

    // A player with nothing to play

    [Fact]
    public void Player_WithoutAnAnimation_TakesEveryCommand()
    {
        var player = new SpriteAnimationPlayer();

        player.Update(TimeSpan.FromSeconds(1));
        player.Pause();
        player.Resume();
        player.Stop();

        Assert.Null(player.Animation);
        Assert.False(player.IsPlaying);
        Assert.False(player.IsFinished);
        Assert.Equal(TimeSpan.Zero, player.Time);
        Assert.Throws<InvalidOperationException>(() => player.Time = TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Play_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SpriteAnimationPlayer().Play(null!));
    }

    [Fact]
    public void Handle_RejectsNullActions()
    {
        var handle = new SpriteAnimationPlayer().Play(new SpriteAnimation(Regions(2), 10));

        Assert.Throws<ArgumentNullException>(() => handle.When(0, null!));
        Assert.Throws<ArgumentNullException>(() => handle.Then(null!));
    }

    // Updates that don't move time

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_WithNoTime_HoldsTheFrame_AndRunsTheFirstFramesAction(
        double seconds)
    {
        var player = new SpriteAnimationPlayer();
        int ran = 0;
        player.Play(new SpriteAnimation(Regions(3), 10)).When(0, () => ran++);

        player.Update(TimeSpan.FromSeconds(seconds));
        player.Update(TimeSpan.FromSeconds(seconds));

        Assert.Equal(1, ran);
        Assert.Equal(TimeSpan.Zero, player.Time);
    }

    // Long updates

    [Fact]
    public void Update_LongerThanAPass_OfALoop_RunsEachActionOnce_AndLandsWhereItShould()
    {
        var animation = new SpriteAnimation(Regions(3), 10);
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(animation).When(0, () => entered.Add(0)).When(1, () => entered.Add(1)).When(2, () => entered.Add(2));

        player.Update(TimeSpan.FromSeconds(100.15));

        Assert.Equal(3, entered.Count);
        Assert.Equal(animation.GetFrameIndex(TimeSpan.FromSeconds(100.15)), player.FrameIndex);
    }

    [Fact]
    public void Update_ExactlyAPass_OfALoop_RunsEachActionOnce()
    {
        var animation = new SpriteAnimation(Regions(3), 10);
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(animation).When(0, () => entered.Add(0)).When(1, () => entered.Add(1)).When(2, () => entered.Add(2));

        player.Update(animation.Duration);

        Assert.Equal(3, entered.Count);
        Assert.Equal(0, player.FrameIndex);
    }

    [Fact]
    public void Update_LongerThanAPass_OfOnce_RunsEveryActionThenThen()
    {
        var player = new SpriteAnimationPlayer();
        var log = new List<string>();
        player.Play(new SpriteAnimation(Regions(3), 10, PlaybackMode.Once))
            .When(0, () => log.Add("0")).When(1, () => log.Add("1")).When(2, () => log.Add("2"))
            .Then(() => log.Add("then"));

        player.Update(TimeSpan.FromSeconds(100));

        Assert.Equal(["0", "1", "2", "then"], log);
        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
        Assert.Equal(2, player.FrameIndex);
    }

    [Fact]
    public void Once_LandingExactlyOnItsEnd_Finishes()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer(animation);

        player.Update(animation.Duration);

        Assert.True(player.IsFinished);
        Assert.Equal(1, player.FrameIndex);
    }

    [Fact]
    public void Once_AnUpdateAfterFinishing_DoesNothing()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        int thens = 0;
        player.Play(animation).Then(() => thens++);
        player.Update(TimeSpan.FromSeconds(1));

        player.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(1, thens);
        Assert.Equal(animation.Duration, player.Time);
    }

    // Speed

    [Fact]
    public void Speed_AppliesToTheLeftoverTimeOfAChainedPlay()
    {
        var first = new SpriteAnimation(Regions(1), 10, PlaybackMode.Once);
        var second = new SpriteAnimation(Regions(4), 10);
        var player = new SpriteAnimationPlayer { Speed = 2 };
        player.Play(first).Then(() => player.Play(second));

        // 0.1 s at double speed plays the first's 0.1 s and leaves 0.1 s of the second
        player.Update(TimeSpan.FromSeconds(0.1));

        Assert.Same(second, player.Animation);
        Assert.Equal(TimeSpan.FromSeconds(0.1), player.Time);
    }

    // Actions that change the play

    [Fact]
    public void WhenAction_ThatStops_EndsTheUpdate_AtTheFirstFrame()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(new SpriteAnimation(Regions(3), 10))
            .When(0, () =>
            {
                entered.Add(0);
                player.Stop();
            })
            .When(1, () => entered.Add(1));

        player.Update(TimeSpan.FromSeconds(0.15));

        Assert.Equal([0], entered);
        Assert.False(player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, player.Time);
    }

    [Fact]
    public void WhenAction_ThatPauses_EndsTheUpdate_WhereTheTimeWas()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(new SpriteAnimation(Regions(3), 10))
            .When(1, () =>
            {
                entered.Add(1);
                player.Pause();
            })
            .When(2, () => entered.Add(2));
        player.Update(TimeSpan.FromSeconds(0.05));

        player.Update(TimeSpan.FromSeconds(0.2));

        Assert.Equal([1], entered);
        Assert.False(player.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(0.05), player.Time);
    }

    [Fact]
    public void Play_SaysWhetherItStartedThePlay()
    {
        var animation = new SpriteAnimation(Regions(3), 10);
        var player = new SpriteAnimationPlayer();

        Assert.True(player.Play(animation).IsNewPlay);
        Assert.False(player.Play(animation).IsNewPlay);
        Assert.True(player.Play(animation, restart: true).IsNewPlay);
    }

    [Fact]
    public void WhenAction_ThatPauses_DoesNotRunAgainOnResume()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(new SpriteAnimation(Regions(3), 10))
            .When(1, () =>
            {
                entered.Add(1);
                player.Pause();
            })
            .When(2, () => entered.Add(2));
        player.Update(TimeSpan.FromSeconds(0.15));

        // The time stayed at 0 through the pause, so frame 2 is 0.2 away
        player.Resume();
        player.Update(TimeSpan.FromSeconds(0.25));

        Assert.Equal([1, 2], entered);
    }

    [Fact]
    public void WhenAction_ThatRestartsItsOwnAnimation_DropsItself()
    {
        var animation = new SpriteAnimation(Regions(3), 10);
        var player = new SpriteAnimationPlayer();
        int ran = 0;
        player.Play(animation).When(1, () =>
        {
            ran++;
            player.Play(animation, restart: true);
        });

        for (int i = 0; i < 10; i++)
        {
            player.Update(TimeSpan.FromSeconds(0.15));
        }

        Assert.Equal(1, ran);
    }

    [Fact]
    public void Then_ThatResumes_PlaysAgainWithTheLeftoverTime()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        player.Play(animation).Then(() => player.Resume());

        player.Update(TimeSpan.FromSeconds(0.25));

        Assert.False(player.IsFinished);
        Assert.True(player.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(0.05), player.Time);
    }

    [Fact]
    public void Then_ThatPlaysTheSameAnimation_LeavesItFinished()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        player.Play(animation).Then(() => player.Play(animation));

        player.Update(TimeSpan.FromSeconds(0.25));

        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void Then_ThatStops_EndsTheUpdate()
    {
        var animation = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        player.Play(animation).Then(() => player.Stop());

        player.Update(TimeSpan.FromSeconds(0.25));

        Assert.False(player.IsPlaying);
        Assert.Equal(TimeSpan.Zero, player.Time);
    }

    // Playing and seeking

    [Fact]
    public void Actions_OfAnAnimationWithFewerFrames_DontLeakIntoTheNextPlay()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<string>();
        var longer = new SpriteAnimation(Regions(5), 10);
        var shorter = new SpriteAnimation(Regions(2), 10);
        var same = new SpriteAnimation(Regions(5), 10);

        player.Play(longer).When(4, () => entered.Add("longer"));
        player.Play(shorter).When(1, () => entered.Add("shorter"));
        player.Play(same);

        player.Update(TimeSpan.FromSeconds(1));

        Assert.Empty(entered);
    }

    [Fact]
    public void Stop_ThenPlayingTheSameAnimation_StartsAgain_WithTheFirstFramesAction()
    {
        var animation = new SpriteAnimation(Regions(3), 10);
        var player = new SpriteAnimationPlayer(animation);
        player.Update(TimeSpan.FromSeconds(0.15));

        player.Stop();
        int ran = 0;
        player.Play(animation).When(0, () => ran++);
        player.Update(TimeSpan.FromSeconds(0.01));

        Assert.Equal(1, ran);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void Time_Set_OnAFrameBoundary_ShowsThatFrame_WithoutItsAction()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(new SpriteAnimation(Regions(3), 10)).When(1, () => entered.Add(1)).When(2, () => entered.Add(2));

        player.Time = TimeSpan.FromSeconds(0.1);
        Assert.Equal(1, player.FrameIndex);

        player.Update(TimeSpan.FromSeconds(0.05));
        Assert.Empty(entered);

        player.Update(TimeSpan.FromSeconds(0.05));
        Assert.Equal([2], entered);
    }

    [Fact]
    public void Time_Set_PastTheEnd_WrapsOrClamps()
    {
        var player = new SpriteAnimationPlayer();
        var loop = new SpriteAnimation(Regions(4), 10);
        var once = new SpriteAnimation(Regions(4), 10, PlaybackMode.Once);

        player.Play(loop);
        player.Time = TimeSpan.FromSeconds(0.45);
        Assert.Equal(TimeSpan.FromSeconds(0.05), player.Time);

        player.Time = loop.Duration;
        Assert.Equal(TimeSpan.Zero, player.Time);

        player.Play(once);
        player.Time = TimeSpan.FromSeconds(100);
        Assert.Equal(once.Duration, player.Time);
    }
}

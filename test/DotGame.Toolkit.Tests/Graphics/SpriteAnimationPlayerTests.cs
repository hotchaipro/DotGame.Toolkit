// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Animation;
using Xunit;
using static DotGame.Toolkit.Tests.Graphics.SpriteAnimationTests;
using static DotGame.Toolkit.Tests.Graphics.TestRegions;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class SpriteAnimationPlayerTests
{
    private readonly SpriteAnimation _run = new(Regions(4), 10);
    private readonly SpriteAnimation _idle = new(Regions(2), 10);
    private readonly SpriteAnimation _once = new(Regions(3), 10, PlaybackMode.Once);

    [Fact]
    public void Constructor_PlaysTheAnimation()
    {
        var player = new SpriteAnimationPlayer(this._run);

        Assert.True(player.IsPlaying);
        Assert.Same(this._run, player.Animation);
    }

    [Fact]
    public void WithoutAnimation_HasNoFrame()
    {
        var player = new SpriteAnimationPlayer();

        Assert.Null(player.Animation);
        Assert.Null(player.Frame);
        Assert.Equal(-1, player.FrameIndex);
        player.Update(Ms(100));
        Assert.Throws<InvalidOperationException>(() => player.Time = Ms(1));
    }

    [Fact]
    public void Play_WithCurrentAnimationKeepsGoing()
    {
        var player = new SpriteAnimationPlayer(this._run);
        player.Update(Ms(150));

        player.Play(this._run);

        Assert.Equal(1, player.FrameIndex);
    }

    [Fact]
    public void Play_WithRestartStartsOver()
    {
        var player = new SpriteAnimationPlayer(this._run);
        player.Update(Ms(150));

        player.Play(this._run, restart: true);

        Assert.Equal(0, player.FrameIndex);
    }

    [Fact]
    public void Play_WithAnotherAnimationStartsIt()
    {
        var player = new SpriteAnimationPlayer(this._run);
        player.Update(Ms(250));

        player.Play(this._idle);

        Assert.Same(this._idle, player.Animation);
        Assert.Equal(0, player.FrameIndex);
        Assert.Equal(TimeSpan.Zero, player.Time);
    }

    [Fact]
    public void Play_WithCurrentAnimationResumesPausedPlayer()
    {
        var player = new SpriteAnimationPlayer(this._run);
        player.Pause();
        player.Update(Ms(1000));
        Assert.Equal(0, player.FrameIndex);

        player.Play(this._run);

        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void Play_AfterStopPlaysFromStart()
    {
        var player = new SpriteAnimationPlayer(this._run);
        player.Update(Ms(250));

        player.Stop();
        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.FrameIndex);

        player.Play(this._run);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void Once_FinishesAndHoldsLastFrame()
    {
        var player = new SpriteAnimationPlayer(this._once);

        player.Update(Ms(250));
        Assert.False(player.IsFinished);

        player.Update(Ms(100));
        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
        Assert.Equal(2, player.FrameIndex);
        Assert.Equal(Ms(300), player.Time);
    }

    [Fact]
    public void Once_PlayWithFinishedAnimationStaysFinished()
    {
        var player = new SpriteAnimationPlayer(this._once);
        player.Update(Ms(1000));

        player.Play(this._once);

        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void Once_ResumeRestartsFinishedAnimation()
    {
        var player = new SpriteAnimationPlayer(this._once);
        player.Update(Ms(1000));

        player.Resume();

        Assert.True(player.IsPlaying);
        Assert.False(player.IsFinished);
        Assert.Equal(0, player.FrameIndex);
    }

    [Fact]
    public void Then_RunsOnceWhenPlayFinishes()
    {
        var player = new SpriteAnimationPlayer();
        int finished = 0;
        player.Play(this._once).Then(() => finished++);

        player.Update(Ms(250));
        Assert.Equal(0, finished);

        player.Update(Ms(100));
        Assert.Equal(1, finished);

        player.Update(Ms(1000));
        Assert.Equal(1, finished);
    }

    [Fact]
    public void Then_SetAfterPlayFinishedDoesNotRun()
    {
        var player = new SpriteAnimationPlayer();
        var handle = player.Play(this._once);
        player.Update(Ms(1000));
        int finished = 0;

        handle.Then(() => finished++);
        player.Play(this._once).Then(() => finished++);
        player.Update(Ms(1000));

        Assert.Equal(0, finished);
    }

    [Fact]
    public void Then_ReplacesEarlierThenForSamePlay()
    {
        var player = new SpriteAnimationPlayer();
        var ran = new List<string>();
        player.Play(this._once).Then(() => ran.Add("first"));
        player.Play(this._once).Then(() => ran.Add("second"));

        player.Update(Ms(1000));

        Assert.Equal(["second"], ran);
    }

    [Fact]
    public void Then_SetEveryFrameRunsOnce()
    {
        var player = new SpriteAnimationPlayer();
        int finished = 0;

        for (int i = 0; i < 10; i++)
        {
            player.Play(this._once).Then(() => finished++);
            player.Update(Ms(50));
        }

        Assert.Equal(1, finished);
    }

    [Fact]
    public void Then_AttachedThroughRepeatedPlayBelongsToSamePlay()
    {
        var player = new SpriteAnimationPlayer(this._once);
        player.Update(Ms(100));
        int finished = 0;

        player.Play(this._once).Then(() => finished++);
        player.Update(Ms(1000));

        Assert.Equal(1, finished);
    }

    [Fact]
    public void Then_DoesNotRunWhenInterrupted()
    {
        int finished = 0;
        var player = new SpriteAnimationPlayer();

        player.Play(this._once).Then(() => finished++);
        player.Play(this._idle);
        player.Play(this._once).Then(() => finished++);
        player.Play(this._once, restart: true);
        player.Update(Ms(1000));
        Assert.Equal(0, finished);

        player.Play(this._once, restart: true).Then(() => finished++);
        player.Stop();
        player.Play(this._once);
        player.Update(Ms(1000));
        Assert.Equal(0, finished);
    }

    [Fact]
    public void Then_FromInterruptedPlayIsIgnoredLater()
    {
        var player = new SpriteAnimationPlayer();
        var stale = player.Play(this._once);
        player.Play(this._idle);
        player.Play(this._once);
        int finished = 0;

        stale.Then(() => finished++);
        player.Update(Ms(1000));

        Assert.Equal(0, finished);
    }

    [Fact]
    public void Then_NeverRunsForLoopingAnimation()
    {
        var player = new SpriteAnimationPlayer();
        int finished = 0;
        player.Play(this._run).Then(() => finished++);

        player.Update(Ms(10_000));

        Assert.Equal(0, finished);
    }

    [Fact]
    public void Then_PlayCarriesLeftoverTime()
    {
        var player = new SpriteAnimationPlayer();
        player.Play(this._once).Then(() => player.Play(this._run));

        player.Update(Ms(250));
        Assert.Same(this._once, player.Animation);

        player.Update(Ms(170));
        Assert.Same(this._run, player.Animation);
        Assert.Equal(Ms(120), player.Time);
        Assert.Equal(1, player.FrameIndex);
        Assert.True(player.IsPlaying);
        Assert.False(player.IsFinished);
    }

    [Fact]
    public void Then_ChainsAcrossSeveralPlaysInOneUpdate()
    {
        var first = new SpriteAnimation(Regions(3), 10, PlaybackMode.Once);
        var second = new SpriteAnimation(Regions(2), 10, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        int cycles = 0;

        void PlayCycle()
        {
            player.Play(first).Then(() => player.Play(second).Then(() =>
            {
                cycles++;
                PlayCycle();
            }));
        }

        PlayCycle();

        // A cycle lasts 500 ms; 1,120 ms is two cycles and 120 ms into the first animation.
        player.Update(Ms(1120));

        Assert.Equal(2, cycles);
        Assert.Same(first, player.Animation);
        Assert.Equal(Ms(120), player.Time);
    }

    [Fact]
    public void Then_StopsChainingAfterTooManyPlaysInOneUpdate()
    {
        var blink = new SpriteAnimation(Regions(1), 1000, PlaybackMode.Once);
        var player = new SpriteAnimationPlayer();
        int plays = 0;

        void PlayBlink()
        {
            plays++;
            player.Play(blink, restart: true).Then(PlayBlink);
        }

        PlayBlink();
        player.Update(TimeSpan.FromHours(1));

        Assert.InRange(plays, 2, 100);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void When_RunsEachTimeFrameIsEntered()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        player.Play(this._run).When(1, () => entered.Add(1)).When(3, () => entered.Add(3));

        for (int i = 0; i < 16; i++)
        {
            player.Update(Ms(50));
        }

        // 800 ms is two loops.
        Assert.Equal([1, 3, 1, 3], entered);
    }

    [Fact]
    public void When_FirstFrameRunsOnNextUpdate()
    {
        var player = new SpriteAnimationPlayer();
        int entered = 0;
        player.Play(this._run).When(0, () => entered++);
        Assert.Equal(0, entered);

        player.Update(TimeSpan.Zero);
        Assert.Equal(1, entered);

        player.Update(Ms(10));
        Assert.Equal(1, entered);
    }

    [Fact]
    public void When_RunsForFramesSkippedOnScreenInOrder()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        var handle = player.Play(this._run);
        for (int i = 1; i < 4; i++)
        {
            int frame = i;
            handle.When(frame, () => entered.Add(frame));
        }

        player.Update(Ms(10));
        player.Update(Ms(350));

        Assert.Equal([1, 2, 3], entered);
        Assert.Equal(3, player.FrameIndex);
    }

    [Fact]
    public void When_RunsAtMostOnePassPerUpdate()
    {
        var player = new SpriteAnimationPlayer();
        int entered = 0;
        player.Play(this._run).When(1, () => entered++);
        player.Update(Ms(10));

        player.Update(TimeSpan.FromMinutes(10));

        Assert.Equal(1, entered);
    }

    [Fact]
    public void When_RunsTwicePerPassForMiddleFramesOfPingPong()
    {
        var pingPong = new SpriteAnimation(Regions(4), 10, PlaybackMode.PingPong);
        var player = new SpriteAnimationPlayer();
        int entered = 0;
        player.Play(pingPong).When(2, () => entered++);

        // One pass, 0 1 2 3 2 1, is 600 ms.
        for (int i = 0; i < 12; i++)
        {
            player.Update(Ms(50));
        }

        Assert.Equal(2, entered);
    }

    [Fact]
    public void When_ReplacesEarlierActionForSameFrame()
    {
        var player = new SpriteAnimationPlayer();
        var ran = new List<string>();
        player.Play(this._run).When(1, () => ran.Add("first"));
        player.Play(this._run).When(1, () => ran.Add("second"));

        player.Update(Ms(150));

        Assert.Equal(["second"], ran);
    }

    [Fact]
    public void When_SetEveryFrameDoesNotAccumulate()
    {
        var player = new SpriteAnimationPlayer();
        int entered = 0;

        for (int i = 0; i < 16; i++)
        {
            player.Play(this._run).When(1, () => entered++);
            player.Update(Ms(50));
        }

        Assert.Equal(2, entered);
    }

    [Fact]
    public void When_LastFrameRunsBeforeThen()
    {
        var player = new SpriteAnimationPlayer();
        var events = new List<string>();
        player.Play(this._once).When(2, () => events.Add("last frame")).Then(() => events.Add("finished"));

        player.Update(Ms(1000));

        Assert.Equal(["last frame", "finished"], events);
    }

    [Fact]
    public void When_OfAnimationPlayedFromThenRunsInSameUpdate()
    {
        var player = new SpriteAnimationPlayer();
        var events = new List<string>();
        player.Play(this._once).Then(() =>
        {
            events.Add("finished");
            player.Play(this._run).When(1, () => events.Add("run frame 1"));
        });

        player.Update(Ms(420));

        Assert.Equal(["finished", "run frame 1"], events);
    }

    [Fact]
    public void When_ActionThatPlaysAnotherAnimationInterruptsThisOne()
    {
        var player = new SpriteAnimationPlayer();
        int later = 0;
        player.Play(this._run)
            .When(1, () => player.Play(this._idle))
            .When(2, () => later++);

        player.Update(Ms(250));

        Assert.Same(this._idle, player.Animation);
        Assert.Equal(TimeSpan.Zero, player.Time);
        Assert.Equal(0, later);
    }

    [Fact]
    public void When_IsDroppedWhenInterrupted()
    {
        var player = new SpriteAnimationPlayer();
        int entered = 0;
        player.Play(this._run).When(0, () => entered++).When(1, () => entered++);

        player.Play(this._idle);
        player.Update(Ms(150));
        player.Play(this._run);
        player.Update(Ms(150));

        Assert.Equal(0, entered);
    }

    [Fact]
    public void When_SeekingRunsNoActionsForSkippedFrames()
    {
        var player = new SpriteAnimationPlayer();
        var entered = new List<int>();
        var handle = player.Play(this._run);
        for (int i = 0; i < 4; i++)
        {
            int frame = i;
            handle.When(frame, () => entered.Add(frame));
        }

        player.Time = Ms(250);
        player.Update(Ms(10));
        Assert.Empty(entered);

        player.Update(Ms(50));
        Assert.Equal([3], entered);
    }

    [Fact]
    public void When_SetAfterPlayFinishedIsIgnored()
    {
        var player = new SpriteAnimationPlayer();
        var handle = player.Play(this._once);
        player.Update(Ms(1000));

        handle.When(5, () => { });
    }

    [Fact]
    public void When_RejectsOutOfRangeFrame()
    {
        var player = new SpriteAnimationPlayer();
        var handle = player.Play(this._run);

        Assert.Throws<ArgumentOutOfRangeException>(() => handle.When(-1, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => handle.When(4, () => { }));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("play another")]
    [InlineData("restart")]
    public void Interrupting_ReleasesActionsForGarbageCollection(
        string interruption)
    {
        var player = new SpriteAnimationPlayer();
        var captured = SetActionsCapturingAnObject(player, this._once);

        switch (interruption)
        {
            case "stop":
                player.Stop();
                break;
            case "play another":
                player.Play(this._idle);
                break;
            default:
                player.Play(this._once, restart: true);
                break;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(captured.IsAlive);
        GC.KeepAlive(player);
    }

    [Fact]
    public void Handle_DefaultDoesNothing()
    {
        var handle = default(SpriteAnimationHandle);

        handle.When(0, () => throw new InvalidOperationException()).Then(() => throw new InvalidOperationException());
    }

    [Fact]
    public void Speed_ScalesTime()
    {
        var player = new SpriteAnimationPlayer(this._run) { Speed = 0.5f };

        player.Update(Ms(300));

        Assert.Equal(Ms(150), player.Time);
        Assert.Equal(1, player.FrameIndex);
    }

    [Fact]
    public void Speed_ZeroHoldsButKeepsPlaying()
    {
        var player = new SpriteAnimationPlayer(this._run) { Speed = 0 };

        player.Update(Ms(1000));

        Assert.True(player.IsPlaying);
        Assert.Equal(0, player.FrameIndex);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Speed_RejectsInvalidValues(
        float speed)
    {
        var player = new SpriteAnimationPlayer();

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Speed = speed);
    }

    [Fact]
    public void Time_SeekWrapsForLoop()
    {
        var player = new SpriteAnimationPlayer(this._run) { Time = Ms(450) };

        Assert.Equal(Ms(50), player.Time);
    }

    [Fact]
    public void Time_SeekToRandomStart()
    {
        var player = new SpriteAnimationPlayer(this._run) { Time = this._run.Duration * 0.75 };

        Assert.Equal(3, player.FrameIndex);
    }

    [Fact]
    public void Time_SeekToEndFinishesOnNextUpdate()
    {
        var player = new SpriteAnimationPlayer();
        int finished = 0;
        player.Play(this._once).Then(() => finished++);

        player.Time = Ms(5000);
        Assert.Equal(Ms(300), player.Time);
        Assert.False(player.IsFinished);
        Assert.Equal(0, finished);

        player.Update(Ms(1));
        Assert.True(player.IsFinished);
        Assert.Equal(1, finished);
    }

    [Fact]
    public void Time_SeekBackIntoFinishedAnimationLeavesItPaused()
    {
        var player = new SpriteAnimationPlayer(this._once);
        player.Update(Ms(1000));

        player.Time = Ms(150);

        Assert.False(player.IsFinished);
        Assert.False(player.IsPlaying);
        Assert.Equal(1, player.FrameIndex);
    }

    // Not inlined, so no local in the test keeps the captured object alive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference SetActionsCapturingAnObject(
        SpriteAnimationPlayer player,
        SpriteAnimation animation)
    {
        var view = new object();
        player.Play(animation)
            .When(1, () => GC.KeepAlive(view))
            .Then(() => GC.KeepAlive(view));
        return new WeakReference(view);
    }

    [Fact]
    public void Time_RejectsNegativeValues()
    {
        var player = new SpriteAnimationPlayer(this._run);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Time = Ms(-1));
    }
}

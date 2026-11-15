// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

using DotGame.Toolkit.Graphics;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Plays a <see cref="SpriteAnimation"/>: the playback state of one animated
/// thing, such as a character.
/// </summary>
/// <remarks>
/// <para>
/// Animations are shared; each animated thing has its own player. The player
/// is a plain object: call <see cref="Update"/> once per frame with the
/// elapsed game time, and draw <see cref="Frame"/> (or use the
/// <c>SpriteBatch.Draw</c> overload that takes a player).
/// </para>
/// <para>
/// <see cref="Play"/> returns a <see cref="SpriteAnimationHandle"/> for that
/// play: <see cref="SpriteAnimationHandle.When"/> sets an action for a frame,
/// such as a footstep or a hit, and <see cref="SpriteAnimationHandle.Then"/>
/// sets what happens when the play finishes, such as playing the next
/// animation. Both are dropped when the play is interrupted, so there's
/// nothing to unsubscribe.
/// </para>
/// </remarks>
public sealed class SpriteAnimationPlayer
{
    // When a single update finishes this many plays in a row, each started
    // from the last one's Then, the rest of the elapsed time is dropped, so a
    // chain of very short animations can't spin for a long update.
    private const int MaxChainedPlaysPerUpdate = 64;

    private SpriteAnimation? _animation;
    private long _ticks;
    private float _speed = 1;

    // Identifies the current play; a When or Then set through an earlier
    // play's handle is ignored.
    private int _run;

    // The current play's one continuation slot.
    private Action? _then;

    // The current play's frame actions: one slot per frame, indexed by frame.
    // Allocated on the first When and cleared, not reallocated, by later
    // plays of an animation with as many frames.
    private Action?[]? _frameActions;
    private int _frameActionCount;

    // How far past the time an action paused the play: its frame has been entered
    private long _enteredAhead;

    // Whether the next update must also run the action for the frame the play
    // starts on, which playback enters at time zero.
    private bool _isStartFramePending;

    /// <summary>
    /// Creates a player, playing <paramref name="animation"/> if one is given.
    /// </summary>
    public SpriteAnimationPlayer(
        SpriteAnimation? animation = null)
    {
        if (animation is not null)
        {
            this.Play(animation);
        }
    }

    /// <summary>
    /// Gets the animation being played, or <c>null</c> for none.
    /// </summary>
    public SpriteAnimation? Animation => this._animation;

    /// <summary>
    /// Gets or sets the time since the animation started, scaled by
    /// <see cref="Speed"/>. Setting it seeks: the time wraps for looping
    /// animations and is clamped to <see cref="SpriteAnimation.Duration"/> for
    /// <see cref="PlaybackMode.Once"/>.
    /// </summary>
    /// <remarks>
    /// Seeking only moves the time. It runs no
    /// <see cref="SpriteAnimationHandle.When"/> actions for the frames it skips
    /// or lands on; the next <see cref="Update"/> runs them for frames it
    /// enters after that. A playing <see cref="PlaybackMode.Once"/>
    /// animation sought to its end finishes, and runs its
    /// <see cref="SpriteAnimationHandle.Then"/>, on the next update.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The time is negative.</exception>
    /// <exception cref="InvalidOperationException">There is no animation.</exception>
    public TimeSpan Time
    {
        get => new TimeSpan(this._ticks);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);

            var animation = this._animation
                ?? throw new InvalidOperationException("The player has no animation to seek in.");

            this._ticks = animation.NormalizeTicks(value.Ticks);
            this._enteredAhead = 0;
            this.IsFinished = false;
            this._isStartFramePending = false;
        }
    }

    /// <summary>
    /// Gets the index of the frame showing now, or -1 when there is no
    /// animation.
    /// </summary>
    public int FrameIndex => this._animation?.GetFrameIndex(new TimeSpan(this._ticks)) ?? -1;

    /// <summary>
    /// Gets the region of the frame showing now, or <c>null</c> when there is
    /// no animation.
    /// </summary>
    public TextureRegion? Frame => this._animation?.GetFrame(new TimeSpan(this._ticks));

    /// <summary>
    /// Gets or sets the playback rate: 1 is normal, 0.5 half speed, 2 double.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The speed is negative or not finite.</exception>
    public float Speed
    {
        get => this._speed;
        set
        {
            if (!float.IsFinite(value) || (value < 0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The speed must be zero or positive, and finite.");
            }

            this._speed = value;
        }
    }

    /// <summary>
    /// Gets whether the animation advances on <see cref="Update"/>.
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Gets whether a <see cref="PlaybackMode.Once"/> animation has
    /// reached its end. It keeps showing its last frame.
    /// </summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Plays an animation from its start, or keeps playing it if it's already
    /// the current animation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calling this every frame with the animation the game state calls for
    /// (<c>Play(isMoving ? run : idle)</c>) is the intended use: it only
    /// restarts when the animation changes. With the current animation, a
    /// paused player resumes, a finished one stays finished, and the handle is
    /// the current play's.
    /// </para>
    /// <para>
    /// Starting a play interrupts the previous one: its
    /// <see cref="SpriteAnimationHandle.When"/> and
    /// <see cref="SpriteAnimationHandle.Then"/> actions are dropped without
    /// running.
    /// </para>
    /// </remarks>
    /// <param name="animation">The animation to play.</param>
    /// <param name="restart">True to start the animation over even if it's the current one.</param>
    /// <returns>A handle for setting actions for this play.</returns>
    public SpriteAnimationHandle Play(
        SpriteAnimation animation,
        bool restart = false)
    {
        ArgumentNullException.ThrowIfNull(animation);

        if ((animation == this._animation) && (!restart))
        {
            if (!this.IsFinished)
            {
                this.IsPlaying = true;
            }

            return new SpriteAnimationHandle(this, this._run, isNewPlay: false);
        }

        this._animation = animation;
        this.StartRun();
        this.IsPlaying = true;

        return new SpriteAnimationHandle(this, this._run, isNewPlay: true);
    }

    /// <summary>
    /// Continues the current animation, or plays it again from the start if
    /// it has finished (a new play, without the old one's actions).
    /// </summary>
    public void Resume()
    {
        if (this._animation is null)
        {
            return;
        }

        if (this.IsFinished)
        {
            this.StartRun();
        }

        this.IsPlaying = true;
    }

    /// <summary>
    /// Pauses the animation on its current frame. The play and its actions
    /// are kept.
    /// </summary>
    public void Pause()
    {
        this.IsPlaying = false;
    }

    /// <summary>
    /// Stops the animation and returns it to its first frame. This interrupts
    /// the play: its <see cref="SpriteAnimationHandle.When"/> and
    /// <see cref="SpriteAnimationHandle.Then"/> actions are dropped without
    /// running.
    /// </summary>
    public void Stop()
    {
        this.StartRun();
        this.IsPlaying = false;
    }

    /// <summary>
    /// Advances the animation by the elapsed game time, running the
    /// <see cref="SpriteAnimationHandle.When"/> actions of the frames it
    /// enters and the <see cref="SpriteAnimationHandle.Then"/> of a play that
    /// finishes. Call once per frame.
    /// </summary>
    /// <param name="elapsed">The game time since the last update.</param>
    public void Update(
        TimeSpan elapsed)
    {
        if ((!this.IsPlaying) || (this._animation is null))
        {
            return;
        }

        long delta = (elapsed <= TimeSpan.Zero)
            ? 0
            : (this._speed == 1)
                ? elapsed.Ticks
                : (long)(elapsed.Ticks * (double)this._speed);

        for (int plays = 0; plays < MaxChainedPlaysPerUpdate; plays++)
        {
            int run = this._run;

            // Frame actions run before the time moves, so one that starts
            // another play interrupts this one where it is.
            this.RunFrameActions(delta);
            if ((this._run != run) || (!this.IsPlaying))
            {
                break;
            }

            if (!this.Advance(delta, out long leftover))
            {
                break;
            }

            // The play finished: run its Then. An animation played from
            // there starts with the leftover time.
            this.RunThen();
            if ((this._run == run) || (!this.IsPlaying))
            {
                break;
            }

            delta = leftover;
        }
    }

    internal void When(
        int run,
        int frameIndex,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // A handle from an interrupted play, or a play that has already
        // finished, has no frames left to enter.
        if ((run != this._run) || (this.IsFinished))
        {
            return;
        }

        int frameCount = this._animation!.Frames.Count;
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, frameCount);

        var actions = this._frameActions;
        if ((actions is null) || (actions.Length != frameCount))
        {
            actions = new Action?[frameCount];
            this._frameActions = actions;
        }

        if (actions[frameIndex] is null)
        {
            this._frameActionCount++;
        }

        actions[frameIndex] = action;
    }

    internal void Then(
        int run,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // A handle from an interrupted play, or a play that has already
        // finished (whose Then has run), has nothing left to continue.
        if ((run != this._run) || (this.IsFinished))
        {
            return;
        }

        this._then = action;
    }

    // Starts a new play of the current animation from its first frame,
    // dropping the previous play's actions.
    private void StartRun()
    {
        this._run++;
        this._ticks = 0;
        this._enteredAhead = 0;
        this.IsFinished = false;
        this._then = null;
        this._isStartFramePending = true;

        if (this._frameActionCount > 0)
        {
            Array.Clear(this._frameActions!);
            this._frameActionCount = 0;
        }
    }

    // Runs the actions of the frames playback enters while advancing by
    // delta, in order: the first frame of the play, then every frame whose
    // start the time passes, including frames an update skips over on screen.
    // An update runs at most one pass's worth, so a very long one (resuming
    // after the game was in the background) can't run a looping animation's
    // actions over and over.
    private void RunFrameActions(
        long delta)
    {
        bool fromStart = this._isStartFramePending;
        this._isStartFramePending = false;

        if (this._frameActionCount == 0)
        {
            return;
        }

        var animation = this._animation!;
        var actions = this._frameActions!;
        var starts = animation.PassStarts;
        var frames = animation.PassFrames;
        long pass = animation.PassTicks;
        bool once = animation.Playback == PlaybackMode.Once;

        // Frames entered in (from, to]; a frame is entered at its start time.
        long from = fromStart ? -1 : (this._ticks + this._enteredAhead);
        long to = this._ticks + delta;
        this._enteredAhead = Math.Max(0, from - to);

        if ((!once) && ((to - from) > pass))
        {
            from = to - pass;
        }

        long passIndex = (from < 0) ? 0 : (from / pass);
        long offset = from - (passIndex * pass);
        int index = Array.BinarySearch(starts, offset);
        index = (index >= 0) ? (index + 1) : ~index;

        int run = this._run;
        while (true)
        {
            if (index == starts.Length)
            {
                if (once)
                {
                    return;
                }

                index = 0;
                passIndex++;
            }

            if (((passIndex * pass) + starts[index]) > to)
            {
                return;
            }

            long entered = (passIndex * pass) + starts[index];
            actions[frames[index]]?.Invoke();

            // An action that starts another play, or stops or pauses this
            // one, ends this play's actions for the update.
            if (this._run != run)
            {
                return;
            }

            if (!this.IsPlaying)
            {
                // The time doesn't move past a pause, so remember this frame's been entered
                this._enteredAhead = Math.Max(0, entered - this._ticks);
                return;
            }

            index++;
        }
    }

    // Advances the time. Returns whether a Once animation finished, and the
    // time left over past its end.
    private bool Advance(
        long delta,
        out long leftover)
    {
        var animation = this._animation!;
        leftover = 0;

        if (animation.Playback != PlaybackMode.Once)
        {
            this._ticks = animation.NormalizeTicks(this._ticks + delta);
            return false;
        }

        long ticks = this._ticks + delta;
        long duration = animation.PassTicks;

        if (ticks < duration)
        {
            this._ticks = ticks;
            return false;
        }

        leftover = ticks - duration;
        this._ticks = duration;
        this.IsFinished = true;
        this.IsPlaying = false;
        return true;
    }

    private void RunThen()
    {
        var then = this._then;
        this._then = null;
        then?.Invoke();
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DotGame.Framework;

using DotGame.Toolkit.Graphics;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// A frame-by-frame sprite animation: a sequence of texture regions and their
/// timing.
/// </summary>
/// <remarks>
/// <para>
/// An animation is immutable and holds no playback state, so any number of
/// <see cref="SpriteAnimationPlayer"/>s can share one. Its regions belong to
/// whatever owns their texture (typically a <see cref="TextureAtlas"/>), so
/// disposing that invalidates the animation.
/// </para>
/// <para>
/// Every frame has the same logical size (<see cref="TextureRegion.Size"/>):
/// one canvas, redrawn. Trimmed atlas regions keep their logical size, so
/// frames trimmed differently still share an origin.
/// </para>
/// </remarks>
public sealed class SpriteAnimation
{
    private readonly SpriteAnimationFrame[] _frames;

    // The time at which each frame ends, in ticks from the start of a forward
    // pass. Computed from the running sum of holds, so rounding never
    // accumulates across frames.
    private readonly long[] _frameEnds;

    // The length of one forward pass through the frames, in ticks.
    private readonly long _forwardTicks;

    // The length of one pass in ticks: forward and back for PingPong.
    private readonly long _passTicks;

    // Every frame entered during one pass, in order, and when it's entered,
    // in ticks from the start of the pass: frames 0 to n-1, then n-2 down to
    // 1 for PingPong. Lets a player find the frames an update passes through.
    private readonly long[] _passStarts;
    private readonly int[] _passFrames;

    /// <summary>
    /// Creates an animation whose frames each show for one frame at
    /// <paramref name="framesPerSecond"/>.
    /// </summary>
    /// <param name="frames">The frames, in order. Must not be empty, and every region must have the same size.</param>
    /// <param name="framesPerSecond">The frame rate. Must be positive and finite.</param>
    /// <param name="playback">How the animation plays through its frames.</param>
    public SpriteAnimation(
        IReadOnlyList<TextureRegion> frames,
        float framesPerSecond,
        PlaybackMode playback = PlaybackMode.Loop)
        : this(ToFrames(frames), framesPerSecond, playback)
    {
    }

    /// <summary>
    /// Creates an animation whose frames each show for their
    /// <see cref="SpriteAnimationFrame.Hold"/> at
    /// <paramref name="framesPerSecond"/>.
    /// </summary>
    /// <param name="frames">The frames, in order. Must not be empty, and every region must have the same size.</param>
    /// <param name="framesPerSecond">The frame rate. Must be positive and finite.</param>
    /// <param name="playback">How the animation plays through its frames.</param>
    public SpriteAnimation(
        IReadOnlyList<SpriteAnimationFrame> frames,
        float framesPerSecond,
        PlaybackMode playback = PlaybackMode.Loop)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Count == 0)
        {
            throw new ArgumentException("A sprite animation must have at least one frame.", nameof(frames));
        }

        if (!float.IsFinite(framesPerSecond) || (framesPerSecond <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond), "The frame rate must be positive and finite.");
        }

        if ((playback < PlaybackMode.Loop) || (playback > PlaybackMode.PingPong))
        {
            throw new ArgumentOutOfRangeException(nameof(playback));
        }

        var copy = new SpriteAnimationFrame[frames.Count];
        var frameEnds = new long[frames.Count];
        Vector2Int size = default;
        double holds = 0;
        long previousEnd = 0;

        for (int i = 0; i < copy.Length; i++)
        {
            var frame = frames[i];

            if (frame.Region is null)
            {
                throw new ArgumentException($"Frame {i} has no region.", nameof(frames));
            }

            if (!float.IsFinite(frame.Hold) || (frame.Hold <= 0))
            {
                throw new ArgumentException($"Frame {i} has a hold of {frame.Hold}; it must be positive and finite.", nameof(frames));
            }

            if (i == 0)
            {
                size = frame.Region.Size;
            }
            else if (frame.Region.Size != size)
            {
                throw new ArgumentException(
                    $"Frame {i} is {frame.Region.Size.X}x{frame.Region.Size.Y}, but frame 0 is {size.X}x{size.Y}; every frame of a sprite animation must have the same logical size.",
                    nameof(frames));
            }

            holds += frame.Hold;
            long end = (long)Math.Round(holds * TimeSpan.TicksPerSecond / framesPerSecond);

            if (end <= previousEnd)
            {
                throw new ArgumentException($"Frame {i} is too short to show at {framesPerSecond} frames per second.", nameof(frames));
            }

            copy[i] = frame;
            frameEnds[i] = end;
            previousEnd = end;
        }

        this._frames = copy;
        this._frameEnds = frameEnds;
        this._forwardTicks = previousEnd;

        // The backward leg of a ping-pong plays frames n-2 down to 1, which
        // together last as long as the forward pass minus its first and last
        // frames.
        this._passTicks = ((playback == PlaybackMode.PingPong) && (copy.Length > 2))
            ? (2 * previousEnd) - frameEnds[0] - (previousEnd - frameEnds[^2])
            : previousEnd;

        (this._passStarts, this._passFrames) = BuildPass(frameEnds, playback);

        this.Frames = new ReadOnlyCollection<SpriteAnimationFrame>(copy);
        this.FramesPerSecond = framesPerSecond;
        this.Playback = playback;
        this.Size = size;
    }

    /// <summary>
    /// Gets the frames, in order.
    /// </summary>
    public IReadOnlyList<SpriteAnimationFrame> Frames { get; }

    /// <summary>
    /// Gets the frame rate. A frame shows for its
    /// <see cref="SpriteAnimationFrame.Hold"/> divided by the frame rate.
    /// </summary>
    public float FramesPerSecond { get; }

    /// <summary>
    /// Gets how the animation plays through its frames.
    /// </summary>
    public PlaybackMode Playback { get; }

    /// <summary>
    /// Gets the length of one pass through the frames: forward and back for
    /// <see cref="PlaybackMode.PingPong"/>.
    /// </summary>
    public TimeSpan Duration => new TimeSpan(this._passTicks);

    /// <summary>
    /// Gets the logical size every frame shares; use it for layout and origins.
    /// </summary>
    public Vector2Int Size { get; }

    /// <summary>
    /// Gets how long a frame shows: its <see cref="SpriteAnimationFrame.Hold"/>
    /// divided by <see cref="FramesPerSecond"/>.
    /// </summary>
    /// <param name="index">The frame's index in <see cref="Frames"/>.</param>
    public TimeSpan GetFrameDuration(
        int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, this._frames.Length);

        long start = (index == 0) ? 0 : this._frameEnds[index - 1];
        return new TimeSpan(this._frameEnds[index] - start);
    }

    /// <summary>
    /// Gets the index of the frame that shows at a time since the animation
    /// started. Has no state, so things that animate in lockstep can share one
    /// clock instead of each having a player.
    /// </summary>
    /// <param name="time">
    /// The time since the animation started. Negative times show the first
    /// frame; times past the end of a <see cref="PlaybackMode.Once"/>
    /// animation show its last frame.
    /// </param>
    public int GetFrameIndex(
        TimeSpan time)
    {
        return this.GetFrameIndex(time.Ticks);
    }

    // NOTE: For a particle renderer, which asks per particle per frame
    internal TextureRegion GetRegion(
        int frameIndex)
    {
        return this._frames[frameIndex].Region;
    }

    internal int GetFrameIndex(
        long ticks)
    {
        if (ticks <= 0)
        {
            return 0;
        }

        switch (this.Playback)
        {
            case PlaybackMode.Once:
                return (ticks >= this._forwardTicks) ? (this._frames.Length - 1) : this.FindForwardIndex(ticks);

            case PlaybackMode.PingPong:
                ticks %= this._passTicks;
                if (ticks < this._forwardTicks)
                {
                    return this.FindForwardIndex(ticks);
                }

                // On the way back: mirror the time into the forward pass, from
                // the end of frame n-2 back toward the start of frame 1.
                return this.FindForwardIndex(this._frameEnds[^2] - 1 - (ticks - this._forwardTicks));

            default:
                return this.FindForwardIndex(ticks % this._forwardTicks);
        }
    }

    /// <summary>
    /// Gets the region of the frame that shows at a time since the animation
    /// started; see <see cref="GetFrameIndex(System.TimeSpan)"/>.
    /// </summary>
    public TextureRegion GetFrame(
        TimeSpan time)
    {
        return this._frames[this.GetFrameIndex(time)].Region;
    }

    /// <summary>
    /// Wraps or clamps a time since the animation started into one pass:
    /// wrapped for looping playback, clamped to <see cref="Duration"/> for
    /// <see cref="PlaybackMode.Once"/>.
    /// </summary>
    internal long NormalizeTicks(
        long ticks)
    {
        if (ticks <= 0)
        {
            return 0;
        }

        if (this.Playback == PlaybackMode.Once)
        {
            return Math.Min(ticks, this._passTicks);
        }

        return ticks % this._passTicks;
    }

    internal long PassTicks => this._passTicks;

    internal long[] PassStarts => this._passStarts;

    internal int[] PassFrames => this._passFrames;

    private static (long[] Starts, int[] Frames) BuildPass(
        long[] frameEnds,
        PlaybackMode playback)
    {
        int count = frameEnds.Length;
        int backward = ((playback == PlaybackMode.PingPong) && (count > 2)) ? (count - 2) : 0;
        var starts = new long[count + backward];
        var frames = new int[count + backward];

        for (int i = 0; i < count; i++)
        {
            starts[i] = (i == 0) ? 0 : frameEnds[i - 1];
            frames[i] = i;
        }

        // The way back: frames n-2 down to 1, each lasting as long as it did
        // going forward.
        long time = frameEnds[^1];
        for (int i = 0; i < backward; i++)
        {
            int frame = count - 2 - i;
            starts[count + i] = time;
            frames[count + i] = frame;
            time += frameEnds[frame] - frameEnds[frame - 1];
        }

        return (starts, frames);
    }

    // Finds the frame showing at a time within the forward pass: the first
    // frame that ends after it.
    private int FindForwardIndex(
        long ticks)
    {
        int index = Array.BinarySearch(this._frameEnds, ticks);

        // An exact match is the end of that frame, so the next one shows.
        // Otherwise the complement is the first end greater than ticks.
        index = (index >= 0) ? (index + 1) : ~index;

        return Math.Min(index, this._frames.Length - 1);
    }

    private static SpriteAnimationFrame[] ToFrames(
        IReadOnlyList<TextureRegion> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var result = new SpriteAnimationFrame[frames.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new SpriteAnimationFrame(frames[i]);
        }

        return result;
    }
}

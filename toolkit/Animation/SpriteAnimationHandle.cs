// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// One play of an animation on a <see cref="SpriteAnimationPlayer"/>, returned
/// by <see cref="SpriteAnimationPlayer.Play"/>, for setting actions on its
/// frames and for when it finishes.
/// </summary>
/// <remarks>
/// <para>
/// A play has one action slot per frame (<see cref="When"/>) and one for when
/// it finishes (<see cref="Then"/>); setting a slot again replaces it. Another
/// <see cref="SpriteAnimationPlayer.Play"/> of a different animation, a
/// restart, or <see cref="SpriteAnimationPlayer.Stop"/> interrupts the play
/// and empties its slots without running them, so a chain built on it stops by
/// itself and nothing needs to be unsubscribed. A handle from an interrupted
/// play does nothing.
/// </para>
/// <para>
/// Actions run synchronously inside <see cref="SpriteAnimationPlayer.Update"/>.
/// </para>
/// </remarks>
public readonly struct SpriteAnimationHandle
{
    private readonly SpriteAnimationPlayer? _player;
    private readonly int _run;

    internal SpriteAnimationHandle(
        SpriteAnimationPlayer player,
        int run,
        bool isNewPlay)
    {
        this._player = player;
        this._run = run;
        this.IsNewPlay = isNewPlay;
    }

    /// <summary>
    /// Whether <see cref="SpriteAnimationPlayer.Play"/> started this play, rather than finding it
    /// already going. A game that calls <c>Play</c> every frame can set its actions only then,
    /// instead of allocating a closure each frame:
    /// <code>
    /// var play = player.Play(isMoving ? run : idle);
    /// if (play.IsNewPlay) { play.Then(() => ...); }
    /// </code>
    /// </summary>
    public bool IsNewPlay { get; }

    /// <summary>
    /// Sets the action to run whenever this play enters a frame, such as a
    /// footstep or a hit, replacing any set before for that frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The action runs every time playback enters the frame: on each loop of
    /// a looping animation, and twice per pass for a middle frame of a
    /// ping-pong. It runs even when an update skips over the frame on screen,
    /// so a hit frame isn't lost to a slow frame; the frames an update enters
    /// run in order, at most one pass's worth per update.
    /// </para>
    /// <para>
    /// The play's first frame is entered when it starts, so its action runs on
    /// the next <see cref="SpriteAnimationPlayer.Update"/>. The last frame of a
    /// <see cref="PlaybackMode.Once"/> animation runs its action
    /// before <see cref="Then"/>.
    /// </para>
    /// </remarks>
    /// <param name="frameIndex">The frame's index in the animation's frames.</param>
    /// <param name="action">The action to run.</param>
    /// <returns>This handle, for setting more actions.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameIndex"/> isn't a frame of the animation.</exception>
    public SpriteAnimationHandle When(
        int frameIndex,
        Action action)
    {
        this._player?.When(this._run, frameIndex, action);
        return this;
    }

    /// <summary>
    /// Sets the action to run when this play of a
    /// <see cref="PlaybackMode.Once"/> animation finishes,
    /// replacing any set before. Not run if the play is interrupted first, or
    /// if it has already finished, and never for a looping animation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The action runs synchronously inside
    /// <see cref="SpriteAnimationPlayer.Update"/>. An animation played from it
    /// starts with the time left over from that update, so the hand-off lands
    /// on the exact frame.
    /// </para>
    /// <para>
    /// Because the slot holds one action, calling this again for the same play
    /// (for example from a state machine that calls <c>Play(...).Then(...)</c>
    /// every frame) replaces it rather than adding another.
    /// </para>
    /// </remarks>
    /// <param name="action">The action to run.</param>
    public void Then(
        Action action)
    {
        this._player?.Then(this._run, action);
    }
}

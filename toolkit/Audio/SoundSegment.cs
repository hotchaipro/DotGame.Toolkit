// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Audio;

namespace DotGame.Toolkit.Audio;

/// <summary>
/// Describes a named sub-range of a <see cref="DotGame.Framework.Audio.Sound"/> as a
/// <c>(offset, duration)</c> pair. Mirrors
/// <see cref="Graphics.TextureRegion"/> for audio: a non-owning view
/// over an underlying framework resource that callers can pass to a
/// voice factory in place of a whole <see cref="DotGame.Framework.Audio.Sound"/>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SoundSegment"/> has no lifetime of its own and is not
/// <see cref="IDisposable"/>; the underlying <see cref="DotGame.Framework.Audio.Sound"/> is
/// owned by whoever loaded it (typically the
/// <see cref="SoundBank"/> that produced this segment, or content
/// service callers for a whole-sound segment).
/// </para>
/// <para>
/// The framework's
/// <see cref="AudioService.CreateVoice(Sound, TimeSpan, TimeSpan)"/>
/// bakes the segment into the voice at construction time, so
/// playback remains allocation-free on every platform.
/// </para>
/// <para>
/// A default segment has no sound and can't be played: test for one with <c>segment == default</c>.
/// </para>
/// </remarks>
public readonly record struct SoundSegment
{
    /// <summary>
    /// Creates a segment that covers the entirety of
    /// <paramref name="sound"/>.
    /// </summary>
    public SoundSegment(
        Sound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);

        this.Sound = sound;
        this.Offset = TimeSpan.Zero;
        this.Duration = sound.Duration;
    }

    /// <summary>
    /// Creates a segment that covers <paramref name="duration"/>
    /// starting at <paramref name="offset"/> within
    /// <paramref name="sound"/>.
    /// </summary>
    public SoundSegment(
        Sound sound,
        TimeSpan offset,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(sound);

        if (offset < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                "Offset must be non-negative.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Duration must be positive.");
        }

        this.Sound = sound;
        this.Offset = offset;
        this.Duration = duration;
    }

    /// <summary>
    /// The underlying framework <see cref="DotGame.Framework.Audio.Sound"/> this
    /// segment is a view of.
    /// </summary>
    public Sound Sound { get; }

    /// <summary>
    /// The starting offset within <see cref="DotGame.Framework.Audio.Sound"/>.
    /// </summary>
    public TimeSpan Offset { get; }

    /// <summary>
    /// The duration of this segment within <see cref="DotGame.Framework.Audio.Sound"/>.
    /// </summary>
    public TimeSpan Duration { get; }
}

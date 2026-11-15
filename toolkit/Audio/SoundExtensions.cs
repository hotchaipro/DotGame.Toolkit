// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Audio;

namespace DotGame.Toolkit.Audio;

/// <summary>
/// Toolkit extensions that bridge between framework
/// <see cref="Sound"/>s and toolkit <see cref="SoundSegment"/>s.
/// </summary>
public static class SoundExtensions
{
    /// <summary>
    /// Wraps a whole <see cref="Sound"/> as a
    /// <see cref="SoundSegment"/> covering its full duration.
    /// </summary>
    public static SoundSegment AsSegment(
        this Sound sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        return new SoundSegment(sound);
    }

    /// <summary>
    /// Creates a <see cref="SoundSegment"/> over a sub-range of
    /// <paramref name="sound"/>.
    /// </summary>
    public static SoundSegment AsSegment(
        this Sound sound,
        TimeSpan offset,
        TimeSpan duration)
    {
        return new SoundSegment(sound, offset, duration);
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Audio;

namespace DotGame.Toolkit.Audio;

/// <summary>
/// Toolkit extensions for <see cref="AudioService"/> that accept
/// <see cref="SoundSegment"/>s.
/// </summary>
public static class AudioServiceExtensions
{
    /// <summary>
    /// Creates a pre-allocated voice that plays the given segment.
    /// The segment's <c>(offset, duration)</c> is baked into the
    /// voice at construction time so subsequent
    /// <see cref="SoundVoice.Play"/> calls remain allocation-free.
    /// </summary>
    public static SoundVoice CreateVoice(
        this AudioService audioService,
        SoundSegment segment)
    {
        ArgumentNullException.ThrowIfNull(audioService);

        return audioService.CreateVoice(
            segment.Sound,
            segment.Offset,
            segment.Duration);
    }
}

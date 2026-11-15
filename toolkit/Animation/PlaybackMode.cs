// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// How a <see cref="SpriteAnimation"/> or a <see cref="Tween{T}"/> plays.
/// </summary>
public enum PlaybackMode
{
    /// <summary>From its start to its end, and over again: 0 1 2 3 0 1 2 3 ...</summary>
    Loop,

    /// <summary>From its start to its end once, then it holds its end and finishes.</summary>
    Once,

    /// <summary>
    /// From its start to its end, back again, and over again: 0 1 2 3 2 1 0 1 ... A sprite animation's
    /// end frames aren't repeated.
    /// </summary>
    PingPong,
}

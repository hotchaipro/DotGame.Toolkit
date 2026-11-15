// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// How an <see cref="Easing"/>'s curve is applied over the animation.
/// </summary>
public enum EasingMode
{
    /// <summary>The curve as defined: slow at the start, fast at the end.</summary>
    In,

    /// <summary>The curve reflected: fast at the start, slow at the end.</summary>
    Out,

    /// <summary>The curve in on the first half, and out on the second.</summary>
    InOut,

    /// <summary>The curve out on the first half, and in on the second.</summary>
    OutIn,
}

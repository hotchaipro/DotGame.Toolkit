// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows a quarter of a circle.
/// </summary>
internal sealed class CircEase : Easing
{
    /// <summary>Makes a circular easing with this mode.</summary>
    public CircEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return 1f - MathF.Sqrt(MathF.Max(0f, 1f - (normalizedTime * normalizedTime)));
    }
}

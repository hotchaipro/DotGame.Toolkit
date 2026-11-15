// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows a quarter of a sine wave.
/// </summary>
internal sealed class SineEase : Easing
{
    /// <summary>Makes a sine easing with this mode.</summary>
    public SineEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return 1f - MathF.Cos(normalizedTime * MathF.PI / 2f);
    }
}

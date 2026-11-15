// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows a cubic curve.
/// </summary>
internal sealed class CubicEase : Easing
{
    /// <summary>Makes a cubic easing with this mode.</summary>
    public CubicEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return normalizedTime * normalizedTime * normalizedTime;
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows the curve t^2.
/// </summary>
internal sealed class QuadEase : Easing
{
    /// <summary>Makes a square easing with this mode.</summary>
    public QuadEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return normalizedTime * normalizedTime;
    }
}

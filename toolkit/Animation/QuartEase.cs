// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows the curve t^4.
/// </summary>
internal sealed class QuartEase : Easing
{
    /// <summary>Makes a fourth-power easing with this mode.</summary>
    public QuartEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        float squared = normalizedTime * normalizedTime;
        return squared * squared;
    }
}

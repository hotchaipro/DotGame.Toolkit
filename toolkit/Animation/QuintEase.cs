// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows the curve t^5.
/// </summary>
internal sealed class QuintEase : Easing
{
    /// <summary>Makes a fifth-power easing with this mode.</summary>
    public QuintEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        float squared = normalizedTime * normalizedTime;
        return squared * squared * normalizedTime;
    }
}

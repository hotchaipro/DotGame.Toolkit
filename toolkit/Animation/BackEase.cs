// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Pulls back before moving (eased in), or overshoots before settling (eased out).
/// </summary>
internal sealed class BackEase : Easing
{
    /// <summary>Makes an easing that overshoots, with this mode.</summary>
    public BackEase(
        EasingMode easingMode,
        float overshoot)
        : base(easingMode)
    {
        // NOTE: Penner's widens it for going in and out, as each half covers half the distance
        this.Amplitude = (easingMode == EasingMode.InOut) ? overshoot * 1.525f : overshoot;
    }

    /// <summary>
    /// How far the curve pulls back (or overshoots). Larger values go further.
    /// </summary>
    public float Amplitude { get; }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return normalizedTime * normalizedTime * (((this.Amplitude + 1.0f) * normalizedTime) - this.Amplitude);
    }
}

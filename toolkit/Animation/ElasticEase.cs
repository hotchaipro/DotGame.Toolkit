// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Robert Penner's elastic curve, as easings.net and other frameworks draw it: it swings with growing
/// (eased in) or settling (eased out) amplitude.
/// </summary>
internal sealed class ElasticEase : Easing
{
    // Penner's: a swing every 0.3 of the duration, or 0.45 going in and out
    public const float StandardPeriod = 0.3f;
    public const float StandardBothWaysPeriod = 0.45f;

    private readonly float _amplitude;
    private readonly float _shift;
    private readonly float _frequency;

    /// <param name="easingMode">Which end of the curve swings.</param>
    /// <param name="amplitude">How far the first swing goes: 1 is the standard curve. At least 1.</param>
    /// <param name="period">The time between swings, as a fraction of the duration. More than 0.</param>
    public ElasticEase(
        EasingMode easingMode,
        float amplitude,
        float period)
        : base(easingMode)
    {
        // NOTE: Written to catch NaN, which the comparison helpers let through
        if (!(amplitude >= 1f) || float.IsInfinity(amplitude))
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude), amplitude, "The amplitude must be 1 or more.");
        }

        if (!(period > 0f) || float.IsInfinity(period))
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "The period must be more than 0.");
        }

        this._amplitude = amplitude;
        this._frequency = MathF.Tau / period;

        // NOTE: Penner's phase, so the curve reaches exactly 1 at the end: a quarter period at amplitude 1
        this._shift = 1f + (period / MathF.Tau * MathF.Asin(1f / amplitude));
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        if (normalizedTime <= 0f)
        {
            return 0f;
        }

        if (normalizedTime >= 1f)
        {
            return 1f;
        }

        return -this._amplitude * MathF.Pow(2f, 10f * (normalizedTime - 1f)) * MathF.Sin((normalizedTime - this._shift) * this._frequency);
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows an exponential curve.
/// </summary>
internal sealed class ExponentialEase : Easing
{
    private readonly float _exponentScale;

    /// <summary>Makes an exponential easing with this mode and exponent.</summary>
    public ExponentialEase(
        EasingMode easingMode = EasingMode.Out,
        float exponent = 2.0f)
        : base(easingMode)
    {
        this.Exponent = exponent;

        // The constant denominator, computed once
        this._exponentScale = (MathF.Abs(exponent) <= float.Epsilon)
            ? 0
            : 1.0f / (MathF.Exp(exponent) - 1.0f);
    }

    /// <summary>The curve's exponent.</summary>
    public float Exponent { get; }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        if (this._exponentScale == 0)
        {
            return normalizedTime;
        }

        return (MathF.Exp(this.Exponent * normalizedTime) - 1.0f) * this._exponentScale;
    }
}

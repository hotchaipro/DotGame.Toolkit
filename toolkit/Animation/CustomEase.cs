// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// A curve a game supplies.
/// </summary>
internal sealed class CustomEase : Easing
{
    private readonly Func<float, float> _easeIn;

    public CustomEase(
        Func<float, float> easeIn,
        EasingMode easingMode)
        : base(easingMode)
    {
        this._easeIn = easeIn;
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return this._easeIn(normalizedTime);
    }
}

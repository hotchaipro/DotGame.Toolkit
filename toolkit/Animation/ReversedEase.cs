// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Another curve, played backward.
/// </summary>
internal sealed class ReversedEase : Easing
{
    private readonly Easing _easing;

    public ReversedEase(
        Easing easing)
        : base(EasingMode.In)
    {
        this._easing = easing;
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return 1f - this._easing.Evaluate(1f - normalizedTime);
    }
}

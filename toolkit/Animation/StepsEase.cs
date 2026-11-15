// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Holds each of a number of values, from 0 to 1, for an equal share of the time.
/// </summary>
internal sealed class StepsEase : Easing
{
    private readonly int _count;

    public StepsEase(
        int count)
        : base(EasingMode.In)
    {
        this._count = count;
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        // The time is split into count equal parts, the first holding 0 and the last 1; the end
        // itself (1) is in the last part
        float step = System.MathF.Min(System.MathF.Floor(normalizedTime * this._count), this._count - 1);
        return step / (this._count - 1);
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Bounces: eased out, it's a ball dropped onto the end value.
/// </summary>
internal sealed class BounceEase : Easing
{
    /// <summary>Makes an easing that bounces, with this mode.</summary>
    public BounceEase(
        EasingMode easingMode = EasingMode.Out)
        : base(easingMode)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        const float Scale = 7.5625f;
        const float Width = 2.75f;

        float t = 1f - normalizedTime;
        float bounced;
        if (t < (1f / Width))
        {
            bounced = Scale * t * t;
        }
        else if (t < (2f / Width))
        {
            t -= 1.5f / Width;
            bounced = (Scale * t * t) + 0.75f;
        }
        else if (t < (2.5f / Width))
        {
            t -= 2.25f / Width;
            bounced = (Scale * t * t) + 0.9375f;
        }
        else
        {
            t -= 2.625f / Width;
            bounced = (Scale * t * t) + 0.984375f;
        }

        return 1f - bounced;
    }
}

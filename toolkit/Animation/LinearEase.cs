// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Animation;

/// <summary>
/// No easing: progress is unchanged. <see cref="Easing.Linear"/> is a shared instance.
/// </summary>
internal sealed class LinearEase : Easing
{
    /// <summary>Makes an easing that doesn't ease: constant speed.</summary>
    public LinearEase()
        : base(EasingMode.In)
    {
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return normalizedTime;
    }
}

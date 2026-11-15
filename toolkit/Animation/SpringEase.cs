// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// A damped spring released from 0 towards 1, whose stiffness is chosen so that it has settled by the
/// end, and which is then corrected to end at exactly 1.
/// </summary>
internal sealed class SpringEase : Easing
{
    // How far the spring's swings decay by the end: about a thousandth of what they started at
    private const float Decay = 6.9f;

    private readonly float _decayRate;
    private readonly float _dampedFrequency;
    private readonly float _endError;

    public SpringEase(
        float damping)
        : base(EasingMode.In)
    {
        this._decayRate = Decay;

        // The frequency undamped is the decay over the damping, and it's this once damped
        float undamped = Decay / damping;
        this._dampedFrequency = undamped * MathF.Sqrt(MathF.Max(0f, 1f - (damping * damping)));

        this._endError = 1f - this.Position(1f);
    }

    private float Position(
        float time)
    {
        float decay = MathF.Exp(-this._decayRate * time);
        if (this._dampedFrequency <= 0f)
        {
            // Critically damped, with no swing
            return 1f - (decay * (1f + (this._decayRate * time)));
        }

        float angle = this._dampedFrequency * time;
        float swing = MathF.Cos(angle) + ((this._decayRate / this._dampedFrequency) * MathF.Sin(angle));
        return 1f - (decay * swing);
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        return this.Position(normalizedTime) + (this._endError * normalizedTime);
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A value that changes over a particle's life (or an emitter's duration), from time 0 to 1,
/// as keys joined by easing curves. Immutable, so one curve can be shared by many effects.
/// </summary>
/// <remarks>
/// A curve is data: its keys and the shared <see cref="Easing"/>s between them.
/// </remarks>
public sealed class ParticleCurve
{
    /// <summary>
    /// A curve that's 1 throughout: no change.
    /// </summary>
    public static ParticleCurve One { get; } = Constant(1);

    private readonly ParticleCurveKey[] _keys;

    private ParticleCurve(
        ParticleCurveKey[] keys)
    {
        this._keys = keys;

        this.IsConstant = true;
        for (int i = 1; i < keys.Length; i++)
        {
            if (keys[i].Value != keys[0].Value)
            {
                this.IsConstant = false;
            }
        }
    }

    /// <summary>
    /// The curve's keys, in time order.
    /// </summary>
    public ReadOnlySpan<ParticleCurveKey> Keys => this._keys;

    /// <summary>
    /// Whether the curve has the same value throughout.
    /// </summary>
    public bool IsConstant { get; }

    /// <summary>A curve that holds one value.</summary>
    public static ParticleCurve Constant(
        float value)
    {
        return new ParticleCurve([new ParticleCurveKey(0, value)]);
    }

    /// <summary>
    /// A curve from <paramref name="from"/> at time 0 to <paramref name="to"/> at time 1,
    /// along <paramref name="ease"/> (linearly when null).
    /// </summary>
    public static ParticleCurve Between(
        float from,
        float to,
        Easing? ease = null)
    {
        return new ParticleCurve([new ParticleCurveKey(0, from), new ParticleCurveKey(1, to, ease)]);
    }

    /// <summary>
    /// A curve through <paramref name="keys"/>, which must be in time order. Before the first
    /// key the curve has the first key's value, and after the last, the last key's.
    /// </summary>
    public static ParticleCurve FromKeys(
        params ParticleCurveKey[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0)
        {
            throw new ArgumentException("A curve needs at least one key.", nameof(keys));
        }

        for (int i = 1; i < keys.Length; i++)
        {
            if (keys[i].Time < keys[i - 1].Time)
            {
                throw new ArgumentException("A curve's keys must be in time order.", nameof(keys));
            }
        }

        return new ParticleCurve((ParticleCurveKey[])keys.Clone());
    }

    /// <summary>
    /// The curve's value at <paramref name="time"/>, from 0 to 1.
    /// </summary>
    public float Evaluate(
        float time)
    {
        var keys = this._keys;
        if (this.IsConstant || (time <= keys[0].Time))
        {
            return keys[0].Value;
        }

        // Curves have a few keys, so a scan beats a search
        for (int i = 1; i < keys.Length; i++)
        {
            ref readonly var key = ref keys[i];
            if (time < key.Time)
            {
                ref readonly var previous = ref keys[i - 1];
                float amount = (time - previous.Time) / (key.Time - previous.Time);
                if (key.Ease is not null)
                {
                    amount = key.Ease.Evaluate(amount);
                }

                return previous.Value + ((key.Value - previous.Value) * amount);
            }
        }

        return keys[^1].Value;
    }
}

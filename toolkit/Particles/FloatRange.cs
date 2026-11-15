// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A range of values, sampled uniformly at random; a single value converts to a range
/// of one value.
/// </summary>
/// <remarks>
/// Write a range as a tuple: <c>Speed = (100, 300)</c>, or a single value: <c>Speed = 200</c>.
/// </remarks>
/// <param name="Min">The smallest value.</param>
/// <param name="Max">The largest value.</param>
public readonly record struct FloatRange(
    float Min,
    float Max)
{
    /// <summary>A range of one value: always that.</summary>
    public FloatRange(
        float value)
        : this(value, value)
    {
    }

    /// <summary>
    /// Whether the range is a single value.
    /// </summary>
    public bool IsConstant => this.Min == this.Max;

    /// <summary>A range of exactly this value.</summary>
    public static implicit operator FloatRange(
        float value)
    {
        return new FloatRange(value, value);
    }

    /// <summary>A range between these two values.</summary>
    public static implicit operator FloatRange(
        (float Min, float Max) range)
    {
        return new FloatRange(range.Min, range.Max);
    }

    /// <summary>
    /// The range from <paramref name="center"/> less <paramref name="variance"/> to
    /// <paramref name="center"/> plus it, as particle tools commonly describe values.
    /// </summary>
    public static FloatRange Around(
        float center,
        float variance)
    {
        return new FloatRange(center - variance, center + variance);
    }

    /// <summary>
    /// The value <paramref name="amount"/> of the way from <see cref="Min"/> to
    /// <see cref="Max"/>.
    /// </summary>
    public float Lerp(
        float amount)
    {
        return this.Min + ((this.Max - this.Min) * amount);
    }

    /// <summary>
    /// A random value in the range.
    /// </summary>
    internal float Sample(
        ParticleRandom random)
    {
        return (this.Min == this.Max) ? this.Min : this.Min + ((this.Max - this.Min) * random.NextSingle());
    }
}

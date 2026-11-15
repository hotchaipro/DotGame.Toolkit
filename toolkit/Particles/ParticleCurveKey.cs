// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A point on a <see cref="ParticleCurve"/>: its value at a time from 0 to 1, reached from the
/// previous key along <paramref name="Ease"/> (linearly when null).
/// </summary>
/// <param name="Time">When, from 0 at the start of the particle's life to 1 at its end.</param>
/// <param name="Value">The value at that time.</param>
/// <param name="Ease">How it gets here from the previous key, or null for a straight line.</param>
public readonly record struct ParticleCurveKey(
    float Time,
    float Value,
    Easing? Ease = null);

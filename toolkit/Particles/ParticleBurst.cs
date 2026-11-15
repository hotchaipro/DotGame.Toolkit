// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A burst of particles at a time in an emitter's run.
/// </summary>
/// <param name="TimeSeconds">Seconds after the emitter starts (after its delay).</param>
/// <param name="Count">How many particles.</param>
public readonly record struct ParticleBurst(
    float TimeSeconds,
    int Count);

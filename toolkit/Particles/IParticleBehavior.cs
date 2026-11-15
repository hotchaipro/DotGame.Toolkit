// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Custom code for a particle effect, for what its built-in values don't cover. Set it as the
/// effect's <see cref="ParticleEffect.Behavior"/>.
/// </summary>
/// <remarks>
/// One instance serves every particle of every effect that uses it, so keep per-particle state
/// in the particle (<see cref="Particle.Custom"/>, for example). A behavior changes a
/// particle's simulation state while it updates; a <see cref="ParticleRenderer"/> reads the
/// final drawn state while it draws. A behavior runs in the middle of the system's update, so it
/// must not create emitters, burst or clear the system it belongs to.
/// </remarks>
public interface IParticleBehavior
{
    /// <summary>
    /// Adjusts a particle as it starts, after the effect's values are applied.
    /// </summary>
    void Start(
        ref Particle particle)
    {
    }

    /// <summary>
    /// Adjusts a particle each frame, after the effect has moved it and aged it by
    /// <paramref name="elapsedSeconds"/>.
    /// </summary>
    /// <remarks>
    /// To stay correct at any frame rate, change things by rates times
    /// <paramref name="elapsedSeconds"/>, or compute them from <see cref="Particle.AgeSeconds"/>.
    /// </remarks>
    /// <returns>False to remove the particle.</returns>
    bool Update(
        ref Particle particle,
        float elapsedSeconds);
}

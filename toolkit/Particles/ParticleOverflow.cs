// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// What happens to a new particle when an effect already has its
/// <see cref="ParticleEffect.Capacity"/> of particles.
/// </summary>
public enum ParticleOverflow
{
    /// <summary>
    /// The new particle isn't added.
    /// </summary>
    DropNew,

    /// <summary>
    /// The oldest particle is removed to make room, so a busy effect's particles are cut short.
    /// </summary>
    ReplaceOldest,
}

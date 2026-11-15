// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// One particle placed exactly, for patterns the effect's random values don't describe.
/// Values left null are sampled from the effect.
/// </summary>
public readonly record struct ParticleSpawn
{
    /// <summary>Where it starts.</summary>
    public required Vector2 Position { get; init; }

    /// <summary>How it starts moving, in pixels per second, in place of the effect's.</summary>
    public Vector2? Velocity { get; init; }

    /// <summary>How long it lives, in place of the effect's.</summary>
    public float? LifetimeSeconds { get; init; }

    /// <summary>How big it's drawn, in place of the effect's.</summary>
    public Vector2? Scale { get; init; }

    /// <summary>
    /// The starting rotation, in degrees.
    /// </summary>
    public float? RotationDegrees { get; init; }

    /// <summary>Its color, in place of the effect's.</summary>
    public Color? Color { get; init; }

    /// <summary>How opaque it is, in place of the effect's.</summary>
    public float? Opacity { get; init; }

    /// <summary>
    /// The particle's <see cref="Particle.Variant"/>.
    /// </summary>
    public uint? Variant { get; init; }

    /// <summary>
    /// Which of the system's effects the particle belongs to.
    /// </summary>
    public int Layer { get; init; }
}

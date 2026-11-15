// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A particle as it's drawn: everything the system works out for it (where it is, how big,
/// which way, what color, how old), for a <see cref="ParticleRenderer"/> to turn into pixels.
/// </summary>
public readonly struct ParticleDrawState
{
    internal ParticleDrawState(
        Vector2 position,
        Vector2 scale,
        Angle rotation,
        Color color,
        Vector2 velocity,
        float age,
        float normalizedAge,
        uint variant,
        Vector4 custom)
    {
        this.Position = position;
        this.Scale = scale;
        this.Rotation = rotation;
        this.Color = color;
        this.Velocity = velocity;
        this.AgeSeconds = age;
        this.NormalizedAge = normalizedAge;
        this.Variant = variant;
        this.Custom = custom;
    }

    /// <summary>
    /// Where it is, in the system's space, after its sway.
    /// </summary>
    public Vector2 Position { get; }

    /// <summary>
    /// Its scale, after <see cref="ParticleEffect.ScaleOverLife"/> and its flip.
    /// </summary>
    public Vector2 Scale { get; }

    /// <summary>
    /// Its rotation, after its spin, and from the direction of its velocity with
    /// <see cref="ParticleEffect.AlignToVelocity"/>.
    /// </summary>
    public Angle Rotation { get; }

    /// <summary>
    /// Its premultiplied color, after its color change, opacity, the system's draw tint, and
    /// its effect's blend: an additive particle's alpha is 0.
    /// </summary>
    public Color Color { get; }

    /// <summary>
    /// How fast it's moving, in pixels per second.
    /// </summary>
    public Vector2 Velocity { get; }

    /// <summary>
    /// How long it's lived, in seconds.
    /// </summary>
    public float AgeSeconds { get; }

    /// <summary>
    /// How far through its life it is, from 0 to 1.
    /// </summary>
    public float NormalizedAge { get; }

    /// <summary>
    /// A random number fixed for its life, for a stable choice (see
    /// <see cref="Particle.Variant"/>).
    /// </summary>
    public uint Variant { get; }

    /// <summary>
    /// What the effect's <see cref="IParticleBehavior"/> left in
    /// <see cref="Particle.Custom"/>.
    /// </summary>
    public Vector4 Custom { get; }
}

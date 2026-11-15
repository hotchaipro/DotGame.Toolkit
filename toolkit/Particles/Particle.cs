// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A particle's simulation state, as a custom <see cref="IParticleBehavior"/> sees and adjusts
/// it.
/// </summary>
/// <remarks>
/// Values that change over the particle's life (its scale, opacity, and color curves, its
/// spin, sway, and flip) are computed from its age when drawn, so these hold their starting
/// values. Times are in seconds.
/// </remarks>
// Fields, not properties: behaviors update thousands of particles a frame, in place
#pragma warning disable CA1051
public struct Particle
{
    /// <summary>
    /// Where the particle is, in the system's space.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// How fast it's moving, in pixels per second.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// Where it was emitted from, in the system's space: the burst's or emitter's position,
    /// before the effect's shape spread it out. For behaviors that pull particles toward or
    /// around the point they came from.
    /// </summary>
    public Vector2 Origin;

    /// <summary>
    /// How long it's lived.
    /// </summary>
    public float AgeSeconds;

    /// <summary>
    /// How long it lives.
    /// </summary>
    public float LifetimeSeconds;

    /// <summary>How big it's drawn.</summary>
    public Vector2 Scale;

    /// <summary>Its rotation.</summary>
    public Angle Rotation;

    /// <summary>
    /// How far it turns per second.
    /// </summary>
    public Angle Spin;

    /// <summary>Its color at the start of its life.</summary>
    public Color Color;

    /// <summary>Its color at the end of its life.</summary>
    public Color EndColor;

    /// <summary>How opaque it is, from 0 to 1.</summary>
    public float Opacity;

    /// <summary>
    /// A random number fixed for the particle's life, so a renderer can make a stable choice
    /// for it (which of several textures it draws, for example).
    /// </summary>
    public uint Variant;

    /// <summary>How far it sways from side to side.</summary>
    internal float SwayAmplitude;

    /// <summary>
    /// The sway's angular frequency, in radians per second.
    /// </summary>
    internal float SwayFrequency;

    /// <summary>Where in its sway it starts, in radians.</summary>
    internal float SwayPhase;

    /// <summary>
    /// The flip's angular frequency, in radians per second.
    /// </summary>
    internal float FlipFrequency;

    /// <summary>Where in its flip it starts, in radians.</summary>
    internal float FlipPhase;

    /// <summary>
    /// Free for a custom behavior's own state. It reaches the renderer as
    /// <see cref="ParticleDrawState.Custom"/>.
    /// </summary>
    public Vector4 Custom;

    /// <summary>
    /// How far through its life it is, from 0 to 1.
    /// </summary>
    public readonly float NormalizedAge => (this.LifetimeSeconds > 0) ? Math.Min(this.AgeSeconds / this.LifetimeSeconds, 1) : 1;
}
#pragma warning restore CA1051

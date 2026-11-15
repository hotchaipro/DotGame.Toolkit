// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// What a particle effect looks like and how it behaves: how particles are drawn, how they're
/// emitted, how they start, how they move, and how they change over their lives. A
/// <see cref="ParticleSystem"/> runs effects.
/// </summary>
/// <remarks>
/// <para>
/// An effect is immutable data, like a theme: define it once (in the game's theme, for
/// example), share it, and make variants with <c>with</c>.
/// </para>
/// <para>
/// Angles are in degrees, clockwise from the right (screen Y points down), so a
/// <see cref="DirectionDegrees"/> of 90 is straight down. Times are in seconds, distances in pixels.
/// Ranges are sampled per particle when it starts.
/// </para>
/// <para>
/// Motion is exact at any frame rate: gravity and drag move each particle along their exact
/// path, and sways and spins are computed from its age.
/// </para>
/// </remarks>
public sealed record ParticleEffect
{
    // Appearance

    /// <summary>
    /// What draws the particles.
    /// </summary>
    public required ParticleRenderer Renderer { get; init; }

    /// <summary>
    /// How particles blend with what's behind them. Applied to their color before the renderer
    /// sees it, so every renderer blends correctly.
    /// </summary>
    public ParticleBlend Blend { get; init; }

    /// <summary>
    /// The most particles the effect has at once.
    /// </summary>
    public int Capacity { get; init; } = 100;

    /// <summary>What happens to a new particle when the effect is at its <see cref="Capacity"/>.</summary>
    public ParticleOverflow Overflow { get; init; }

    // Life

    /// <summary>
    /// How long each particle lives, in seconds.
    /// </summary>
    public FloatRange LifetimeSeconds { get; init; } = 1;

    // Emission, by an emitter (see ParticleSystem.CreateEmitter)

    /// <summary>
    /// Particles emitted per second while an emitter runs.
    /// </summary>
    public float Rate { get; init; }

    /// <summary>
    /// Particles emitted per pixel an emitter moves, so a trail is even at any speed.
    /// </summary>
    public float RatePerDistance { get; init; }

    /// <summary>
    /// How <see cref="Rate"/> changes over the emitter's <see cref="DurationSeconds"/>, as a multiplier.
    /// </summary>
    public ParticleCurve RateOverDuration { get; init; } = ParticleCurve.One;

    /// <summary>
    /// Bursts of particles at times in an emitter's run.
    /// </summary>
    public ParticleBurst[] Bursts { get; init; } = [];

    /// <summary>
    /// How long an emitter runs, in seconds, after which it stops; null runs until it's stopped.
    /// </summary>
    public float? DurationSeconds { get; init; }

    /// <summary>
    /// How long an emitter waits before it starts, in seconds.
    /// </summary>
    public float DelaySeconds { get; init; }

    // Start

    /// <summary>
    /// Where around the emitter (or burst position) particles start.
    /// </summary>
    public EmitShape Shape { get; init; }

    /// <summary>
    /// Whether the <see cref="Shape"/> is scaled by the emitter's
    /// <see cref="ParticleEmitter.Scale"/>.
    /// </summary>
    public bool ShapeScalesWithEmitter { get; init; }

    /// <summary>
    /// How fast particles start moving, in pixels per second.
    /// </summary>
    public FloatRange Speed { get; init; }

    /// <summary>
    /// The direction particles start moving, in degrees: all around by default.
    /// </summary>
    public FloatRange DirectionDegrees { get; init; } = (0, 360);

    /// <summary>
    /// The particles' starting scale, the same on both axes.
    /// </summary>
    public FloatRange Scale { get; init; } = 1;

    /// <summary>
    /// A further starting scale along X, sampled separately from <see cref="ScaleY"/>, to vary
    /// particles' proportions.
    /// </summary>
    public FloatRange ScaleX { get; init; } = 1;

    /// <summary>
    /// A further starting scale along Y; see <see cref="ScaleX"/>.
    /// </summary>
    public FloatRange ScaleY { get; init; } = 1;

    /// <summary>
    /// The particles' starting rotation, in degrees.
    /// </summary>
    public FloatRange RotationDegrees { get; init; }

    /// <summary>
    /// How fast particles turn, in degrees per second.
    /// </summary>
    public FloatRange SpinDegrees { get; init; }

    /// <summary>
    /// Whether particles point the way they move. <see cref="RotationDegrees"/> and
    /// <see cref="SpinDegrees"/> then turn them from that direction: 90 for a texture drawn
    /// pointing down the screen.
    /// </summary>
    public bool AlignToVelocity { get; init; }

    /// <summary>
    /// The particles' starting color.
    /// </summary>
    public ParticleColor Color { get; init; } = ParticleColor.White;

    /// <summary>
    /// The color particles change to by the end of their lives, sampled separately; null
    /// keeps their starting color.
    /// </summary>
    public ParticleColor? EndColor { get; init; }

    /// <summary>
    /// How particles change from <see cref="Color"/> to <see cref="EndColor"/>; linearly when null.
    /// </summary>
    public Easing? ColorEase { get; init; }

    /// <summary>
    /// The particles' starting opacity.
    /// </summary>
    public FloatRange Opacity { get; init; } = 1;

    // Motion

    /// <summary>
    /// The acceleration on every particle, in pixels per second per second.
    /// </summary>
    public Vector2 Gravity { get; init; }

    /// <summary>
    /// How fast particles slow down, per axis: the fraction of their speed lost per second,
    /// continuously. With <see cref="Gravity"/>, particles approach a top speed of gravity
    /// divided by drag (a falling leaf's).
    /// </summary>
    public Vector2 Drag { get; init; }

    /// <summary>
    /// A sideways sway; null for none.
    /// </summary>
    public ParticleSway? Sway { get; init; }

    /// <summary>
    /// A flutter, as of paper turning over; null for none.
    /// </summary>
    public ParticleFlip? Flip { get; init; }

    // Over life

    /// <summary>
    /// How the particles' scale changes over their lives, as a multiplier.
    /// </summary>
    public ParticleCurve ScaleOverLife { get; init; } = ParticleCurve.One;

    /// <summary>
    /// How the particles' opacity changes over their lives, as a multiplier.
    /// </summary>
    public ParticleCurve OpacityOverLife { get; init; } = ParticleCurve.One;

    // Custom code

    /// <summary>
    /// A custom behavior that adjusts each particle as it starts and every frame; null for
    /// none.
    /// </summary>
    public IParticleBehavior? Behavior { get; init; }
}

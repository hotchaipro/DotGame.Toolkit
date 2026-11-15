// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Changes to an effect's values for one burst, such as a direction or color that depends on
/// where it's used. Values left null keep the effect's.
/// </summary>
public readonly record struct ParticleOverrides
{
    /// <summary>
    /// Where around the burst position particles start.
    /// </summary>
    public EmitShape? Shape { get; init; }

    /// <summary>
    /// The direction particles start moving, in degrees.
    /// </summary>
    public FloatRange? DirectionDegrees { get; init; }

    /// <summary>
    /// The particles' starting color.
    /// </summary>
    public ParticleColor? Color { get; init; }

    /// <summary>
    /// The particles' <see cref="Particle.Variant"/>, so a renderer makes the same choice for
    /// all of them: with <see cref="ParticleRenderer.AnyOf"/>, 2 draws the third texture.
    /// </summary>
    public uint? Variant { get; init; }
}

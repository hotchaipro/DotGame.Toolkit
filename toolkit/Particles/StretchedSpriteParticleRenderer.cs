// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Draws each particle as a texture pointed along its velocity and lengthened in proportion to
/// its speed, for sparks, rain, and speed lines. Create one with
/// <see cref="ParticleRenderer.Stretched"/>.
/// </summary>
/// <remarks>
/// The texture is drawn pointing right, centered on the particle. A particle that isn't moving
/// draws at its own rotation and length.
/// </remarks>
public sealed record StretchedSpriteParticleRenderer : ParticleRenderer
{
    internal StretchedSpriteParticleRenderer(
        ITextureSource texture)
    {
        this.Texture = texture;
    }

    /// <summary>What each particle draws.</summary>
    public ITextureSource Texture { get; }

    /// <summary>
    /// How much longer a particle draws per pixel per second of speed, beyond the texture's
    /// width times its scale.
    /// </summary>
    public float LengthPerSpeed { get; init; }

    /// <summary>
    /// The longest a particle draws, in pixels, so a fast particle doesn't stretch across the
    /// screen; null for no limit.
    /// </summary>
    public float? MaxLength { get; init; }

    /// <inheritdoc/>
    public override void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles)
    {
        var texture = this.Texture;
        foreach (ref readonly var particle in particles)
        {
            this.Resolve(in particle, out var rotation, out var origin, out var scale);
            spriteBatch.Draw(texture, particle.Position, particle.Color, rotation, origin, scale);
        }
    }

    /// <summary>
    /// The rotation, origin and scale a particle draws with.
    /// </summary>
    internal void Resolve(
        in ParticleDrawState particle,
        out Angle rotation,
        out Vector2 origin,
        out Vector2 scale)
    {
        var source = this.Texture.Bounds;
        var logicalSize = new Vector2(source.Width, source.Height);
        origin = logicalSize * 0.5f;

        float speed = particle.Velocity.Length();
        if ((speed <= 0) || (logicalSize.X <= 0))
        {
            rotation = particle.Rotation;
            scale = particle.Scale;
            return;
        }

        rotation = Angle.FromRadians(MathF.Atan2(particle.Velocity.Y, particle.Velocity.X));

        float length = (logicalSize.X * particle.Scale.X) + (this.LengthPerSpeed * speed);
        if (length > this.MaxLength)
        {
            length = this.MaxLength.GetValueOrDefault();
        }

        scale = new Vector2(length / logicalSize.X, particle.Scale.Y);
    }
}

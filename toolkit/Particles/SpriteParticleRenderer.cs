// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Draws each particle as a texture, a whole texture or an atlas region, through the toolkit's
/// trim-aware <see cref="SpriteBatchExtensions.Draw(SpriteBatch, ITextureSource, Vector2, Framework.Color, Framework.Angle, Vector2, Vector2?, Framework.Rectangle?, Framework.Graphics.SpriteFlip)"/>.
/// Create one with <see cref="ParticleRenderer.Sprite"/> or <see cref="ParticleRenderer.AnyOf"/>.
/// </summary>
/// <remarks>
/// A solid particle (confetti, sparks, rain) is a white texture tinted by the particle's
/// color: <c>Sprite(white) with { Size = new(15, 25) }</c> draws 15×25 confetti. Use a white
/// atlas region of a few pixels with padding: a tiny region stretched large samples its
/// neighbors unless the atlas pads or extrudes it.
/// </remarks>
public sealed record SpriteParticleRenderer : ParticleRenderer
{
    private readonly ITextureSource[] _textures;

    internal SpriteParticleRenderer(
        ITextureSource[] textures)
    {
        this._textures = textures;
    }

    /// <summary>
    /// The textures; each particle draws the one its <see cref="Particle.Variant"/> picks.
    /// </summary>
    public ReadOnlySpan<ITextureSource> Textures => this._textures;

    /// <summary>
    /// The size a particle draws at before its scale; null for the texture's logical
    /// (untrimmed) size.
    /// </summary>
    public Vector2? Size { get; init; }

    /// <summary>
    /// Where the texture is placed, and what it scales and rotates around, from (0, 0) at its
    /// top left to (1, 1) at its bottom right. Relative, since one renderer can draw textures
    /// of different sizes.
    /// </summary>
    public Vector2 Pivot { get; init; } = new Vector2(0.5f, 0.5f);

    /// <inheritdoc/>
    public override void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles)
    {
        foreach (ref readonly var particle in particles)
        {
            var texture = this.Resolve(in particle, out var origin, out var scale);
            spriteBatch.Draw(texture, particle.Position, particle.Color, particle.Rotation, origin, scale);
        }
    }

    /// <summary>
    /// The texture a particle draws, and the origin (in the texture's logical pixels, as the
    /// trim-aware draw takes it) and scale it draws with.
    /// </summary>
    internal ITextureSource Resolve(
        in ParticleDrawState particle,
        out Vector2 origin,
        out Vector2 scale)
    {
        var textures = this._textures;
        var texture = (textures.Length == 1) ? textures[0] : textures[(int)(particle.Variant % (uint)textures.Length)];

        var source = texture.Bounds;
        var logicalSize = new Vector2(source.Width, source.Height);
        origin = this.Pivot * logicalSize;
        scale = ScaleTo(logicalSize, this.Size, particle.Scale);
        return texture;
    }
}

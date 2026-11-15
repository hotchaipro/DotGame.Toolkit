// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Draws an effect's particles from their final state. The built-in renderers draw textures
/// (<see cref="Sprite"/>, <see cref="AnyOf"/>), sprite animations (<see cref="Animation"/>),
/// stretched sprites (<see cref="Stretched"/>), and several renderers at once
/// (<see cref="Composite"/>); a game adds its own by deriving from this.
/// </summary>
/// <remarks>
/// <para>
/// A renderer is immutable data, like the rest of an effect, so <c>with</c> makes variants
/// and one renderer can be shared by many effects.
/// </para>
/// <para>
/// A renderer draws into the batch it's given, which the caller has begun: it can't change
/// the transform. Text needs nothing special: <c>DrawString</c> draws with the font's own
/// shader, whatever the batch began with.
/// </para>
/// </remarks>
public abstract record ParticleRenderer
{
    /// <summary>
    /// Draws an effect's live particles, oldest first, into the caller's batch. Fully
    /// transparent particles are left out.
    /// </summary>
    public abstract void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles);

    /// <summary>
    /// Particles draw <paramref name="texture"/>, a whole texture or an atlas region.
    /// </summary>
    public static SpriteParticleRenderer Sprite(
        ITextureSource texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new SpriteParticleRenderer([texture]);
    }

    /// <summary>
    /// Each particle draws one of <paramref name="textures"/>, chosen by its
    /// <see cref="Particle.Variant"/>, so it keeps the same one for its life.
    /// </summary>
    public static SpriteParticleRenderer AnyOf(
        params ITextureSource[] textures)
    {
        ArgumentNullException.ThrowIfNull(textures);
        if (textures.Length == 0)
        {
            throw new ArgumentException("A particle needs at least one texture.", nameof(textures));
        }

        foreach (var texture in textures)
        {
            ArgumentNullException.ThrowIfNull(texture, nameof(textures));
        }

        return new SpriteParticleRenderer((ITextureSource[])textures.Clone());
    }

    /// <summary>
    /// Particles play <paramref name="animation"/>: once over each particle's life, or from
    /// its age at the animation's own frame rate.
    /// </summary>
    public static AnimatedSpriteParticleRenderer Animation(
        SpriteAnimation animation,
        ParticleAnimationTiming timing = ParticleAnimationTiming.OverLife)
    {
        ArgumentNullException.ThrowIfNull(animation);
        return new AnimatedSpriteParticleRenderer(animation) { Timing = timing };
    }

    /// <summary>
    /// The same particles drawn by each of <paramref name="renderers"/> in turn, for example
    /// a soft glow under a crisp core.
    /// </summary>
    public static CompositeParticleRenderer Composite(
        params ParticleRenderer[] renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        if (renderers.Length == 0)
        {
            throw new ArgumentException("A composite needs at least one renderer.", nameof(renderers));
        }

        foreach (var renderer in renderers)
        {
            ArgumentNullException.ThrowIfNull(renderer, nameof(renderers));
        }

        return new CompositeParticleRenderer((ParticleRenderer[])renderers.Clone());
    }

    /// <summary>
    /// Particles draw <paramref name="texture"/> pointed along their velocity and lengthened
    /// in proportion to their speed, for sparks, rain, and speed lines.
    /// </summary>
    /// <param name="texture">The texture, drawn pointing right.</param>
    /// <param name="lengthPerSpeed">How much longer it draws per pixel per second of speed.</param>
    public static StretchedSpriteParticleRenderer Stretched(
        ITextureSource texture,
        float lengthPerSpeed)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegative(lengthPerSpeed);
        return new StretchedSpriteParticleRenderer(texture) { LengthPerSpeed = lengthPerSpeed };
    }

    /// <summary>
    /// The scale that draws a texture of <paramref name="logicalSize"/> at
    /// <paramref name="size"/> (its own size when null), times the particle's scale.
    /// </summary>
    private protected static Vector2 ScaleTo(
        Vector2 logicalSize,
        Vector2? size,
        Vector2 particleScale)
    {
        if ((size is not Vector2 target) || (logicalSize.X <= 0) || (logicalSize.Y <= 0))
        {
            return particleScale;
        }

        return particleScale * (target / logicalSize);
    }
}

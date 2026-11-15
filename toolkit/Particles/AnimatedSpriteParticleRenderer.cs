// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Draws each particle as a frame of a <see cref="SpriteAnimation"/>, chosen from its age, so
/// frames, holds and playback are authored like any sprite animation. Create one with
/// <see cref="ParticleRenderer.Animation"/>.
/// </summary>
public sealed record AnimatedSpriteParticleRenderer : ParticleRenderer
{
    internal AnimatedSpriteParticleRenderer(
        SpriteAnimation animation)
    {
        this.SpriteAnimation = animation;
    }

    /// <summary>
    /// The animation each particle plays.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public SpriteAnimation SpriteAnimation { get; }

    /// <summary>
    /// How the animation is timed: once over each particle's life, or from its age.
    /// </summary>
    public ParticleAnimationTiming Timing { get; init; }

    /// <summary>
    /// The size a particle draws at before its scale; null for the animation's logical size.
    /// </summary>
    public Vector2? Size { get; init; }

    /// <summary>
    /// Where each frame is placed, and what it scales and rotates around, from (0, 0) at its
    /// top left to (1, 1) at its bottom right.
    /// </summary>
    public Vector2 Pivot { get; init; } = new Vector2(0.5f, 0.5f);

    /// <inheritdoc/>
    public override void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles)
    {
        foreach (ref readonly var particle in particles)
        {
            var frame = this.Resolve(in particle, out var origin, out var scale);
            spriteBatch.Draw(frame, particle.Position, particle.Color, particle.Rotation, origin, scale);
        }
    }

    /// <summary>
    /// The frame a particle draws, and the origin and scale it draws with.
    /// </summary>
    internal TextureRegion Resolve(
        in ParticleDrawState particle,
        out Vector2 origin,
        out Vector2 scale)
    {
        var animation = this.SpriteAnimation;
        int index;
        if (this.Timing == ParticleAnimationTiming.OverLife)
        {
            // NOTE: Just short of the whole pass at the end of life, so a looping animation
            // ends on its last frame rather than wrapping to its first
            long pass = animation.Duration.Ticks;
            long ticks = Math.Min((long)(particle.NormalizedAge * pass), pass - 1);
            index = animation.GetFrameIndex(ticks);
        }
        else
        {
            index = animation.GetFrameIndex((long)(particle.AgeSeconds * TimeSpan.TicksPerSecond));
        }

        var logicalSize = new Vector2(animation.Size.X, animation.Size.Y);
        origin = this.Pivot * logicalSize;
        scale = ScaleTo(logicalSize, this.Size, particle.Scale);
        return animation.GetRegion(index);
    }
}

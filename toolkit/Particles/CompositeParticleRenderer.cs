// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Draws the same particles with each of several renderers in turn, for example a soft glow
/// under a crisp core. Create one with <see cref="ParticleRenderer.Composite"/>.
/// </summary>
/// <remarks>
/// Every renderer draws into the same batch, so the effect has one blend mode: a glow that
/// needs additive blending under a normal core is two effects.
/// </remarks>
public sealed record CompositeParticleRenderer : ParticleRenderer
{
    private readonly ParticleRenderer[] _renderers;

    internal CompositeParticleRenderer(
        ParticleRenderer[] renderers)
    {
        this._renderers = renderers;
    }

    /// <summary>
    /// The renderers, in the order they draw.
    /// </summary>
    public ReadOnlySpan<ParticleRenderer> Renderers => this._renderers;

    /// <inheritdoc/>
    public override void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles)
    {
        foreach (var renderer in this._renderers)
        {
            renderer.Draw(spriteBatch, particles);
        }
    }
}

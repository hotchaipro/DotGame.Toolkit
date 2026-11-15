// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// The colors particles are given when they start (or end), sampled per particle. A color
/// converts to a fixed one.
/// </summary>
public readonly record struct ParticleColor
{
    /// <summary>Every particle is white.</summary>
    public static ParticleColor White => Constant(Color.White);

    private readonly Color[]? _palette;

    private ParticleColor(
        ParticleColorKind kind,
        Color from,
        Color to,
        Color[]? palette)
    {
        this.Kind = kind;
        this.From = from;
        this.To = to;
        this._palette = palette;
    }

    /// <summary>How the color is chosen.</summary>
    internal ParticleColorKind Kind { get; }

    /// <summary>
    /// The fixed color, or the first of the two to pick between.
    /// </summary>
    internal Color From { get; }

    /// <summary>
    /// The second of the two colors to pick between.
    /// </summary>
    internal Color To { get; }

    /// <summary>
    /// The palette's colors; empty unless <see cref="Kind"/> is
    /// <see cref="ParticleColorKind.Palette"/>.
    /// </summary>
    internal ReadOnlySpan<Color> PaletteColors => this._palette;

    /// <summary>One color, for the whole of a particle's life.</summary>
    public static implicit operator ParticleColor(
        Color color)
    {
        return Constant(color);
    }

    /// <summary>
    /// The same color for every particle. A value, so making one per burst allocates nothing.
    /// </summary>
    public static ParticleColor Constant(
        Color color)
    {
        return new ParticleColor(ParticleColorKind.Constant, color, color, null);
    }

    /// <summary>
    /// A random color between <paramref name="from"/> and <paramref name="to"/>, per particle.
    /// A value, so making one per burst allocates nothing.
    /// </summary>
    public static ParticleColor Between(
        Color from,
        Color to)
    {
        return new ParticleColor(ParticleColorKind.Between, from, to, null);
    }

    /// <summary>
    /// One of <paramref name="colors"/>, picked at random per particle. Copies the colors, so
    /// make palettes once (in a theme), not per burst.
    /// </summary>
    public static ParticleColor Palette(
        params Color[] colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Length == 0)
        {
            throw new ArgumentException("A palette needs at least one color.", nameof(colors));
        }

        return new ParticleColor(ParticleColorKind.Palette, colors[0], colors[0], (Color[])colors.Clone());
    }

    internal Color Sample(
        ParticleRandom random)
    {
        switch (this.Kind)
        {
            case ParticleColorKind.Between:
                return Color.Lerp(this.From, this.To, random.NextSingle());

            case ParticleColorKind.Palette:
                return this._palette![random.Next(this._palette.Length)];

            default:
                return this.From;
        }
    }
}

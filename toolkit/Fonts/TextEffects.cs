// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// The visual effects of a piece of text: a shadow, a glow and outlines behind it, and the
/// emboldening and softness of its face. <c>DrawString</c>
/// draws them with the text, in one call.
/// </summary>
/// <remarks>
/// <para>
/// Every distance is in pixels of the font's native size (<see cref="SpriteFont.EmSize"/>), so
/// effects scale with the text and keep their proportions at every size. The shadow's offset is
/// in the text's own space, so it rotates with rotated text.
/// </para>
/// <para>
/// The effects of a string are drawn before its face, across all its glyphs: every glyph's
/// shadow, then every glyph's glow, then every glyph's outline (each of <see cref="Outlines"/> in
/// turn, then <see cref="Outline"/>), then every glyph's face. Glyphs
/// overlap (kerned pairs, tight tracking, outlines wider than the gap between glyphs), so drawing
/// each glyph's outline and face together would let one glyph's outline cover its neighbor's
/// face.
/// </para>
/// <para>
/// An effect reaches only as far from the glyphs' edges as the font's atlas encodes distances:
/// half the distance range it was built with. Each effect's width (or spread) plus half its
/// softness has to fit, and is limited to it when drawn. Build fonts with a distance range of at
/// least twice the widest effect. Effects aren't drawn for bitmap fonts.
/// </para>
/// <para>
/// Effects are values: share one <see cref="TextEffects"/> between many strings, and derive
/// others with <c>with</c> (<c>title with { Outline = null }</c>).
/// </para>
/// </remarks>
public sealed record TextEffects
{
    /// <summary>
    /// Text with no effects.
    /// </summary>
    public static TextEffects None { get; } = new();

    /// <summary>
    /// Gets the shadow drawn behind everything else, or <c>null</c> for none.
    /// </summary>
    public TextShadow? Shadow { get; init; }

    /// <summary>
    /// Gets the glow drawn behind the outline and the face, or <c>null</c> for none.
    /// </summary>
    public TextGlow? Glow { get; init; }

    /// <summary>
    /// Gets the outline drawn behind the face, or <c>null</c> for none. It's drawn after
    /// <see cref="Outlines"/>, over them.
    /// </summary>
    public TextOutline? Outline { get; init; }

    /// <summary>
    /// Gets outlines drawn behind <see cref="Outline"/> and the face, in order, each over the one
    /// before (so the widest comes first), or <c>null</c> for none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each is measured from the face's edges, so one round another is as wide as both: a band
    /// 3 wide round an outline 2 wide is an outline 5 wide drawn first. An outline moved down
    /// (<see cref="TextOutline.Offset"/>) is the depth under the one after it.
    /// </para>
    /// <para>
    /// Effects with the same outlines are equal, whether or not they share the list.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Cream letters in a dark orange line, in a gold band, each with a depth under it
    /// var title = new TextEffects
    /// {
    ///     Outlines =
    ///     [
    ///         new TextOutline(deepBrown, Width: 6, TextCorners.Round) { Offset = new Vector2(0, 3) },
    ///         new TextOutline(gold, Width: 6, TextCorners.Round) { Gradient = new TextGradient(yellow, orange) },
    ///         new TextOutline(darkOrange, Width: 3, TextCorners.Round) { Offset = new Vector2(0, 3) },
    ///     ],
    ///     Outline = new TextOutline(orange, Width: 3, TextCorners.Round),
    /// };
    /// </code>
    /// </example>
    public IReadOnlyList<TextOutline>? Outlines { get; init; }

    /// <summary>
    /// Gets the two colors the face is drawn in, top to bottom of each line, or <c>null</c> to
    /// draw it in the text's color. A character given a color of its own
    /// (<see cref="ICharacterAttributes.Color"/>) keeps that color.
    /// </summary>
    public TextGradient? Gradient { get; init; }

    /// <summary>
    /// Gets a slanted band of light across the face, or <c>null</c> for none.
    /// </summary>
    public TextSheen? Sheen { get; init; }

    /// <summary>
    /// Gets how far the face's edges move outward, in pixels of the font's native size: positive
    /// is bolder, negative thinner. The other effects are measured from the face's edges.
    /// </summary>
    public float Embolden { get; init; }

    /// <summary>
    /// Gets how much wider than a screen pixel the face's edge fades, in pixels of the font's
    /// native size.
    /// </summary>
    public float Softness { get; init; }

    /// <summary>
    /// Gets whether anything is drawn behind the glyphs: a shadow, glow or outline. Text drawn in
    /// several calls (line by line) with such effects draws every call's
    /// <see cref="TextLayers.Effects"/> before any call's <see cref="TextLayers.Face"/>, so no
    /// line's effects cover another's glyphs; without them one pass is enough.
    /// </summary>
    public bool HasBackgroundEffects => (this.Shadow is not null) || (this.Glow is not null) || (this.Outline is not null)
        || (this.Outlines is { Count: > 0 });

    /// <summary>
    /// Gets how far the effects reach beyond the glyphs on each side, in pixels of the font's
    /// native size, for layouts that make room for them. Measuring text ignores effects.
    /// </summary>
    public Thickness GetExtent()
    {
        // The face's own emboldening and softness, which the other effects start from
        var face = MathF.Max(this.Embolden, 0) + (this.Softness / 2);
        var left = face;
        var top = face;
        var right = face;
        var bottom = face;

        void Include(float reach, Vector2 offset)
        {
            left = MathF.Max(left, reach - offset.X);
            right = MathF.Max(right, reach + offset.X);
            top = MathF.Max(top, reach - offset.Y);
            bottom = MathF.Max(bottom, reach + offset.Y);
        }

        var embolden = MathF.Max(this.Embolden, 0);
        if (this.Outline is { } outline)
        {
            Include(embolden + outline.Width + (outline.Softness / 2), outline.Offset);
        }

        if (this.Outlines is { } outlines)
        {
            for (int i = 0; i < outlines.Count; i++)
            {
                var each = outlines[i];
                Include(embolden + each.Width + (each.Softness / 2), each.Offset);
            }
        }

        if (this.Glow is { } glow)
        {
            Include(embolden + glow.Width + (glow.Softness / 2), Vector2.Zero);
        }

        if (this.Shadow is { } shadow)
        {
            Include(embolden + shadow.Spread + (shadow.Softness / 2), shadow.Offset);
        }

        return new Thickness(left, top, right, bottom);
    }

    /// <summary>
    /// Gets whether these effects are the same as others: the same in every effect, with the
    /// same outlines in the same order.
    /// </summary>
    /// <param name="other">The other effects.</param>
    public bool Equals(
        TextEffects? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (other is not null)
            && (this.Shadow == other.Shadow)
            && (this.Glow == other.Glow)
            && (this.Outline == other.Outline)
            && (this.Gradient == other.Gradient)
            && (this.Sheen == other.Sheen)
            && this.Embolden.Equals(other.Embolden)
            && this.Softness.Equals(other.Softness)
            && SameOutlines(this.Outlines, other.Outlines);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(this.Shadow);
        hash.Add(this.Glow);
        hash.Add(this.Outline);
        hash.Add(this.Gradient);
        hash.Add(this.Sheen);
        hash.Add(this.Embolden);
        hash.Add(this.Softness);
        if (this.Outlines is { } outlines)
        {
            for (int i = 0; i < outlines.Count; i++)
            {
                hash.Add(outlines[i]);
            }
        }

        return hash.ToHashCode();
    }

    // No outlines and an empty list are the same
    private static bool SameOutlines(
        IReadOnlyList<TextOutline>? a,
        IReadOnlyList<TextOutline>? b)
    {
        int count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0))
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (a![i] != b![i])
            {
                return false;
            }
        }

        return true;
    }
}

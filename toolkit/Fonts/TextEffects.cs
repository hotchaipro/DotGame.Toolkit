// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// The visual effects of a piece of text: underlays under its face (outlines, shadows, glows,
/// depths), and the face's own gradient, sheen, emboldening and softness. <c>DrawString</c> draws
/// them with the text, in one call.
/// </summary>
/// <remarks>
/// <para>
/// Every distance is in pixels of the font's native size (<see cref="SpriteFont.EmSize"/>), so
/// effects scale with the text and keep their proportions at every size. An underlay's offset is in
/// the text's own space, so it turns with turned text.
/// </para>
/// <para>
/// The underlays of a string are drawn before its face, across all its glyphs: every glyph of the
/// farthest underlay, then every glyph of the next, and so on to the nearest, then every glyph's
/// face. Glyphs overlap (kerned pairs, tight tracking, outlines wider than the gap between glyphs),
/// so drawing each glyph's underlays and face together would let one glyph's outline cover its
/// neighbor's face.
/// </para>
/// <para>
/// An underlay reaches only as far from the glyphs' edges as the font's atlas encodes distances:
/// half the distance range it was built with. Each underlay's width plus half its softness has to
/// fit, and is limited to it when drawn. Build fonts with a distance range of at least twice the
/// widest underlay. Effects aren't drawn for bitmap fonts.
/// </para>
/// <para>
/// Effects are values: share one <see cref="TextEffects"/> between many strings, and derive
/// others with <c>with</c> (<c>title with { Underlays = null }</c>). Effects with the same
/// underlays are equal, whether or not they share the list.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // An outline with a soft shadow under it
/// var label = new TextEffects
/// {
///     Underlays = [TextUnderlay.Outline(Color.Black, 2), TextUnderlay.Shadow(Color.Black * 0.5f, new Vector2(2, 3), softness: 2)],
/// };
///
/// // Cream letters in a dark orange line, in a gold band, with a depth under each
/// var banner = new TextEffects
/// {
///     Underlays =
///     [
///         TextUnderlay.Outline(darkOrange, 3, TextCorners.Round),
///         TextUnderlay.Outline(darkerOrange, 3, TextCorners.Round) with { Offset = new Vector2(0, 3) },
///         TextUnderlay.Outline(gold, 6, TextCorners.Round) with { Gradient = new TextGradient(yellow, orange) },
///         TextUnderlay.Outline(deepBrown, 6, TextCorners.Round) with { Offset = new Vector2(0, 3) },
///     ],
/// };
/// </code>
/// </example>
public sealed record TextEffects
{
    /// <summary>
    /// Text with no effects.
    /// </summary>
    public static TextEffects None { get; } = new();

    /// <summary>
    /// Gets what's drawn under the face, nearest the face first, or <c>null</c> for nothing.
    /// </summary>
    /// <remarks>
    /// Listed as CSS lists a <c>text-shadow</c>'s, and as a layers panel does: each underlay is drawn
    /// under the ones before it, so the first is nearest the face and the last farthest from it. Each
    /// is measured from the face's edges, so a band 3 wide round an outline 2 wide is an underlay 5
    /// wide after it; moved down by its <see cref="TextUnderlay.Offset"/>, an underlay is the depth
    /// under the ones before it.
    /// </remarks>
    public IReadOnlyList<TextUnderlay>? Underlays { get; init; }

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
    /// is bolder, negative thinner. The underlays are measured from the face's edges.
    /// </summary>
    public float Embolden { get; init; }

    /// <summary>
    /// Gets how much wider than a screen pixel the face's edge fades, in pixels of the font's
    /// native size.
    /// </summary>
    public float Softness { get; init; }

    /// <summary>
    /// Gets whether anything is drawn under the face. Text drawn in several calls (line by line)
    /// with underlays draws every call's <see cref="TextLayers.Underlays"/> before any call's
    /// <see cref="TextLayers.Face"/>, so no line's underlays cover another's glyphs; without them
    /// one pass is enough.
    /// </summary>
    public bool HasUnderlays => this.Underlays is { Count: > 0 };

    /// <summary>
    /// Gets how far the effects reach beyond the glyphs on each side, in pixels of the font's
    /// native size, for layouts that make room for them. Measuring text ignores effects.
    /// </summary>
    /// <returns>How far they reach on each side.</returns>
    public Thickness GetExtent()
    {
        // The face's own emboldening and softness, which the underlays start from
        var face = MathF.Max(this.Embolden, 0) + (this.Softness / 2);
        var left = face;
        var top = face;
        var right = face;
        var bottom = face;

        var embolden = MathF.Max(this.Embolden, 0);
        if (this.Underlays is { } underlays)
        {
            for (int i = 0; i < underlays.Count; i++)
            {
                var underlay = underlays[i];
                var reach = embolden + underlay.Width + (underlay.Softness / 2);
                left = MathF.Max(left, reach - underlay.Offset.X);
                right = MathF.Max(right, reach + underlay.Offset.X);
                top = MathF.Max(top, reach - underlay.Offset.Y);
                bottom = MathF.Max(bottom, reach + underlay.Offset.Y);
            }
        }

        return new Thickness(left, top, right, bottom);
    }

    /// <summary>
    /// Gets whether these effects are the same as others: the same face, and the same underlays in
    /// the same order, whether or not they share the list.
    /// </summary>
    /// <param name="other">The other effects.</param>
    /// <returns>Whether they're the same.</returns>
    public bool Equals(
        TextEffects? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return (other is not null)
            && (this.Gradient == other.Gradient)
            && (this.Sheen == other.Sheen)
            && this.Embolden.Equals(other.Embolden)
            && this.Softness.Equals(other.Softness)
            && SameUnderlays(this.Underlays, other.Underlays);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(this.Gradient);
        hash.Add(this.Sheen);
        hash.Add(this.Embolden);
        hash.Add(this.Softness);
        if (this.Underlays is { } underlays)
        {
            for (int i = 0; i < underlays.Count; i++)
            {
                hash.Add(underlays[i]);
            }
        }

        return hash.ToHashCode();
    }

    // No underlays and an empty list are the same
    private static bool SameUnderlays(
        IReadOnlyList<TextUnderlay>? a,
        IReadOnlyList<TextUnderlay>? b)
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

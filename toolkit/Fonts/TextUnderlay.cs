// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A copy of text's glyphs drawn under its face: grown by a width, moved, softened and painted.
/// An outline, a shadow, a glow and the depth under a title are all underlays.
/// </summary>
/// <remarks>
/// <para>
/// An underlay is a CSS <c>text-shadow</c> with a spread: <see cref="Width"/> grows the glyphs from
/// the face's edges (the spread), <see cref="Offset"/> moves them, <see cref="Softness"/> fades their
/// edge (the blur), and <see cref="Color"/> or <see cref="Gradient"/> paints them. With no offset or
/// softness it's an outline; moved and softened, a shadow; softened and round, a glow; moved by a
/// little and hard-edged, the depth under the underlay nearer the face.
/// </para>
/// <para>
/// <see cref="Outline"/>, <see cref="Shadow"/> and <see cref="Glow"/> make the usual ones; any
/// of them can be changed with <c>with</c>.
/// </para>
/// <para>
/// Every distance is in pixels of the font's native size (<see cref="SpriteFont.EmSize"/>), so an
/// underlay scales with the text. The offset is in the text's own space, so it turns with turned
/// text.
/// </para>
/// </remarks>
/// <param name="Color">The underlay's color, unless it has a <see cref="Gradient"/>.</param>
/// <param name="Width">
/// How far the underlay extends beyond the face's edges, in pixels of the font's native size (to the
/// middle of its fade, when it's soft). Zero is the glyphs' own shape, as a hard shadow is.
/// </param>
public readonly record struct TextUnderlay(
    Color Color,
    float Width = 0)
{
    /// <summary>
    /// Gets where the underlay is drawn relative to the text, in pixels of the font's native size, in
    /// the text's own space.
    /// </summary>
    public Vector2 Offset { get; init; }

    /// <summary>
    /// Gets how much wider than a screen pixel the underlay's edge fades, in pixels of the font's
    /// native size.
    /// </summary>
    public float Softness { get; init; }

    /// <summary>
    /// Gets how the underlay's corners are drawn: <see cref="TextCorners.Sharp"/>, following the
    /// glyphs' corners, by default.
    /// </summary>
    public TextCorners Corners { get; init; }

    /// <summary>
    /// Gets, for <see cref="TextCorners.Sharp"/> corners, how many times the width a corner may extend
    /// before it's cut back, as SVG's <c>stroke-miterlimit</c>; <see cref="float.PositiveInfinity"/>
    /// never cuts them. 4 by default.
    /// </summary>
    public float MiterLimit { get; init; } = DefaultMiterLimit;

    /// <summary>
    /// Gets the two colors the underlay is drawn in, top to bottom of each line as the face's are
    /// (<see cref="TextEffects.Gradient"/>), or <c>null</c> to draw it in <see cref="Color"/>.
    /// </summary>
    public TextGradient? Gradient { get; init; }

    // The miter limit of an underlay's corners unless it sets its own, the face's and SVG's
    internal const float DefaultMiterLimit = 4;

    /// <summary>
    /// Gets an outline: the glyphs grown by a width, drawn in place.
    /// </summary>
    /// <param name="color">The outline's color.</param>
    /// <param name="width">How far it extends beyond the face's edges, in pixels of the font's native size.</param>
    /// <param name="corners">How its corners are drawn.</param>
    /// <returns>The outline.</returns>
    public static TextUnderlay Outline(
        Color color,
        float width,
        TextCorners corners = TextCorners.Sharp)
    {
        return new TextUnderlay(color, width) { Corners = corners };
    }

    /// <summary>
    /// Gets a shadow: the glyphs moved, and softened if <paramref name="softness"/> isn't zero.
    /// </summary>
    /// <param name="color">The shadow's color.</param>
    /// <param name="offset">Where it's drawn relative to the text, in pixels of the font's native size.</param>
    /// <param name="softness">How much wider than a screen pixel its edge fades.</param>
    /// <param name="spread">How far it extends beyond the face's edges.</param>
    /// <returns>The shadow.</returns>
    public static TextUnderlay Shadow(
        Color color,
        Vector2 offset,
        float softness = 0,
        float spread = 0)
    {
        return new TextUnderlay(color, spread) { Offset = offset, Softness = softness };
    }

    /// <summary>
    /// Gets a glow: the glyphs grown and softened, with round corners.
    /// </summary>
    /// <param name="color">The glow's color.</param>
    /// <param name="width">How far it extends beyond the face's edges, to the middle of its fade.</param>
    /// <param name="softness">How much wider than a screen pixel its edge fades.</param>
    /// <returns>The glow.</returns>
    public static TextUnderlay Glow(
        Color color,
        float width,
        float softness)
    {
        return new TextUnderlay(color, width) { Softness = softness, Corners = TextCorners.Round };
    }
}

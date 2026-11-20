// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// An outline around text, drawn behind its face.
/// </summary>
/// <remarks>
/// Text can have several, one round another (<see cref="TextEffects.Outlines"/>): a dark line
/// round the letters, a wider band of another color round that, and each moved down a little
/// (<see cref="Offset"/>) as the depth under it.
/// </remarks>
/// <param name="Color">The outline's color.</param>
/// <param name="Width">How far the outline extends beyond the face's edges, in pixels of the font's native size.</param>
/// <param name="Corners">How the outline's corners are drawn.</param>
/// <param name="MiterLimit">
/// For <see cref="TextCorners.Sharp"/> corners, how many times the width a corner may extend
/// before it's cut back, as SVG's <c>stroke-miterlimit</c>; <see cref="float.PositiveInfinity"/>
/// never cuts them.
/// </param>
/// <param name="Softness">How much wider than a screen pixel the outline's edge fades, in pixels of the font's native size.</param>
public readonly record struct TextOutline(
    Color Color,
    float Width,
    TextCorners Corners = TextCorners.Sharp,
    float MiterLimit = 4,
    float Softness = 0)
{
    /// <summary>
    /// Gets where the outline is drawn relative to the text, in pixels of the font's native size:
    /// moved down, an outline is the depth under the text or under another outline.
    /// </summary>
    public Vector2 Offset { get; init; }

    /// <summary>
    /// Gets the two colors the outline is drawn in, top to bottom of each line as a face's
    /// gradient is (<see cref="TextEffects.Gradient"/>), or <c>null</c> to draw it in
    /// <see cref="Color"/>.
    /// </summary>
    public TextGradient? Gradient { get; init; }
}

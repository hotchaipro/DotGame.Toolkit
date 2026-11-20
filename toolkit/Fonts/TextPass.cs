// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// One pass over a string's glyphs: an underlay or the face, with the settings each of its quads
/// carries to the text shader.
/// </summary>
internal readonly struct TextPass
{
    internal TextPass(
        bool isFace,
        Color color,
        Vector2 offset,
        in ShaderCustomData data,
        TextGradient? gradient = null)
    {
        this.IsFace = isFace;
        this.Color = color;
        this.Offset = offset;
        this.Data = data;
        this.Gradient = gradient;
    }

    /// <summary>
    /// Gets whether this is the face, which is drawn in the text's colors; an underlay has its own.
    /// </summary>
    internal bool IsFace { get; }

    /// <summary>
    /// Gets the underlay's color. Unused for the face.
    /// </summary>
    internal Color Color { get; }

    /// <summary>
    /// Gets where the pass is drawn relative to the text, in pixels of the font's native size.
    /// </summary>
    internal Vector2 Offset { get; }

    /// <summary>
    /// Gets the settings each quad of the pass carries to the text shader.
    /// </summary>
    internal ShaderCustomData Data { get; }

    /// <summary>
    /// Gets the underlay's two colors, top to bottom, in place of <see cref="Color"/>, or
    /// <c>null</c>. Unused for the face, whose gradient is the effects' own.
    /// </summary>
    internal TextGradient? Gradient { get; }

    /// <summary>
    /// Gets how many passes text with these effects can draw: its underlays and its face.
    /// </summary>
    internal static int GetMaxCount(
        TextEffects? effects) => (effects?.Underlays?.Count ?? 0) + 1;
}

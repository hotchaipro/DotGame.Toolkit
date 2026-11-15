// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// One pass over a string's glyphs: its shadow, glow, outline or face, with the settings each of
/// its quads carries to the text shader.
/// </summary>
internal readonly struct TextLayer
{
    /// <summary>
    /// The most layers a string draws: shadow, glow, outline and face.
    /// </summary>
    internal const int MaxCount = 4;

    internal TextLayer(
        bool isFace,
        Color color,
        Vector2 offset,
        in ShaderCustomData data)
    {
        this.IsFace = isFace;
        this.Color = color;
        this.Offset = offset;
        this.Data = data;
    }

    /// <summary>
    /// Gets whether this is the face, which is drawn in the text's colors; an effect has its own.
    /// </summary>
    internal bool IsFace { get; }

    /// <summary>
    /// Gets the effect's color. Unused for the face.
    /// </summary>
    internal Color Color { get; }

    /// <summary>
    /// Gets where the layer is drawn relative to the text, in pixels of the font's native size.
    /// </summary>
    internal Vector2 Offset { get; }

    /// <summary>
    /// Gets the settings each quad of the layer carries to the text shader.
    /// </summary>
    internal ShaderCustomData Data { get; }
}

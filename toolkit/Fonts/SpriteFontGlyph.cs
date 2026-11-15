// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// One glyph of a <see cref="SpriteFont"/>: its atlas region and its metrics.
/// </summary>
/// <remarks>
/// Metrics are in pixels at the font's <see cref="SpriteFont.EmSize"/>, relative to the pen
/// position on the baseline, with y increasing downwards. The bounds describe the whole atlas
/// region, including the distance range around the outline, so drawing the region at
/// (<see cref="Left"/>, <see cref="Top"/>) places the glyph exactly.
/// </remarks>
internal sealed class SpriteFontGlyph
{
    /// <summary>Makes a glyph, as the loader does.</summary>
    public SpriteFontGlyph(
        Texture texture,
        int page,
        RectangleInt sourceRectangle,
        int codePoint,
        float advance,
        float left,
        float top,
        float right,
        float bottom)
    {
        this.Texture = texture;
        this.Page = page;
        this.SourceRectangle = sourceRectangle;
        this.CodePoint = codePoint;
        this.Advance = advance;
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
    }

    /// <summary>The atlas page that holds the glyph.</summary>
    public readonly Texture Texture;

    /// <summary>The index of the atlas page that holds the glyph.</summary>
    public readonly int Page;

    /// <summary>The glyph's region of its atlas page, in texels. Empty for blank glyphs such as space.</summary>
    public readonly RectangleInt SourceRectangle;

    /// <summary>The Unicode code point that the glyph draws.</summary>
    public readonly int CodePoint;

    /// <summary>The distance the pen moves after the glyph (the sum of the A, B and C widths).</summary>
    public readonly float Advance;

    /// <summary>The left edge of the glyph's region relative to the pen (the A width, less the padding).</summary>
    public readonly float Left;

    /// <summary>The top edge of the glyph's region relative to the baseline (negative above it).</summary>
    public readonly float Top;

    /// <summary>The right edge of the glyph's region relative to the pen.</summary>
    public readonly float Right;

    /// <summary>The bottom edge of the glyph's region relative to the baseline.</summary>
    public readonly float Bottom;

    /// <summary>The width of the glyph's region, in the atlas's pixels.</summary>
    public int Width => this.SourceRectangle.Width;

    /// <summary>The height of the glyph's region, in the atlas's pixels.</summary>
    public int Height => this.SourceRectangle.Height;
}

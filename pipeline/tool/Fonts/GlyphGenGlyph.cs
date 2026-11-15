// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// Builds a SpriteFontContent item: reads the primary font and each fallback font (FontFile:
// metrics, names, character map, advances, kerning, cap height), renders their glyphs with
// glyphgen (outlines to pixels only), packs the glyph bitmaps into one or more atlas pages and
// writes the .spritefont descriptor (PBON).

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

internal sealed class GlyphGenGlyph
{
    public int CodePoint;
    public int GlyphIndex;
    public double Advance, Left, Bottom, Right, Top;
    public int Width, Height, Channels;
    public byte[] Pixels = Array.Empty<byte>();
    public int Source;
    public int Page, X, Y;
}

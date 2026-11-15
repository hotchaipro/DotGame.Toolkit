// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// Builds a SpriteFontContent item: reads the primary font and each fallback font (FontFile:
// metrics, names, character map, advances, kerning, cap height), renders their glyphs with
// glyphgen (outlines to pixels only), packs the glyph bitmaps into one or more atlas pages and
// writes the .spritefont descriptor (PBON).

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

// glyphgen's output: the rendered glyphs, by glyph index
internal sealed class GlyphGenResult
{
    public List<GlyphGenGlyph> Glyphs = new List<GlyphGenGlyph>();
}

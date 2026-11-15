// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// Builds a SpriteFontContent item: reads the primary font and each fallback font (FontFile:
// metrics, names, character map, advances, kerning, cap height), renders their glyphs with
// glyphgen (outlines to pixels only), packs the glyph bitmaps into one or more atlas pages and
// writes the .spritefont descriptor (PBON).

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

internal sealed class SpriteFontBuildResult
{
    public List<FontPage> Pages = new List<FontPage>();
    public List<GlyphGenGlyph> Glyphs = new List<GlyphGenGlyph>();
    public List<KerningPair> Kerning = new List<KerningPair>();
    public List<int> MissingCodePoints = new List<int>();
    public FontFile? Primary;
    public List<string> SourceFamilies = new List<string>();
    public List<string> SourceFiles = new List<string>();

    // The primary font's cap height, in ems, or null if the font doesn't say
    public double? CapHeight;
}

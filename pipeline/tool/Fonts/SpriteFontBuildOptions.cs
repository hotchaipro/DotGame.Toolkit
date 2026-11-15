// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// Builds a SpriteFontContent item: reads the primary font and each fallback font (FontFile:
// metrics, names, character map, advances, kerning, cap height), renders their glyphs with
// glyphgen (outlines to pixels only), packs the glyph bitmaps into one or more atlas pages and
// writes the .spritefont descriptor (PBON).

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

internal sealed class SpriteFontBuildOptions
{
    public string GlyphGenExe = "";
    public string PrimaryFont = "";
    public string[] FallbackFonts = Array.Empty<string>();
    public string Charset = "";
    public string[] CharsetFiles = Array.Empty<string>();
    public float Size = 32f;
    public float PxRange = 4f;
    public float PxRangeOffset;
    public GlyphFormat Format = GlyphFormat.Msdf;
    public bool Kerning;
    public int MaxPageWidth = 2048;
    public int MaxPageHeight = 2048;
    public bool PowerOfTwo;
    public string IntermediateDir = "";
    public string Name = "";
}

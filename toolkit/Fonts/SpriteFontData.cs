// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// The contents of a <c>.spritefont</c> file, before its atlas pages are loaded.
/// </summary>
internal sealed class SpriteFontData
{
    public int Version;
    public GlyphFormat GlyphFormat = GlyphFormat.Msdf;
    public float EmSize;
    public float? DistanceRange;
    public float DistanceRangeMiddle;
    public float LineHeight;
    public float Ascender;
    public float Descender;
    public float UnderlinePosition;
    public float UnderlineThickness;
    public float CapHeight;
    public readonly List<SpriteFontPageData> Pages = new();
    public readonly List<SpriteFontSourceData> Sources = new();
    public int GlyphCount;
    public int KerningCount;
    public ReadOnlyMemory<byte> Glyphs;
    public ReadOnlyMemory<byte> Kerning;
}

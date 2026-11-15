// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Fonts;

namespace DotGame.Toolkit.Tests.Fonts;

/// <summary>
/// Builds sprite fonts for tests without a graphics device.
/// </summary>
/// <remarks>
/// The glyph and kerning records are packed the way the SpriteFontContent pipeline writes them
/// (see <see cref="SpriteFontReader"/>), and the atlas pages are fake textures. Each glyph is
/// <c>Left</c> 1 and <c>Top</c> -10 from the pen, and its source rectangle starts 16 pixels
/// along per glyph before it in code point order.
/// </remarks>
internal sealed class TestFonts
{
    public const float EmSize = 32;
    public const float LineHeight = 40;
    public const float Ascender = 30;
    public const float Descender = -10;

    private readonly List<(int CodePoint, float Advance, int Width, int Page)> _glyphs = new();
    private readonly List<(int First, int Second, float Adjustment)> _kerning = new();
    private int _pageCount = 1;
    private GlyphFormat _format = GlyphFormat.Msdf;
    private float _distanceRange = 4;
    private float _distanceRangeMiddle = 0.5f;

    /// <summary>
    /// Adds a glyph for each character of <paramref name="characters"/> (a string, so it can
    /// hold surrogate pairs), each with the same advance.
    /// </summary>
    public TestFonts Glyphs(
        string characters,
        float advance = 10)
    {
        foreach (var rune in characters.EnumerateRunes())
        {
            this.Glyph(rune.Value, advance);
        }

        return this;
    }

    public TestFonts Glyph(
        int codePoint,
        float advance = 10,
        int width = 8,
        int page = 0)
    {
        this._glyphs.Add((codePoint, advance, width, page));
        return this;
    }

    public TestFonts Kerning(
        int first,
        int second,
        float adjustment)
    {
        this._kerning.Add((first, second, adjustment));
        return this;
    }

    public TestFonts Pages(
        int count)
    {
        this._pageCount = count;
        return this;
    }

    public TestFonts Format(
        GlyphFormat format)
    {
        this._format = format;
        return this;
    }

    public TestFonts DistanceRange(
        float range,
        float middle = 0)
    {
        this._distanceRange = range;
        this._distanceRangeMiddle = middle;
        return this;
    }

    public SpriteFont Build()
    {
        // The pipeline writes both kinds of record sorted
        var glyphs = this._glyphs.OrderBy(g => g.CodePoint).ToArray();
        var glyphRecords = new byte[glyphs.Length * SpriteFontReader.GlyphRecordSize];
        for (int i = 0; i < glyphs.Length; i++)
        {
            var record = glyphRecords.AsSpan(i * SpriteFontReader.GlyphRecordSize, SpriteFontReader.GlyphRecordSize);
            var glyph = glyphs[i];
            BinaryPrimitives.WriteInt32LittleEndian(record, glyph.CodePoint);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(4), (ushort)glyph.Page);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(6), (ushort)(i * 16));
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(8), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(10), (ushort)glyph.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(12), (ushort)(glyph.Width == 0 ? 0 : 12));
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(14), glyph.Advance);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(18), 1);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(22), -10);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(26), 1 + glyph.Width);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(30), 2);
        }

        var kerning = this._kerning.OrderBy(k => k.First).ThenBy(k => k.Second).ToArray();
        var kerningRecords = new byte[kerning.Length * SpriteFontReader.KerningRecordSize];
        for (int i = 0; i < kerning.Length; i++)
        {
            var record = kerningRecords.AsSpan(i * SpriteFontReader.KerningRecordSize, SpriteFontReader.KerningRecordSize);
            BinaryPrimitives.WriteInt32LittleEndian(record, kerning[i].First);
            BinaryPrimitives.WriteInt32LittleEndian(record.Slice(4), kerning[i].Second);
            BinaryPrimitives.WriteSingleLittleEndian(record.Slice(8), kerning[i].Adjustment);
        }

        var data = new SpriteFontData()
        {
            Version = SpriteFontReader.SupportedVersion,
            GlyphFormat = this._format,
            EmSize = EmSize,
            DistanceRange = this._format == GlyphFormat.Bitmap ? null : this._distanceRange,
            DistanceRangeMiddle = this._format == GlyphFormat.Bitmap ? 0 : this._distanceRangeMiddle,
            LineHeight = LineHeight,
            Ascender = Ascender,
            Descender = Descender,
            UnderlinePosition = 4,
            UnderlineThickness = 2,
            GlyphCount = glyphs.Length,
            KerningCount = kerning.Length,
            Glyphs = glyphRecords,
            Kerning = kerningRecords,
        };

        var textures = new List<Texture>();
        for (int i = 0; i < this._pageCount; i++)
        {
            data.Pages.Add(new SpriteFontPageData() { Name = $"page{i}", Width = 256, Height = 256 });
            textures.Add(TestTextures.Create(256, 256));
        }

        return new SpriteFont(data, textures, TestShaders.Shared);
    }
}

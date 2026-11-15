// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Buffers.Binary;
using System.IO;
using DotGame.Toolkit.Serialization;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Reads the <c>.spritefont</c> format written by the toolkit SpriteFontContent pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The file is a PBON object (see <see cref="PbonReader"/>) whose members are:
/// 1 version, 2 glyph format (<c>bitmap</c>, <c>sdf</c>, <c>msdf</c>, <c>mtsdf</c>), 3 em size,
/// 4 distance range and 5 distance range middle (distance-field formats only), 6 metrics
/// { 1 line height, 2 ascender, 3 descender, 4 underline position, 5 underline thickness },
/// 7 pages [ { 1 name, 2 width, 3 height } ], 8 sources [ { 1 font family, 2 source file } ],
/// 9 glyph count, 10 kerning pair count, 11 glyph records, 12 kerning records. Metrics are pixels
/// at the em size, y down. The writer puts the metadata first and the large record blobs last.
/// </para>
/// <para>
/// Glyph records are 34 bytes, little-endian, sorted by code point: int code point; ushort page,
/// x, y, width, height; float advance, left, top, right, bottom. Kerning records are 12 bytes,
/// sorted by pair: int first; int second; float adjustment.
/// </para>
/// </remarks>
internal static class SpriteFontReader
{
    public const int GlyphRecordSize = 34;
    public const int KerningRecordSize = 12;
    public const int SupportedVersion = 1;

    public static SpriteFontData Read(
        ReadOnlyMemory<byte> data)
    {
        var font = new SpriteFontData();
        var reader = new PbonReader(data);

        if (!reader.ReadStartObject())
        {
            throw new InvalidDataException("The sprite font file is empty.");
        }

        while (reader.TryReadMember(out var key))
        {
            switch (key)
            {
                case 1:
                    font.Version = reader.ReadInt32();
                    if (font.Version > SupportedVersion)
                    {
                        throw new InvalidDataException($"Unsupported sprite font version {font.Version}; rebuild the content with this toolkit.");
                    }
                    break;
                case 2:
                    font.GlyphFormat = ParseGlyphFormat(reader.ReadString());
                    break;
                case 3:
                    font.EmSize = reader.ReadSingle();
                    break;
                case 4:
                    font.DistanceRange = reader.ReadSingle();
                    break;
                case 5:
                    font.DistanceRangeMiddle = reader.ReadSingle();
                    break;
                case 6:
                    ReadMetrics(reader, font);
                    break;
                case 7:
                    ReadPages(reader, font);
                    break;
                case 8:
                    ReadSources(reader, font);
                    break;
                case 9:
                    font.GlyphCount = reader.ReadInt32();
                    break;
                case 10:
                    font.KerningCount = reader.ReadInt32();
                    break;
                case 11:
                    font.Glyphs = reader.ReadBytes();
                    break;
                case 12:
                    font.Kerning = reader.ReadBytes();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (font.Version == 0)
        {
            throw new InvalidDataException("The sprite font file has no version.");
        }

        if (font.EmSize <= 0)
        {
            throw new InvalidDataException("The sprite font file has no em size.");
        }

        if (font.Pages.Count == 0)
        {
            throw new InvalidDataException("The sprite font file has no atlas pages.");
        }

        if (font.Glyphs.Length != font.GlyphCount * GlyphRecordSize)
        {
            throw new InvalidDataException("The sprite font glyph records do not match the glyph count.");
        }

        if (font.Kerning.Length != 0 && font.Kerning.Length != font.KerningCount * KerningRecordSize)
        {
            throw new InvalidDataException("The sprite font kerning records do not match the kerning count.");
        }

        return font;
    }

    public static void ReadGlyph(
        ReadOnlySpan<byte> records,
        int index,
        out int codePoint,
        out int page,
        out int x,
        out int y,
        out int width,
        out int height,
        out float advance,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        var record = records.Slice(index * GlyphRecordSize, GlyphRecordSize);
        codePoint = BinaryPrimitives.ReadInt32LittleEndian(record);
        page = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(4));
        x = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(6));
        y = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(8));
        width = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(10));
        height = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(12));
        advance = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(14));
        left = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(18));
        top = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(22));
        right = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(26));
        bottom = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(30));
    }

    public static void ReadKerning(
        ReadOnlySpan<byte> records,
        int index,
        out int first,
        out int second,
        out float adjustment)
    {
        var record = records.Slice(index * KerningRecordSize, KerningRecordSize);
        first = BinaryPrimitives.ReadInt32LittleEndian(record);
        second = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(4));
        adjustment = BinaryPrimitives.ReadSingleLittleEndian(record.Slice(8));
    }

    private static GlyphFormat ParseGlyphFormat(
        string value)
    {
        return value switch
        {
            "bitmap" => GlyphFormat.Bitmap,
            "sdf" => GlyphFormat.Sdf,
            "msdf" => GlyphFormat.Msdf,
            "mtsdf" => GlyphFormat.Mtsdf,
            _ => throw new InvalidDataException($"Unknown sprite font glyph format '{value}'."),
        };
    }

    private static void ReadMetrics(
        PbonReader reader,
        SpriteFontData font)
    {
        if (!reader.ReadStartObject())
        {
            return;
        }

        while (reader.TryReadMember(out var key))
        {
            switch (key)
            {
                case 1: font.LineHeight = reader.ReadSingle(); break;
                case 2: font.Ascender = reader.ReadSingle(); break;
                case 3: font.Descender = reader.ReadSingle(); break;
                case 4: font.UnderlinePosition = reader.ReadSingle(); break;
                case 5: font.UnderlineThickness = reader.ReadSingle(); break;
                case 6: font.CapHeight = reader.ReadSingle(); break;
                default: reader.Skip(); break;
            }
        }
    }

    private static void ReadPages(
        PbonReader reader,
        SpriteFontData font)
    {
        if (!reader.ReadStartArray())
        {
            return;
        }

        while (reader.TryReadArrayElement())
        {
            var page = new SpriteFontPageData();
            if (reader.ReadStartObject())
            {
                while (reader.TryReadMember(out var key))
                {
                    switch (key)
                    {
                        case 1: page.Name = reader.ReadString(); break;
                        case 2: page.Width = reader.ReadInt32(); break;
                        case 3: page.Height = reader.ReadInt32(); break;
                        default: reader.Skip(); break;
                    }
                }
            }

            font.Pages.Add(page);
        }
    }

    private static void ReadSources(
        PbonReader reader,
        SpriteFontData font)
    {
        if (!reader.ReadStartArray())
        {
            return;
        }

        while (reader.TryReadArrayElement())
        {
            var source = new SpriteFontSourceData();
            if (reader.ReadStartObject())
            {
                while (reader.TryReadMember(out var key))
                {
                    switch (key)
                    {
                        case 1: source.FontFamily = reader.ReadString(); break;
                        case 2: source.SourceFile = reader.ReadString(); break;
                        default: reader.Skip(); break;
                    }
                }
            }

            font.Sources.Add(source);
        }
    }
}

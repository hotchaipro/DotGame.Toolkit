// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;
using System.Text;

namespace DotGame.Toolkit.ContentPipeline.Tests.Fonts;

/// <summary>
/// Writes small sfnt files table by table, so a font test says exactly which bytes it's about.
/// </summary>
internal sealed class SfntBuilder
{
    private readonly SortedDictionary<string, byte[]> _tables = new(StringComparer.Ordinal);

    public SfntBuilder()
    {
        this.Head(1000);
        this.Hhea(800, -200, 100);
    }

    public SfntBuilder Table(string tag, byte[] data)
    {
        this._tables[tag] = data;
        return this;
    }

    public SfntBuilder Without(string tag)
    {
        this._tables.Remove(tag);
        return this;
    }

    public SfntBuilder Head(int unitsPerEm, int indexToLocFormat = 0)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), (ushort)unitsPerEm);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), (short)indexToLocFormat);
        return this.Table("head", head);
    }

    public SfntBuilder Hhea(int ascender, int descender, int lineGap, int metricCount = 0)
    {
        var hhea = new byte[36];
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(4), (short)ascender);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(6), (short)descender);
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(8), (short)lineGap);
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34), (ushort)metricCount);
        return this.Table("hhea", hhea);
    }

    public SfntBuilder Hmtx(params int[] advances)
    {
        var hmtx = new byte[advances.Length * 4];
        for (int i = 0; i < advances.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(hmtx.AsSpan(i * 4), (ushort)advances[i]);
        }

        return this.Table("hmtx", hmtx);
    }

    public SfntBuilder Os2(int typoAscender = 0, int typoDescender = 0, int typoLineGap = 0, int winAscent = 0, int winDescent = 0, int fsSelection = 0, int capHeight = 0, int version = 4)
    {
        var os2 = new byte[96];
        BinaryPrimitives.WriteUInt16BigEndian(os2, (ushort)version);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(62), (ushort)fsSelection);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(68), (short)typoAscender);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(70), (short)typoDescender);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(72), (short)typoLineGap);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(74), (ushort)winAscent);
        BinaryPrimitives.WriteUInt16BigEndian(os2.AsSpan(76), (ushort)winDescent);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(88), (short)capHeight);
        return this.Table("OS/2", os2);
    }

    public SfntBuilder Post(int underlinePosition, int underlineThickness)
    {
        var post = new byte[32];
        BinaryPrimitives.WriteInt16BigEndian(post.AsSpan(8), (short)underlinePosition);
        BinaryPrimitives.WriteInt16BigEndian(post.AsSpan(10), (short)underlineThickness);
        return this.Table("post", post);
    }

    /// <summary>Records of platform, encoding, language, name ID and text, UTF-16 or Mac Roman by platform.</summary>
    public SfntBuilder Name(params (int Platform, int Encoding, int Language, int NameId, string Text)[] records)
    {
        var strings = new MemoryStream();
        var table = new byte[6 + (records.Length * 12)];
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), (ushort)records.Length);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(4), (ushort)table.Length);
        for (int i = 0; i < records.Length; i++)
        {
            var record = records[i];
            byte[] text = record.Platform == 1 ? Encoding.Latin1.GetBytes(record.Text) : Encoding.BigEndianUnicode.GetBytes(record.Text);
            Span<byte> entry = table.AsSpan(6 + (i * 12));
            BinaryPrimitives.WriteUInt16BigEndian(entry, (ushort)record.Platform);
            BinaryPrimitives.WriteUInt16BigEndian(entry[2..], (ushort)record.Encoding);
            BinaryPrimitives.WriteUInt16BigEndian(entry[4..], (ushort)record.Language);
            BinaryPrimitives.WriteUInt16BigEndian(entry[6..], (ushort)record.NameId);
            BinaryPrimitives.WriteUInt16BigEndian(entry[8..], (ushort)text.Length);
            BinaryPrimitives.WriteUInt16BigEndian(entry[10..], (ushort)strings.Length);
            strings.Write(text);
        }

        return this.Table("name", [.. table, .. strings.ToArray()]);
    }

    /// <summary>A cmap of the given subtables: platform, encoding and the subtable's bytes.</summary>
    public SfntBuilder Cmap(params (int Platform, int Encoding, byte[] Subtable)[] subtables)
    {
        var header = new byte[4 + (subtables.Length * 8)];
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), (ushort)subtables.Length);
        var body = new MemoryStream();
        for (int i = 0; i < subtables.Length; i++)
        {
            Span<byte> record = header.AsSpan(4 + (i * 8));
            BinaryPrimitives.WriteUInt16BigEndian(record, (ushort)subtables[i].Platform);
            BinaryPrimitives.WriteUInt16BigEndian(record[2..], (ushort)subtables[i].Encoding);
            BinaryPrimitives.WriteUInt32BigEndian(record[4..], (uint)(header.Length + body.Length));
            body.Write(subtables[i].Subtable);
        }

        return this.Table("cmap", [.. header, .. body.ToArray()]);
    }

    public static byte[] CmapFormat0(params (int Code, int Glyph)[] map)
    {
        var table = new byte[262];
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), 262);
        foreach ((int code, int glyph) in map)
        {
            table[6 + code] = (byte)glyph;
        }

        return table;
    }

    /// <summary>Format 4 with one segment per entry (delta form), plus the required end segment.</summary>
    public static byte[] CmapFormat4(params (int Start, int End, int Delta)[] segments)
    {
        int count = segments.Length + 1;
        var table = new byte[16 + (count * 8)];
        BinaryPrimitives.WriteUInt16BigEndian(table, 4);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), (ushort)table.Length);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(6), (ushort)(count * 2));
        for (int i = 0; i < count; i++)
        {
            int start = i < segments.Length ? segments[i].Start : 0xFFFF;
            int end = i < segments.Length ? segments[i].End : 0xFFFF;
            int delta = i < segments.Length ? segments[i].Delta : 1;
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(14 + (i * 2)), (ushort)end);
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(16 + (count * 2) + (i * 2)), (ushort)start);
            BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(16 + (count * 4) + (i * 2)), (short)delta);
        }

        return table;
    }

    public static byte[] CmapFormat6(int first, params int[] glyphs)
    {
        var table = new byte[10 + (glyphs.Length * 2)];
        BinaryPrimitives.WriteUInt16BigEndian(table, 6);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(2), (ushort)table.Length);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(6), (ushort)first);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(8), (ushort)glyphs.Length);
        for (int i = 0; i < glyphs.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(10 + (i * 2)), (ushort)glyphs[i]);
        }

        return table;
    }

    public static byte[] CmapFormat12(int format, params (uint Start, uint End, uint Glyph)[] groups)
    {
        var table = new byte[16 + (groups.Length * 12)];
        BinaryPrimitives.WriteUInt16BigEndian(table, (ushort)format);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(4), (uint)table.Length);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(12), (uint)groups.Length);
        for (int i = 0; i < groups.Length; i++)
        {
            Span<byte> group = table.AsSpan(16 + (i * 12));
            BinaryPrimitives.WriteUInt32BigEndian(group, groups[i].Start);
            BinaryPrimitives.WriteUInt32BigEndian(group[4..], groups[i].End);
            BinaryPrimitives.WriteUInt32BigEndian(group[8..], groups[i].Glyph);
        }

        return table;
    }

    /// <summary>A legacy kern table: subtables of coverage and (left, right, value) pairs.</summary>
    public SfntBuilder Kern(params (int Coverage, (int Left, int Right, int Value)[] Pairs)[] subtables)
    {
        var table = new MemoryStream();
        WriteU16(table, 0);
        WriteU16(table, subtables.Length);
        foreach (var subtable in subtables)
        {
            WriteU16(table, 0);
            WriteU16(table, 14 + (subtable.Pairs.Length * 6));
            WriteU16(table, subtable.Coverage);
            WriteU16(table, subtable.Pairs.Length);
            WriteU16(table, 0);
            WriteU16(table, 0);
            WriteU16(table, 0);
            foreach ((int left, int right, int value) in subtable.Pairs)
            {
                WriteU16(table, left);
                WriteU16(table, right);
                WriteU16(table, value & 0xFFFF);
            }
        }

        return this.Table("kern", table.ToArray());
    }

    /// <summary>A GPOS table whose 'kern' feature uses the given lookups, each a type and subtables.</summary>
    public SfntBuilder Gpos(params (int LookupType, byte[][] SubTables)[] lookups) => this.GposWithFeature("kern", lookups);

    public SfntBuilder GposWithFeature(string featureTag, params (int LookupType, byte[][] SubTables)[] lookups)
    {
        // Feature list: one feature, using every lookup
        var featureList = new MemoryStream();
        WriteU16(featureList, 1);
        featureList.Write(Encoding.ASCII.GetBytes(featureTag));
        WriteU16(featureList, 8);
        WriteU16(featureList, 0);
        WriteU16(featureList, lookups.Length);
        for (int i = 0; i < lookups.Length; i++)
        {
            WriteU16(featureList, i);
        }

        // Lookup list, offsets from its start
        var lookupBodies = new List<byte[]>();
        foreach (var lookup in lookups)
        {
            var body = new MemoryStream();
            WriteU16(body, lookup.LookupType);
            WriteU16(body, 0);
            WriteU16(body, lookup.SubTables.Length);
            int offset = 6 + (lookup.SubTables.Length * 2);
            foreach (byte[] subTable in lookup.SubTables)
            {
                WriteU16(body, offset);
                offset += subTable.Length;
            }

            foreach (byte[] subTable in lookup.SubTables)
            {
                body.Write(subTable);
            }

            lookupBodies.Add(body.ToArray());
        }

        var lookupList = new MemoryStream();
        WriteU16(lookupList, lookups.Length);
        int lookupOffset = 2 + (lookups.Length * 2);
        foreach (byte[] body in lookupBodies)
        {
            WriteU16(lookupList, lookupOffset);
            lookupOffset += body.Length;
        }

        foreach (byte[] body in lookupBodies)
        {
            lookupList.Write(body);
        }

        var gpos = new MemoryStream();
        WriteU16(gpos, 1);
        WriteU16(gpos, 0);
        WriteU16(gpos, 0);
        WriteU16(gpos, 10);
        WriteU16(gpos, 10 + (int)featureList.Length);
        gpos.Write(featureList.ToArray());
        gpos.Write(lookupList.ToArray());
        return this.Table("GPOS", gpos.ToArray());
    }

    /// <summary>PairPos format 1: per first glyph, its second glyphs and x advances.</summary>
    public static byte[] PairPosFormat1((int First, (int Second, int XAdvance)[] Pairs)[] pairSets, int valueFormat1 = 4)
    {
        byte[] coverage = CoverageFormat1(pairSets.Select(set => set.First).ToArray());
        int recordValues = valueFormat1 == 5 ? 2 : 1;
        int headerSize = 10 + (pairSets.Length * 2);

        var sets = new MemoryStream();
        var setOffsets = new List<int>();
        foreach (var set in pairSets)
        {
            setOffsets.Add(headerSize + (int)sets.Length);
            WriteU16(sets, set.Pairs.Length);
            foreach ((int second, int xAdvance) in set.Pairs)
            {
                WriteU16(sets, second);
                if (recordValues == 2)
                {
                    WriteU16(sets, 7); // XPlacement, which isn't kerning
                }

                WriteU16(sets, xAdvance & 0xFFFF);
            }
        }

        var table = new MemoryStream();
        WriteU16(table, 1);
        WriteU16(table, headerSize + (int)sets.Length);
        WriteU16(table, valueFormat1);
        WriteU16(table, 0);
        WriteU16(table, pairSets.Length);
        foreach (int offset in setOffsets)
        {
            WriteU16(table, offset);
        }

        table.Write(sets.ToArray());
        table.Write(coverage);
        return table.ToArray();
    }

    /// <summary>PairPos format 2: class definitions, and a matrix of x advances [class1, class2].</summary>
    public static byte[] PairPosFormat2(int[] covered, (int Glyph, int Class)[] classes1, (int Glyph, int Class)[] classes2, int[,] matrix)
    {
        byte[] coverage = CoverageFormat2(covered);
        byte[] classDef1 = ClassDefFormat2(classes1);
        byte[] classDef2 = ClassDefFormat1(classes2);
        int class1Count = matrix.GetLength(0);
        int class2Count = matrix.GetLength(1);
        int headerSize = 16 + (class1Count * class2Count * 2);

        var table = new MemoryStream();
        WriteU16(table, 2);
        WriteU16(table, headerSize);
        WriteU16(table, 4);
        WriteU16(table, 0);
        WriteU16(table, headerSize + coverage.Length);
        WriteU16(table, headerSize + coverage.Length + classDef1.Length);
        WriteU16(table, class1Count);
        WriteU16(table, class2Count);
        for (int i = 0; i < class1Count; i++)
        {
            for (int j = 0; j < class2Count; j++)
            {
                WriteU16(table, matrix[i, j] & 0xFFFF);
            }
        }

        table.Write(coverage);
        table.Write(classDef1);
        table.Write(classDef2);
        return table.ToArray();
    }

    /// <summary>An Extension lookup's subtable, pointing at a real subtable of the given type.</summary>
    public static byte[] Extension(int extensionLookupType, byte[] subTable)
    {
        var table = new MemoryStream();
        WriteU16(table, 1);
        WriteU16(table, extensionLookupType);
        table.Write([0, 0, 0, 8]);
        table.Write(subTable);
        return table.ToArray();
    }

    private static byte[] CoverageFormat1(int[] glyphs)
    {
        var table = new MemoryStream();
        WriteU16(table, 1);
        WriteU16(table, glyphs.Length);
        foreach (int glyph in glyphs)
        {
            WriteU16(table, glyph);
        }

        return table.ToArray();
    }

    // One range per glyph; a contiguous range would do as well
    private static byte[] CoverageFormat2(int[] glyphs)
    {
        var table = new MemoryStream();
        WriteU16(table, 2);
        WriteU16(table, glyphs.Length);
        for (int i = 0; i < glyphs.Length; i++)
        {
            WriteU16(table, glyphs[i]);
            WriteU16(table, glyphs[i]);
            WriteU16(table, i);
        }

        return table.ToArray();
    }

    private static byte[] ClassDefFormat1((int Glyph, int Class)[] classes)
    {
        int start = classes.Min(entry => entry.Glyph);
        int end = classes.Max(entry => entry.Glyph);
        var values = new int[end - start + 1];
        foreach ((int glyph, int value) in classes)
        {
            values[glyph - start] = value;
        }

        var table = new MemoryStream();
        WriteU16(table, 1);
        WriteU16(table, start);
        WriteU16(table, values.Length);
        foreach (int value in values)
        {
            WriteU16(table, value);
        }

        return table.ToArray();
    }

    private static byte[] ClassDefFormat2((int Glyph, int Class)[] classes)
    {
        var table = new MemoryStream();
        WriteU16(table, 2);
        WriteU16(table, classes.Length);
        foreach ((int glyph, int value) in classes.OrderBy(entry => entry.Glyph))
        {
            WriteU16(table, glyph);
            WriteU16(table, glyph);
            WriteU16(table, value);
        }

        return table.ToArray();
    }

    private static void WriteU16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    public byte[] Build() => this.Build(ttcPrefix: false);

    /// <summary>A collection of this one font.</summary>
    public byte[] BuildCollection() => this.Build(ttcPrefix: true);

    private byte[] Build(bool ttcPrefix)
    {
        int prefix = ttcPrefix ? 16 : 0;
        int directorySize = 12 + (this._tables.Count * 16);
        var file = new MemoryStream();
        if (ttcPrefix)
        {
            file.Write("ttcf"u8);
            file.Write([0, 1, 0, 0, 0, 0, 0, 1]);
            file.Write([0, 0, 0, 16]);
        }

        file.Write([0, 1, 0, 0]);
        WriteU16(file, this._tables.Count);
        file.Write(new byte[6]);
        int offset = prefix + directorySize;
        Span<byte> number = stackalloc byte[4];
        foreach (var table in this._tables)
        {
            file.Write(Encoding.ASCII.GetBytes(table.Key));
            file.Write(new byte[4]);
            BinaryPrimitives.WriteUInt32BigEndian(number, (uint)offset);
            file.Write(number);
            BinaryPrimitives.WriteUInt32BigEndian(number, (uint)table.Value.Length);
            file.Write(number);
            offset += table.Value.Length;
        }

        foreach (var table in this._tables)
        {
            file.Write(table.Value);
        }

        return file.ToArray();
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;
using System.Text;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

/// <summary>
/// Reads a TrueType or OpenType font file (the first font of a collection): its metrics, names,
/// character map, advances and cap height. Everything the font build reads from a font's tables
/// is read here; glyphgen only renders glyph outlines to pixels.
/// </summary>
/// <remarks>
/// The metrics, names and character map follow FreeType's rules (<c>sfobjs.c</c> and
/// <c>ftobjs.c</c>), which the fonts were built with before, so built fonts don't change.
/// </remarks>
internal sealed class FontFile
{
    private readonly Dictionary<string, (int Offset, int Length)> _tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
    private int _cmapSubtable = -1;
    private int _cmapFormat;

    private FontFile(
        string path,
        byte[] data)
    {
        this.Path = path;
        this.Data = data;
    }

    public string Path { get; }

    /// <summary>
    /// Gets the whole file; table offsets are into this.
    /// </summary>
    public byte[] Data { get; }

    public int UnitsPerEm { get; private set; }

    /// <summary>
    /// Gets the ascender, descender (negative below the baseline) and line height, in font
    /// units, chosen as FreeType chooses them: the OS/2 typographic metrics when the font says
    /// to use them (fsSelection bit 7), else <c>hhea</c>'s, else OS/2's typographic and then
    /// Windows metrics when <c>hhea</c>'s are zero.
    /// </summary>
    public int Ascender { get; private set; }

    /// <inheritdoc cref="Ascender"/>
    public int Descender { get; private set; }

    /// <inheritdoc cref="Ascender"/>
    public int LineHeight { get; private set; }

    /// <summary>
    /// Gets the underline's center below the baseline (negative), in font units: the
    /// <c>post</c> table's top edge moved down by half the thickness, as FreeType reports it.
    /// </summary>
    public int UnderlinePosition { get; private set; }

    public int UnderlineThickness { get; private set; }

    /// <summary>
    /// Gets the family and style name ("Family Style"), in ASCII as FreeType gives it.
    /// </summary>
    public string Name { get; private set; } = "";

    /// <summary>
    /// Gets the scale from font units to ems.
    /// </summary>
    public double EmScale => 1.0 / this.UnitsPerEm;

    /// <summary>
    /// Loads a font file. WOFF and WOFF2 files aren't supported: build from the font's TTF or
    /// OTF instead.
    /// </summary>
    public static FontFile Load(
        string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if ((data.Length >= 4) && ((ReadTag(data, 0) == "wOFF") || (ReadTag(data, 0) == "wOF2")))
        {
            throw new NotSupportedException($"'{path}' is a WOFF web font, which the font build doesn't read; use the font's TTF or OTF file instead.");
        }

        var font = new FontFile(path, data);
        try
        {
            font.ReadTables();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException($"'{path}' is truncated or isn't a TrueType or OpenType font.", ex);
        }

        return font;
    }

    /// <summary>
    /// Gets a table's offset and length in <see cref="Data"/>, or false if the font has none.
    /// </summary>
    public bool TryGetTable(
        string tag,
        out int offset,
        out int length)
    {
        if (this._tables.TryGetValue(tag, out var table))
        {
            (offset, length) = table;
            return true;
        }

        offset = -1;
        length = 0;
        return false;
    }

    /// <summary>
    /// Gets the glyph for a code point from the font's Unicode character map, or zero if it has
    /// none, as FreeType's <c>FT_Get_Char_Index</c> does.
    /// </summary>
    public int GetGlyphIndex(
        int codePoint)
    {
        if (this._cmapSubtable < 0)
        {
            return 0;
        }

        try
        {
            return this._cmapFormat switch
            {
                0 => LookupFormat0(this._cmapSubtable, codePoint),
                4 => this.LookupFormat4(this._cmapSubtable, codePoint),
                6 => this.LookupFormat6(this._cmapSubtable, codePoint),
                12 or 13 => this.LookupFormat12(this._cmapSubtable, codePoint, this._cmapFormat == 13),
                _ => 0,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return 0;
        }

        int LookupFormat0(int subtable, int code)
        {
            return ((code >= 0) && (code < 256)) ? this.Data[subtable + 6 + code] : 0;
        }
    }

    /// <summary>
    /// Gets a glyph's advance width in font units, from <c>hmtx</c>.
    /// </summary>
    public int GetAdvanceWidth(
        int glyphIndex)
    {
        if (!this.TryGetTable("hhea", out int hhea, out _) || !this.TryGetTable("hmtx", out int hmtx, out _))
        {
            return 0;
        }

        int metricCount = U16(this.Data, hhea + 34);
        if (metricCount == 0)
        {
            return 0;
        }

        int index = Math.Min(glyphIndex, metricCount - 1);
        return U16(this.Data, hmtx + (4 * index));
    }

    /// <summary>
    /// Reads the height of the font's capital letters above the baseline, as a fraction of the
    /// em, or <c>null</c> if the font doesn't say: the OS/2 table's <c>sCapHeight</c> when it
    /// has one (version 2 and later), and otherwise the top of the "H" glyph's outline in a
    /// TrueType font.
    /// </summary>
    public double? ReadCapHeight()
    {
        try
        {
            if (this.TryGetTable("OS/2", out int os2, out int os2Length) && (os2Length >= 90))
            {
                int version = U16(this.Data, os2);
                int capHeight = S16(this.Data, os2 + 88);
                if ((version >= 2) && (capHeight > 0))
                {
                    return (double)capHeight / this.UnitsPerEm;
                }
            }

            int yMax = this.ReadGlyphTop(this.GetGlyphIndex('H'));
            return (yMax > 0) ? (double)yMax / this.UnitsPerEm : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            // A truncated or malformed table
            return null;
        }
    }

    private void ReadTables()
    {
        int offset = 0;
        if ((this.Data.Length >= 16) && (ReadTag(this.Data, 0) == "ttcf"))
        {
            // A collection: the first font, as FreeType opens face 0
            offset = (int)U32(this.Data, 12);
        }

        int count = U16(this.Data, offset + 4);
        for (int i = 0; i < count; i++)
        {
            int record = offset + 12 + (i * 16);
            this._tables[ReadTag(this.Data, record)] = ((int)U32(this.Data, record + 8), (int)U32(this.Data, record + 12));
        }

        if (!this.TryGetTable("head", out int head, out _) || !this.TryGetTable("hhea", out int hhea, out _))
        {
            throw new InvalidDataException($"'{this.Path}' has no 'head' or 'hhea' table; it isn't a scalable TrueType or OpenType font.");
        }

        this.UnitsPerEm = U16(this.Data, head + 18);
        this.ReadLineMetrics(hhea);
        this.ReadUnderline();
        this.Name = this.ReadName();
        this.SelectCharacterMap();
    }

    private void ReadLineMetrics(
        int hhea)
    {
        bool hasOs2 = this.TryGetTable("OS/2", out int os2, out int os2Length) && (os2Length >= 78);
        int fsSelection = hasOs2 ? U16(this.Data, os2 + 62) : 0;

        int ascender;
        int descender;
        int height;
        if (hasOs2 && ((fsSelection & 128) != 0))
        {
            ascender = S16(this.Data, os2 + 68);
            descender = S16(this.Data, os2 + 70);
            height = ascender - descender + S16(this.Data, os2 + 72);
        }
        else
        {
            ascender = S16(this.Data, hhea + 4);
            descender = S16(this.Data, hhea + 6);
            height = ascender - descender + S16(this.Data, hhea + 8);

            if ((ascender == 0) && (descender == 0) && hasOs2)
            {
                int typoAscender = S16(this.Data, os2 + 68);
                int typoDescender = S16(this.Data, os2 + 70);
                if ((typoAscender != 0) || (typoDescender != 0))
                {
                    ascender = typoAscender;
                    descender = typoDescender;
                    height = ascender - descender + S16(this.Data, os2 + 72);
                }
                else
                {
                    ascender = (short)U16(this.Data, os2 + 74);
                    descender = -(short)U16(this.Data, os2 + 76);
                    height = ascender - descender;
                }
            }
        }

        // FreeType keeps these as shorts
        this.Ascender = (short)ascender;
        this.Descender = (short)descender;
        this.LineHeight = (short)height;
    }

    private void ReadUnderline()
    {
        if (this.TryGetTable("post", out int post, out int postLength) && (postLength >= 12))
        {
            int position = S16(this.Data, post + 8);
            int thickness = S16(this.Data, post + 10);
            this.UnderlinePosition = (short)(position - (thickness / 2));
            this.UnderlineThickness = thickness;
        }
    }

    // The family and style, chosen from the name table as FreeType chooses them
    private string ReadName()
    {
        const int FontFamily = 1;
        const int FontSubfamily = 2;
        const int TypographicFamily = 16;
        const int TypographicSubfamily = 17;
        const int WwsFamily = 21;
        const int WwsSubfamily = 22;

        bool wwsOnly = this.TryGetTable("OS/2", out int os2, out int os2Length) && (os2Length >= 64)
            && ((U16(this.Data, os2 + 62) & 256) != 0);

        string? family;
        string? style;
        if (wwsOnly)
        {
            family = this.ReadNameRecord(TypographicFamily) ?? this.ReadNameRecord(FontFamily);
            style = this.ReadNameRecord(TypographicSubfamily) ?? this.ReadNameRecord(FontSubfamily);
        }
        else
        {
            family = this.ReadNameRecord(WwsFamily) ?? this.ReadNameRecord(TypographicFamily) ?? this.ReadNameRecord(FontFamily);
            style = this.ReadNameRecord(WwsSubfamily) ?? this.ReadNameRecord(TypographicSubfamily) ?? this.ReadNameRecord(FontSubfamily);
        }

        string name = family ?? "";
        if (!string.IsNullOrEmpty(style))
        {
            name += (name.Length == 0 ? "" : " ") + style;
        }

        return name;
    }

    // One name, preferring an English Windows record, then a Macintosh one, then a Unicode one,
    // as ASCII with '?' for anything else (FreeType's tt_face_get_name)
    private string? ReadNameRecord(
        int nameId)
    {
        if (!this.TryGetTable("name", out int name, out _))
        {
            return null;
        }

        int count = U16(this.Data, name + 2);
        int strings = name + U16(this.Data, name + 4);

        int foundAppleRoman = -1;
        int foundAppleEnglish = -1;
        int foundWin = -1;
        int foundUnicode = -1;
        bool isEnglish = false;
        for (int i = 0; i < count; i++)
        {
            int record = name + 6 + (i * 12);
            int platform = U16(this.Data, record);
            int encoding = U16(this.Data, record + 2);
            int language = U16(this.Data, record + 4);
            if ((U16(this.Data, record + 6) != nameId) || (U16(this.Data, record + 8) == 0))
            {
                continue;
            }

            switch (platform)
            {
                case 0: // Unicode
                case 2: // ISO
                    foundUnicode = i;
                    break;

                case 1: // Macintosh
                    if (language == 0)
                    {
                        foundAppleEnglish = i;
                    }
                    else if (encoding == 0)
                    {
                        foundAppleRoman = i;
                    }

                    break;

                case 3: // Microsoft
                    if ((foundWin == -1) || ((language & 0x3FF) == 0x009))
                    {
                        if ((encoding == 0) || (encoding == 1) || (encoding == 10))
                        {
                            isEnglish = (language & 0x3FF) == 0x009;
                            foundWin = i;
                        }
                    }

                    break;
            }
        }

        int foundApple = (foundAppleEnglish >= 0) ? foundAppleEnglish : foundAppleRoman;
        int chosen;
        bool utf16;
        if ((foundWin >= 0) && !((foundApple >= 0) && !isEnglish))
        {
            chosen = foundWin;
            utf16 = true;
        }
        else if (foundApple >= 0)
        {
            chosen = foundApple;
            utf16 = false;
        }
        else if (foundUnicode >= 0)
        {
            chosen = foundUnicode;
            utf16 = true;
        }
        else
        {
            return null;
        }

        int chosenRecord = name + 6 + (chosen * 12);
        int length = U16(this.Data, chosenRecord + 8);
        int start = strings + U16(this.Data, chosenRecord + 10);

        var text = new StringBuilder();
        int unitSize = utf16 ? 2 : 1;
        for (int i = 0; i + unitSize <= length; i += unitSize)
        {
            int code = utf16 ? U16(this.Data, start + i) : this.Data[start + i];
            if (code == 0)
            {
                break;
            }

            text.Append(((code < 32) || (code > 127)) ? '?' : (char)code);
        }

        return text.ToString();
    }

    // Picks the Unicode character map FreeType uses: searching the subtables from the last,
    // first a 32-bit one (Windows UCS-4, Unicode 32-bit, or Unicode full repertoire format 13),
    // then any Unicode one
    private void SelectCharacterMap()
    {
        if (!this.TryGetTable("cmap", out int cmap, out _))
        {
            return;
        }

        int count = U16(this.Data, cmap + 2);
        // NOTE: A Unicode map if the font has one, the widest first; a symbol font's only map is
        // platform 3, encoding 0, its characters at U+F0xx, which FreeType reads too
        for (int pass = 0; (pass < 3) && (this._cmapSubtable < 0); pass++)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                int record = cmap + 4 + (i * 8);
                int platform = U16(this.Data, record);
                int encoding = U16(this.Data, record + 2);
                int subtable = cmap + (int)U32(this.Data, record + 4);
                int format = U16(this.Data, subtable);
                if (!IsSupportedFormat(format))
                {
                    continue;
                }

                bool isUnicode = (platform == 0) || (platform == 2) || ((platform == 3) && ((encoding == 1) || (encoding == 10)));
                bool isSymbol = (platform == 3) && (encoding == 0);
                if ((pass == 2) ? !isSymbol : !isUnicode)
                {
                    continue;
                }

                bool is32Bit = ((platform == 3) && (encoding == 10))
                    || ((platform == 0) && (encoding == 4))
                    || ((platform == 0) && (encoding == 6) && (format == 13));
                if ((pass == 0) && !is32Bit)
                {
                    continue;
                }

                this._cmapSubtable = subtable;
                this._cmapFormat = format;
                break;
            }
        }

        static bool IsSupportedFormat(int format)
        {
            return format is 0 or 4 or 6 or 12 or 13;
        }
    }

    private int LookupFormat4(
        int subtable,
        int codePoint)
    {
        if (codePoint > 0xFFFF)
        {
            return 0;
        }

        int segmentsX2 = U16(this.Data, subtable + 6);
        int ends = subtable + 14;
        int starts = ends + segmentsX2 + 2;
        int deltas = starts + segmentsX2;
        int rangeOffsets = deltas + segmentsX2;
        for (int segment = 0; segment < segmentsX2 / 2; segment++)
        {
            int end = U16(this.Data, ends + (2 * segment));
            if (codePoint > end)
            {
                continue;
            }

            int start = U16(this.Data, starts + (2 * segment));
            if (codePoint < start)
            {
                return 0;
            }

            int delta = S16(this.Data, deltas + (2 * segment));
            int rangeOffset = U16(this.Data, rangeOffsets + (2 * segment));
            if (rangeOffset == 0)
            {
                return (codePoint + delta) & 0xFFFF;
            }

            int glyph = U16(this.Data, rangeOffsets + (2 * segment) + rangeOffset + (2 * (codePoint - start)));
            return (glyph == 0) ? 0 : ((glyph + delta) & 0xFFFF);
        }

        return 0;
    }

    private int LookupFormat6(
        int subtable,
        int codePoint)
    {
        int first = U16(this.Data, subtable + 6);
        int count = U16(this.Data, subtable + 8);
        int index = codePoint - first;
        return ((index >= 0) && (index < count)) ? U16(this.Data, subtable + 10 + (2 * index)) : 0;
    }

    private int LookupFormat12(
        int subtable,
        int codePoint,
        bool manyToOne)
    {
        int groups = (int)U32(this.Data, subtable + 12);
        int low = 0;
        int high = groups - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            int group = subtable + 16 + (12 * middle);
            long start = U32(this.Data, group);
            long end = U32(this.Data, group + 4);
            if (codePoint < start)
            {
                high = middle - 1;
            }
            else if (codePoint > end)
            {
                low = middle + 1;
            }
            else
            {
                long glyph = U32(this.Data, group + 8);
                return (int)(manyToOne ? glyph : glyph + (codePoint - start));
            }
        }

        return 0;
    }

    // The yMax of a TrueType glyph's outline, or zero if the font has no glyf table or no
    // such glyph
    private int ReadGlyphTop(
        int glyphIndex)
    {
        if ((glyphIndex <= 0)
            || !this.TryGetTable("glyf", out int glyf, out _)
            || !this.TryGetTable("loca", out int loca, out _)
            || !this.TryGetTable("head", out int head, out _))
        {
            return 0;
        }

        int indexToLocFormat = S16(this.Data, head + 50);
        int glyphOffset = (indexToLocFormat == 0)
            ? U16(this.Data, loca + (2 * glyphIndex)) * 2
            : (int)U32(this.Data, loca + (4 * glyphIndex));

        return S16(this.Data, glyf + glyphOffset + 8);
    }

    internal static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    internal static int S16(byte[] data, int offset) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));

    internal static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static string ReadTag(
        byte[] data,
        int offset)
    {
        return new string(new[] { (char)data[offset], (char)data[offset + 1], (char)data[offset + 2], (char)data[offset + 3] });
    }
}

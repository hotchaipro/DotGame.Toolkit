// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Buffers.Binary;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

// Reads horizontal pair kerning from an OpenType font: the GPOS 'kern' feature (PairPos
// formats 1 and 2, including Extension lookups), or the legacy 'kern' table when the font
// has no GPOS kerning.  Only XAdvance of the first glyph's value record is used.
internal static class OpenTypeKerning
{
    public static List<KerningPair> Read(FontFile fontFile, List<GlyphGenGlyph> glyphs, double scale)
    {
        var result = new List<KerningPair>();
        byte[] font = fontFile.Data;
        fontFile.TryGetTable("GPOS", out int gpos, out _);
        fontFile.TryGetTable("kern", out int kern, out _);

        var byGlyph = new Dictionary<int, List<int>>();
        foreach (var glyph in glyphs)
        {
            if (!byGlyph.TryGetValue(glyph.GlyphIndex, out var list))
            {
                byGlyph[glyph.GlyphIndex] = list = new List<int>();
            }
            list.Add(glyph.CodePoint);
        }
        var glyphIds = byGlyph.Keys.OrderBy(g => g).ToArray();

        var adjustments = new Dictionary<long, double>();
        bool hasGposKerning = gpos >= 0 && ReadGpos(font, gpos, glyphIds, adjustments);
        if (!hasGposKerning && kern >= 0)
        {
            ReadKern(font, kern, byGlyph, adjustments);
        }

        foreach (var entry in adjustments)
        {
            float adjust = (float)(entry.Value * scale);
            if (adjust == 0f)
            {
                continue;
            }
            int first = (int)(entry.Key >> 32);
            int second = (int)(entry.Key & 0xFFFFFFFF);
            foreach (var firstCode in byGlyph[first])
            {
                foreach (var secondCode in byGlyph[second])
                {
                    result.Add(new KerningPair { First = firstCode, Second = secondCode, Adjust = adjust });
                }
            }
        }
        return result;
    }

    private static bool ReadGpos(byte[] font, int gpos, int[] glyphIds, Dictionary<long, double> adjustments)
    {
        int scriptList = U16(font, gpos + 4);
        int featureList = gpos + U16(font, gpos + 6);
        int lookupList = gpos + U16(font, gpos + 8);

        // NOTE: A text run uses one script's lookups, which add up. Different scripts' lookups are
        // alternatives, often for the same pairs, so a pair takes its value from one script only.
        var scripts = new List<SortedSet<int>>();
        int featureCount = U16(font, featureList);
        int scriptCount = (scriptList == 0) ? 0 : U16(font, gpos + scriptList);
        for (int i = 0; i < scriptCount; i++)
        {
            int record = gpos + scriptList + 2 + (i * 6);
            int script = gpos + scriptList + U16(font, record + 4);
            int langSysOffset = U16(font, script);
            if (langSysOffset == 0)
            {
                continue;
            }

            int langSys = script + langSysOffset;
            var lookups = new SortedSet<int>();
            int count = U16(font, langSys + 4);
            for (int j = 0; j < count; j++)
            {
                int featureIndex = U16(font, langSys + 6 + (j * 2));
                if (featureIndex < featureCount)
                {
                    AddKernLookups(font, featureList, featureIndex, lookups);
                }
            }

            if (lookups.Count > 0)
            {
                // The usual scripts first: their value is the one a pair in several should have
                uint tag = U32(font, record);
                scripts.Insert((tag is 0x44464C54 or 0x6C61746E) ? 0 : scripts.Count, lookups);
            }
        }

        // A font with no script list that says otherwise: all its kern lookups together
        if (scripts.Count == 0)
        {
            var lookups = new SortedSet<int>();
            for (int i = 0; i < featureCount; i++)
            {
                AddKernLookups(font, featureList, i, lookups);
            }

            if (lookups.Count == 0)
            {
                return false;
            }

            scripts.Add(lookups);
        }

        var done = new HashSet<string>();
        foreach (var lookups in scripts)
        {
            if (!done.Add(string.Join(',', lookups)))
            {
                continue;
            }

            var scriptValues = new Dictionary<long, double>();
            ReadLookups(font, lookupList, lookups, glyphIds, scriptValues);
            foreach (var entry in scriptValues)
            {
                adjustments.TryAdd(entry.Key, entry.Value);
            }
        }

        return true;
    }

    private static void AddKernLookups(byte[] font, int featureList, int featureIndex, SortedSet<int> lookups)
    {
        int record = featureList + 2 + (featureIndex * 6);
        if (U32(font, record) != 0x6B65726E) // 'kern'
        {
            return;
        }

        int feature = featureList + U16(font, record + 4);
        int count = U16(font, feature + 2);
        for (int j = 0; j < count; j++)
        {
            lookups.Add(U16(font, feature + 4 + (j * 2)));
        }
    }

    private static void ReadLookups(byte[] font, int lookupList, SortedSet<int> lookupIndices, int[] glyphIds, Dictionary<long, double> adjustments)
    {
        int lookupCount = U16(font, lookupList);
        foreach (int lookupIndex in lookupIndices)
        {
            if (lookupIndex >= lookupCount)
            {
                continue;
            }
            int lookup = lookupList + U16(font, lookupList + 2 + (lookupIndex * 2));
            int lookupType = U16(font, lookup);
            int subTableCount = U16(font, lookup + 4);
            var subTables = new List<int>();
            for (int s = 0; s < subTableCount; s++)
            {
                int subTable = lookup + U16(font, lookup + 6 + (s * 2));
                int type = lookupType;
                if (type == 9)
                {
                    type = U16(font, subTable + 2);
                    subTable += (int)U32(font, subTable + 4);
                }
                if (type == 2)
                {
                    subTables.Add(subTable);
                }
            }
            if (subTables.Count == 0)
            {
                continue;
            }

            // Within a lookup, the first subtable that applies to a pair wins.
            var lookupValues = new Dictionary<long, double>();
            var handled = new HashSet<long>();
            foreach (int subTable in subTables)
            {
                ReadPairPos(font, subTable, glyphIds, lookupValues, handled);
            }
            foreach (var entry in lookupValues)
            {
                double existing;
                adjustments.TryGetValue(entry.Key, out existing);
                adjustments[entry.Key] = existing + entry.Value;
            }
        }
    }

    private static void ReadPairPos(byte[] font, int subTable, int[] glyphIds, Dictionary<long, double> values, HashSet<long> handled)
    {
        int format = U16(font, subTable);
        int coverage = subTable + U16(font, subTable + 2);
        int valueFormat1 = U16(font, subTable + 4);
        int valueFormat2 = U16(font, subTable + 6);
        int size1 = ValueRecordSize(valueFormat1);
        int size2 = ValueRecordSize(valueFormat2);
        int xAdvanceOffset = XAdvanceOffset(valueFormat1);
        var glyphSet = new HashSet<int>(glyphIds);

        foreach (int first in glyphIds)
        {
            int coverageIndex = CoverageIndex(font, coverage, first);
            if (coverageIndex < 0)
            {
                continue;
            }

            if (format == 1)
            {
                int pairSetCount = U16(font, subTable + 8);
                if (coverageIndex >= pairSetCount)
                {
                    continue;
                }
                int pairSet = subTable + U16(font, subTable + 10 + (coverageIndex * 2));
                int pairCount = U16(font, pairSet);
                int recordSize = 2 + size1 + size2;
                for (int p = 0; p < pairCount; p++)
                {
                    int record = pairSet + 2 + (p * recordSize);
                    int second = U16(font, record);
                    if (!glyphSet.Contains(second))
                    {
                        continue;
                    }
                    long key = ((long)first << 32) | (uint)second;
                    if (!handled.Add(key))
                    {
                        continue;
                    }
                    if (xAdvanceOffset >= 0)
                    {
                        values[key] = S16(font, record + 2 + xAdvanceOffset);
                    }
                }
            }
            else if (format == 2)
            {
                int classDef1 = subTable + U16(font, subTable + 8);
                int classDef2 = subTable + U16(font, subTable + 10);
                int class1Count = U16(font, subTable + 12);
                int class2Count = U16(font, subTable + 14);
                int class1 = ClassOf(font, classDef1, first);
                if (class1 >= class1Count)
                {
                    continue;
                }
                foreach (int second in glyphIds)
                {
                    long key = ((long)first << 32) | (uint)second;
                    if (!handled.Add(key))
                    {
                        continue;
                    }
                    int class2 = ClassOf(font, classDef2, second);
                    if (class2 >= class2Count || xAdvanceOffset < 0)
                    {
                        continue;
                    }
                    int record = subTable + 16 + (((class1 * class2Count) + class2) * (size1 + size2));
                    int value = S16(font, record + xAdvanceOffset);
                    if (value != 0)
                    {
                        values[key] = value;
                    }
                }
            }
        }
    }

    private static void ReadKern(byte[] font, int kern, Dictionary<int, List<int>> byGlyph, Dictionary<long, double> adjustments)
    {
        if (U16(font, kern) != 0)
        {
            return; // Apple 'kern' (version 1.0) is not supported.
        }
        int tableCount = U16(font, kern + 2);
        int offset = kern + 4;
        for (int t = 0; t < tableCount; t++)
        {
            int length = U16(font, offset + 2);
            int coverage = U16(font, offset + 4);
            int format = coverage >> 8;
            bool horizontal = (coverage & 1) != 0;
            bool crossStream = (coverage & 4) != 0;
            bool replace = (coverage & 8) != 0;
            if (format == 0 && horizontal && !crossStream)
            {
                int pairCount = U16(font, offset + 6);
                for (int p = 0; p < pairCount; p++)
                {
                    int record = offset + 14 + (p * 6);
                    int left = U16(font, record);
                    int right = U16(font, record + 2);
                    if (!byGlyph.ContainsKey(left) || !byGlyph.ContainsKey(right))
                    {
                        continue;
                    }
                    long key = ((long)left << 32) | (uint)right;
                    double existing;
                    adjustments.TryGetValue(key, out existing);
                    adjustments[key] = (replace ? 0 : existing) + S16(font, record + 4);
                }
            }
            offset += length;
        }
    }

    private static int CoverageIndex(byte[] font, int coverage, int glyph)
    {
        int format = U16(font, coverage);
        if (format == 1)
        {
            int count = U16(font, coverage + 2);
            int lo = 0, hi = count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int value = U16(font, coverage + 4 + (mid * 2));
                if (value == glyph)
                {
                    return mid;
                }

                if (value < glyph)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
        }
        else if (format == 2)
        {
            int count = U16(font, coverage + 2);
            for (int i = 0; i < count; i++)
            {
                int record = coverage + 4 + (i * 6);
                int start = U16(font, record);
                int end = U16(font, record + 2);
                if (glyph >= start && glyph <= end)
                {
                    return U16(font, record + 4) + glyph - start;
                }
            }
        }
        return -1;
    }

    private static int ClassOf(byte[] font, int classDef, int glyph)
    {
        int format = U16(font, classDef);
        if (format == 1)
        {
            int start = U16(font, classDef + 2);
            int count = U16(font, classDef + 4);
            if (glyph >= start && glyph < start + count)
            {
                return U16(font, classDef + 6 + ((glyph - start) * 2));
            }
        }
        else if (format == 2)
        {
            int count = U16(font, classDef + 2);
            for (int i = 0; i < count; i++)
            {
                int record = classDef + 4 + (i * 6);
                if (glyph >= U16(font, record) && glyph <= U16(font, record + 2))
                {
                    return U16(font, record + 4);
                }
            }
        }
        return 0;
    }

    private static int ValueRecordSize(int valueFormat)
    {
        int size = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            if ((valueFormat & (1 << bit)) != 0)
            {
                size += 2;
            }
        }
        return size;
    }

    private static int XAdvanceOffset(int valueFormat)
    {
        if ((valueFormat & 0x0004) == 0)
        {
            return -1;
        }

        int offset = 0;
        if ((valueFormat & 0x0001) != 0)
        {
            offset += 2;
        }

        if ((valueFormat & 0x0002) != 0)
        {
            offset += 2;
        }

        return offset;
    }

    private static int U16(byte[] data, int offset) { return (data[offset] << 8) | data[offset + 1]; }
    private static int S16(byte[] data, int offset) { return (short)((data[offset] << 8) | data[offset + 1]); }
    private static uint U32(byte[] data, int offset) { return BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(data, offset, 4)); }
}

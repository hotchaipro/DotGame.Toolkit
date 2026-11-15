// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

// Builds a SpriteFontContent item: reads the primary font and each fallback font (FontFile:
// metrics, names, character map, advances, kerning, cap height), renders their glyphs with
// glyphgen (outlines to pixels only), packs the glyph bitmaps into one or more atlas pages and
// writes the .spritefont descriptor (PBON).

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;
using DotGame.Toolkit.ContentPipeline.BuildTool.Serialization;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

internal static class SpriteFontBuilder
{
    // Pixels left empty between glyphs so bilinear sampling at a glyph quad's
    // edge never reads a neighbouring glyph.
    private const int GlyphGutter = 1;

    public static List<int> ResolveCharset(string literal, string[] files)
    {
        var codePoints = new SortedSet<int>();
        AddCharacters(codePoints, literal ?? "");
        foreach (var file in files ?? Array.Empty<string>())
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("Charset file not found: '" + file + "'", file);
            }
            AddCharacters(codePoints, File.ReadAllText(file, Encoding.UTF8));
        }
        if (codePoints.Count == 0 && string.IsNullOrEmpty(literal) && (files == null || files.Length == 0))
        {
            for (int c = 0x20; c <= 0x7E; c++)
            {
                codePoints.Add(c);
            }
        }
        return codePoints.ToList();
    }

    private static void AddCharacters(SortedSet<int> codePoints, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int codePoint;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                continue;
            }
            else
            {
                codePoint = text[i];
            }
            // Control characters (line breaks, tabs) and the BOM are not glyphs.
            if (codePoint < 0x20 || (codePoint >= 0x7F && codePoint < 0xA0) || codePoint == 0xFEFF)
            {
                continue;
            }
            codePoints.Add(codePoint);
        }
    }

    public static SpriteFontBuildResult Build(SpriteFontBuildOptions options, Action<string> log)
    {
        var result = new SpriteFontBuildResult();
        var codePoints = ResolveCharset(options.Charset, options.CharsetFiles);
        var remaining = new List<int>(codePoints);
        var sources = new List<string> { options.PrimaryFont };
        sources.AddRange(options.FallbackFonts);

        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            if (remaining.Count == 0 && sourceIndex > 0)
            {
                break;
            }

            var fontPath = sources[sourceIndex];
            var font = FontFile.Load(fontPath);
            if (sourceIndex == 0)
            {
                result.Primary = font;
                result.CapHeight = font.ReadCapHeight();
            }

            // The font's character map decides which of the remaining characters it has, and
            // glyphgen renders their glyphs (once each, though characters may share one)
            var present = new List<(int CodePoint, int GlyphIndex)>();
            foreach (int codePoint in remaining)
            {
                int glyphIndex = font.GetGlyphIndex(codePoint);
                if (glyphIndex != 0)
                {
                    present.Add((codePoint, glyphIndex));
                }
            }

            var generated = RunGlyphGen(options, fontPath, present.Select(p => p.GlyphIndex).Distinct().ToList(), sourceIndex);
            var rendered = generated.Glyphs.ToDictionary(g => g.GlyphIndex);

            result.SourceFamilies.Add(font.Name);
            result.SourceFiles.Add(Path.GetFileName(fontPath));

            var glyphs = new List<GlyphGenGlyph>(present.Count);
            foreach (var (codePoint, glyphIndex) in present)
            {
                if (!rendered.TryGetValue(glyphIndex, out var image))
                {
                    throw new InvalidDataException($"glyphgen didn't render glyph {glyphIndex} (U+{codePoint:X4}) of '{fontPath}'.");
                }

                var glyph = new GlyphGenGlyph
                {
                    CodePoint = codePoint,
                    GlyphIndex = glyphIndex,
                    // A bitmap glyph's advance is its hinted advance, which only rendering
                    // gives; a distance field glyph's is the font's
                    Advance = (options.Format == GlyphFormat.Bitmap) ? image.Advance : font.EmScale * font.GetAdvanceWidth(glyphIndex),
                    Left = image.Left,
                    Bottom = image.Bottom,
                    Right = image.Right,
                    Top = image.Top,
                    Width = image.Width,
                    Height = image.Height,
                    Channels = image.Channels,
                    Pixels = image.Pixels,
                    Source = sourceIndex,
                };
                glyphs.Add(glyph);
                result.Glyphs.Add(glyph);
            }

            var found = new HashSet<int>(present.Select(p => p.CodePoint));
            remaining = remaining.Where(c => !found.Contains(c)).ToList();

            if (log != null && glyphs.Count > 0)
            {
                log(string.Format(CultureInfo.InvariantCulture, "  {0}: {1} glyph(s)", Path.GetFileName(fontPath), glyphs.Count));
            }

            // NOTE: Class kerning looks at every pair of glyphs, too many to finish in a CJK charset
            const int MaxKernedGlyphs = 4096;
            if (options.Kerning && (glyphs.Count > MaxKernedGlyphs))
            {
                log?.Invoke(string.Format(CultureInfo.InvariantCulture, "  {0}: no kerning for {1} glyphs (the limit is {2})", Path.GetFileName(fontPath), glyphs.Count, MaxKernedGlyphs));
            }
            else if (options.Kerning && glyphs.Count > 1)
            {
                var scale = options.Size / font.UnitsPerEm;
                foreach (var pair in OpenTypeKerning.Read(font, glyphs, scale))
                {
                    result.Kerning.Add(pair);
                }
            }
        }

        SynthesizeMissingSpaces(result, remaining);

        result.MissingCodePoints = remaining;
        result.Glyphs.Sort((a, b) => a.CodePoint.CompareTo(b.CodePoint));
        result.Kerning.Sort((a, b) => a.First != b.First ? a.First.CompareTo(b.First) : a.Second.CompareTo(b.Second));
        Pack(options, result);
        return result;
    }

    // A Unicode space that no font has (many fonts lack the narrow no-break space U+202F, which
    // French puts before "!" and "?") is added as a blank glyph of that space's standard width,
    // rather than drawn as the default character. Widths are in ems, as typesetting defines them;
    // the no-break, figure and punctuation spaces take the font's own space width.
    private static void SynthesizeMissingSpaces(SpriteFontBuildResult result, List<int> remaining)
    {
        var space = result.Glyphs.Find(g => g.CodePoint == 0x20);
        var primary = result.Primary;
        if (space is null || primary is null || space.Source != 0)
        {
            return;
        }

        double spaceEms = primary.EmScale * primary.GetAdvanceWidth(primary.GetGlyphIndex(0x20));
        if (spaceEms <= 0)
        {
            return;
        }

        // The advance's units per em: ems for distance fields, the hinted size for bitmaps
        double advancePerEm = space.Advance / spaceEms;

        remaining.RemoveAll(codePoint =>
        {
            double? ems = codePoint switch
            {
                0x00A0 or 0x2007 or 0x2008 => spaceEms, // No-break, figure, punctuation
                0x2000 or 0x2002 => 1.0 / 2, // En quad, en space
                0x2001 or 0x2003 or 0x3000 => 1.0, // Em quad, em space, ideographic space
                0x2004 => 1.0 / 3, // Three-per-em space
                0x2005 => 1.0 / 4, // Four-per-em space
                0x2006 => 1.0 / 6, // Six-per-em space
                0x2009 or 0x202F => 1.0 / 5, // Thin space, narrow no-break space
                0x200A => 1.0 / 10, // Hair space
                0x205F => 4.0 / 18, // Medium mathematical space
                _ => null,
            };
            if (ems is null)
            {
                return false;
            }

            result.Glyphs.Add(new GlyphGenGlyph
            {
                CodePoint = codePoint,
                GlyphIndex = space.GlyphIndex,
                Advance = ems.Value * advancePerEm,
                Left = space.Left,
                Bottom = space.Bottom,
                Right = space.Right,
                Top = space.Top,
                Width = space.Width,
                Height = space.Height,
                Channels = space.Channels,
                Pixels = space.Pixels,
                Source = space.Source,
            });
            return true;
        });
    }

    // Renders the glyphs with glyphgen, which only turns outlines into pixels
    private static GlyphGenResult RunGlyphGen(SpriteFontBuildOptions options, string fontPath, List<int> glyphIndices, int sourceIndex)
    {
        Directory.CreateDirectory(options.IntermediateDir);
        var stem = Path.Combine(options.IntermediateDir, options.Name + ".source" + sourceIndex.ToString(CultureInfo.InvariantCulture));
        var glyphIndexFile = stem + ".glyphindices.txt";
        var outputFile = stem + ".glyphs.bin";
        File.WriteAllText(glyphIndexFile, string.Join("\n", glyphIndices.Select(g => g.ToString(CultureInfo.InvariantCulture))));

        var half = options.PxRange / 2f;
        string[] arguments =
        [
            "--font", fontPath,
            "--glyphs", glyphIndexFile,
            "--output", outputFile,
            "--format", options.Format.ToString().ToLowerInvariant(),
            "--size", options.Size.ToString("R", CultureInfo.InvariantCulture),
            "--range-lower", (-(half + options.PxRangeOffset)).ToString("R", CultureInfo.InvariantCulture),
            "--range-upper", (half - options.PxRangeOffset).ToString("R", CultureInfo.InvariantCulture),
        ];
        ExternalProcess.RunAndCapture(options.GlyphGenExe, arguments, $"glyphgen ('{fontPath}')", captureStandardError: true);

        return ReadGlyphGenOutput(File.ReadAllBytes(outputFile));
    }

    public static GlyphGenResult ReadGlyphGenOutput(byte[] data)
    {
        int offset = 0;
        if (data.Length < 8 || Encoding.ASCII.GetString(data, 0, 4) != "GLGN")
        {
            throw new InvalidDataException("Invalid glyphgen output.");
        }
        offset = 4;
        uint version = ReadU32(data, ref offset);
        if (version != 2)
        {
            throw new InvalidDataException("Unsupported glyphgen output version " + version + "; update glyphgen.");
        }

        var result = new GlyphGenResult();
        int glyphCount = (int)ReadU32(data, ref offset);
        for (int i = 0; i < glyphCount; i++)
        {
            var glyph = new GlyphGenGlyph
            {
                GlyphIndex = (int)ReadU32(data, ref offset),
                Advance = ReadF64(data, ref offset),
                Left = ReadF64(data, ref offset),
                Bottom = ReadF64(data, ref offset),
                Right = ReadF64(data, ref offset),
                Top = ReadF64(data, ref offset),
                Width = (int)ReadU32(data, ref offset),
                Height = (int)ReadU32(data, ref offset),
                Channels = (int)ReadU32(data, ref offset),
            };
            int size = glyph.Width * glyph.Height * glyph.Channels;
            glyph.Pixels = new byte[size];
            Buffer.BlockCopy(data, offset, glyph.Pixels, 0, size);
            offset += size;
            result.Glyphs.Add(glyph);
        }
        return result;
    }

    private static uint ReadU32(byte[] data, ref int offset)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(data, offset, 4));
        offset += 4;
        return value;
    }

    private static double ReadF64(byte[] data, ref int offset)
    {
        double value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(data, offset, 8)));
        offset += 8;
        return value;
    }

    // Packs every non-empty glyph into one or more pages (MaxRects BSSF) and composites the pages.
    private static void Pack(SpriteFontBuildOptions options, SpriteFontBuildResult result)
    {
        var items = result.Glyphs
            .Where(g => g.Width > 0 && g.Height > 0)
            .OrderByDescending(g => Math.Max(g.Width, g.Height))
            .ThenByDescending(g => g.Width * g.Height)
            .ThenBy(g => g.CodePoint)
            .ToList();

        foreach (var glyph in items)
        {
            if (glyph.Width + (2 * GlyphGutter) > options.MaxPageWidth || glyph.Height + (2 * GlyphGutter) > options.MaxPageHeight)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Glyph U+{0:X4} ({1}x{2}) does not fit in a {3}x{4} atlas page; reduce Size or raise SpriteFontAtlasMaxWidth/SpriteFontAtlasMaxHeight.",
                    glyph.CodePoint, glyph.Width, glyph.Height, options.MaxPageWidth, options.MaxPageHeight));
            }
        }

        int pageIndex = 0;
        int pagesWidth = 1, pagesHeight = 1;
        var pagePlacements = new List<List<GlyphGenGlyph>>();
        while (items.Count > 0)
        {
            long area = 0;
            foreach (var glyph in items)
            {
                area += (long)(glyph.Width + GlyphGutter) * (glyph.Height + GlyphGutter);
            }
            int side = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(area)));
            int w = side, h = side;
            List<GlyphGenGlyph> placed;
            List<GlyphGenGlyph> unplaced;
            int pageW, pageH;

            while (true)
            {
                int tryW = options.PowerOfTwo ? NextPowerOfTwo(w) : w;
                int tryH = options.PowerOfTwo ? NextPowerOfTwo(h) : h;
                bool atMax = false;
                if (tryW >= options.MaxPageWidth) { tryW = options.MaxPageWidth; atMax = true; }
                if (tryH >= options.MaxPageHeight) { tryH = options.MaxPageHeight; atMax = true; }

                TryPack(items, tryW, tryH, out placed, out unplaced);
                if (unplaced.Count == 0 || (atMax && tryW == options.MaxPageWidth && tryH == options.MaxPageHeight))
                {
                    pageW = tryW;
                    pageH = tryH;
                    break;
                }
                if (w <= h) { w *= 2; } else { h *= 2; }
            }

            if (placed.Count == 0)
            {
                throw new InvalidOperationException("Unable to place any glyph on a font atlas page.");
            }

            // Tighten to the used extent (plus the trailing gutter).
            int usedW = 1, usedH = 1;
            foreach (var glyph in placed)
            {
                glyph.Page = pageIndex;
                usedW = Math.Max(usedW, glyph.X + glyph.Width + GlyphGutter);
                usedH = Math.Max(usedH, glyph.Y + glyph.Height + GlyphGutter);
            }
            if (options.PowerOfTwo)
            {
                usedW = NextPowerOfTwo(usedW);
                usedH = NextPowerOfTwo(usedH);
            }
            usedW = Math.Min(usedW, pageW);
            usedH = Math.Min(usedH, pageH);

            pagePlacements.Add(placed);
            pagesWidth = Math.Max(pagesWidth, usedW);
            pagesHeight = Math.Max(pagesHeight, usedH);
            items = unplaced;
            pageIndex++;
        }

        // Every page has the same size so the runtime (and the shader's TextureSize
        // uniform) can treat the pages uniformly.
        foreach (var placed in pagePlacements)
        {
            result.Pages.Add(Composite(options.Format, placed, pagesWidth, pagesHeight));
        }

        if (result.Pages.Count == 0)
        {
            // A font with only whitespace still needs a page for the format.
            result.Pages.Add(Composite(options.Format, new List<GlyphGenGlyph>(), 1, 1));
        }
    }

    private static void TryPack(List<GlyphGenGlyph> items, int width, int height, out List<GlyphGenGlyph> placed, out List<GlyphGenGlyph> unplaced)
    {
        placed = new List<GlyphGenGlyph>();
        unplaced = new List<GlyphGenGlyph>();
        // The packer area starts at the gutter so the first row/column also has a border.
        var packer = new MaxRectsPacker(width - GlyphGutter, height - GlyphGutter);
        foreach (var glyph in items)
        {
            int x, y;
            if (packer.TryInsert(glyph.Width + GlyphGutter, glyph.Height + GlyphGutter, out x, out y))
            {
                glyph.X = x + GlyphGutter;
                glyph.Y = y + GlyphGutter;
                placed.Add(glyph);
            }
            else
            {
                unplaced.Add(glyph);
            }
        }
    }

    private static FontPage Composite(GlyphFormat format, List<GlyphGenGlyph> glyphs, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        if (format != GlyphFormat.Bitmap)
        {
            // Empty texels are "far outside" (distance 0, opaque alpha for msdf/sdf).
            for (int i = 3; i < rgba.Length; i += 4)
            {
                rgba[i] = format == GlyphFormat.Mtsdf ? (byte)0 : (byte)255;
            }
        }
        else
        {
            // Bitmap glyphs are white with coverage alpha (premultiplied by the encoder).
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = rgba[i + 1] = rgba[i + 2] = 255;
            }
        }

        foreach (var glyph in glyphs)
        {
            int channels = glyph.Channels;
            for (int y = 0; y < glyph.Height; y++)
            {
                int src = y * glyph.Width * channels;
                int dst = (((glyph.Y + y) * width) + glyph.X) * 4;
                for (int x = 0; x < glyph.Width; x++, src += channels, dst += 4)
                {
                    switch (format)
                    {
                        case GlyphFormat.Bitmap:
                            rgba[dst + 3] = glyph.Pixels[src];
                            break;
                        case GlyphFormat.Sdf:
                            rgba[dst] = rgba[dst + 1] = rgba[dst + 2] = glyph.Pixels[src];
                            break;
                        case GlyphFormat.Msdf:
                            rgba[dst] = glyph.Pixels[src];
                            rgba[dst + 1] = glyph.Pixels[src + 1];
                            rgba[dst + 2] = glyph.Pixels[src + 2];
                            break;
                        case GlyphFormat.Mtsdf:
                            rgba[dst] = glyph.Pixels[src];
                            rgba[dst + 1] = glyph.Pixels[src + 1];
                            rgba[dst + 2] = glyph.Pixels[src + 2];
                            rgba[dst + 3] = glyph.Pixels[src + 3];
                            break;
                    }
                }
            }
        }

        return new FontPage { Width = width, Height = height, Rgba = rgba };
    }

    private static int NextPowerOfTwo(int value) => MaxRectsPacker.NextPowerOfTwo(value);

    // Writes the .spritefont descriptor.  Member keys are assigned once and never reused;
    // metadata members come first and the large binary blobs last so readers can stop early.
    public static void WriteSpriteFont(string path, SpriteFontBuildOptions options, SpriteFontBuildResult result, IList<string> pageNames)
    {
        float size = options.Size;
        var primary = result.Primary ?? throw new InvalidOperationException("The sprite font has no primary font.");
        double emScale = primary.EmScale;
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            var writer = new PbonWriter(stream);

            writer.StartObject();

            writer.Member(1).Write(1);
            writer.Member(2).Write(options.Format.ToString().ToLowerInvariant());
            writer.Member(3).Write(size);
            if (options.Format != GlyphFormat.Bitmap)
            {
                writer.Member(4).Write(options.PxRange);
                writer.Member(5).Write(-options.PxRangeOffset);
            }

            writer.Member(6);
            writer.StartObject();
            writer.Member(1).Write((float)(emScale * primary.LineHeight * size));
            writer.Member(2).Write((float)(emScale * primary.Ascender * size));
            writer.Member(3).Write((float)(emScale * primary.Descender * size));
            writer.Member(4).Write((float)(-(emScale * primary.UnderlinePosition) * size));
            writer.Member(5).Write((float)(emScale * primary.UnderlineThickness * size));
            if (result.CapHeight is double capHeight)
            {
                writer.Member(6).Write((float)(capHeight * size));
            }
            writer.EndObject();

            writer.Member(7);
            writer.StartArray();
            for (int i = 0; i < result.Pages.Count; i++)
            {
                writer.StartObject();
                writer.Member(1).Write(pageNames[i]);
                writer.Member(2).Write(result.Pages[i].Width);
                writer.Member(3).Write(result.Pages[i].Height);
                writer.EndObject();
            }
            writer.EndArray();

            writer.Member(8);
            writer.StartArray();
            for (int i = 0; i < result.SourceFamilies.Count; i++)
            {
                writer.StartObject();
                writer.Member(1).Write(result.SourceFamilies[i]);
                writer.Member(2).Write(result.SourceFiles[i]);
                writer.EndObject();
            }
            writer.EndArray();

            writer.Member(9).Write(result.Glyphs.Count);
            writer.Member(10).Write(result.Kerning.Count);

            // Glyph records (little-endian, 34 bytes):
            // int codePoint; ushort page, x, y, width, height; float advance, left, top, right, bottom
            // Bounds are pixels at emSize relative to the pen position on the baseline, y down.
            var glyphBlob = new byte[result.Glyphs.Count * GlyphRecordSize];
            var span = new Span<byte>(glyphBlob);
            int offset = 0;
            foreach (var glyph in result.Glyphs)
            {
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), glyph.CodePoint);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 4), (ushort)glyph.Page);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 6), (ushort)glyph.X);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 8), (ushort)glyph.Y);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 10), (ushort)glyph.Width);
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset + 12), (ushort)glyph.Height);
                WriteSingle(span.Slice(offset + 14), (float)(glyph.Advance * size));
                WriteSingle(span.Slice(offset + 18), (float)(glyph.Left * size));
                WriteSingle(span.Slice(offset + 22), (float)(-glyph.Top * size));
                WriteSingle(span.Slice(offset + 26), (float)(glyph.Right * size));
                WriteSingle(span.Slice(offset + 30), (float)(-glyph.Bottom * size));
                offset += GlyphRecordSize;
            }
            writer.Member(11).Write(glyphBlob);

            if (options.Kerning)
            {
                // Kerning records (little-endian, 12 bytes): int first; int second; float adjust
                var kerningBlob = new byte[result.Kerning.Count * 12];
                var kerningSpan = new Span<byte>(kerningBlob);
                offset = 0;
                foreach (var pair in result.Kerning)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(kerningSpan.Slice(offset), pair.First);
                    BinaryPrimitives.WriteInt32LittleEndian(kerningSpan.Slice(offset + 4), pair.Second);
                    WriteSingle(kerningSpan.Slice(offset + 8), pair.Adjust);
                    offset += 12;
                }
                writer.Member(12).Write(kerningBlob);
            }

            writer.EndObject();
        }
    }

    private const int GlyphRecordSize = 34;

    private static void WriteSingle(Span<byte> destination, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;

/// <summary>
/// Builds one TextureAtlasContent atlas: packs the source images into one or
/// more pages, encodes each page with the framework's texture content processor
/// and writes a TexturePacker-aligned JSON descriptor.
/// </summary>
/// <remarks>
/// <para>
/// Each sprite is trimmed to its non-transparent pixels, packed with MaxRects
/// BSSF, and extruded: its edge pixels are repeated outward by
/// <c>--extrude</c> pixels so bilinear filtering at draw time samples the
/// sprite's own edge color rather than transparency.  <c>--padding</c> adds
/// transparent space between the extruded footprints (TexturePacker's
/// shapePadding), so adjacent frames are padding + 2 * extrude pixels apart.
/// Sprites whose trimmed pixels are identical share one packed region.
/// </para>
/// <para>
/// Descriptor schema (region names sorted within each page):
/// <code>
/// { "textures": [ { "name": "&lt;page file name with extension&gt;",
///                   "regions": { "&lt;sprite&gt;": { "frame": {x,y,w,h}, "source": {x,y,w,h} } } } ] }
/// </code>
/// frame is the trimmed pixels in the atlas page.  source is the untrimmed
/// (logical) sprite bounds in the same atlas-page coordinates, and is omitted
/// when the sprite was not trimmed.  The runtime reader also accepts optional
/// center (9-slice interior) and layout (layout bounds) rectangles in the same
/// coordinate space, which this packer does not emit, and TexturePacker's
/// JSON Hash form.
/// </para>
/// </remarks>
internal static class TextureAtlasCommand
{
    public const string Name = "atlas";
    public const string ErrorCode = "TXA0001";

    public static void Run(CommandLineOptions options)
    {
        string inputList = options.GetPath("input-list");
        string outputFile = options.GetPath("output");
        string textureOutputBase = options.GetPath("texture-output-base");
        string textureOutputExtension = options.GetRequired("texture-output-extension");
        string encodeTemplate = ContentPaths.ReadCommandTemplate(options.GetPath("encode-command-file"));
        string intermediateDir = options.GetPath("intermediate-dir");
        string inputDisplayDir = options.GetString("input-display-dir");
        string outputDisplayFile = options.GetString("output-display-file");
        string atlasLabel = options.GetString("atlas-display-name");
        int padding = options.GetInt32("padding", 0, minimum: 0);
        int extrude = options.GetInt32("extrude", 1, minimum: 0);
        int maxWidth = options.GetInt32("max-width", 2048, minimum: 1);
        int maxHeight = options.GetInt32("max-height", 2048, minimum: 1);
        bool powerOfTwo = options.GetSwitch("power-of-two");
        bool square = options.GetSwitch("square");
        options.ThrowIfUnrecognized();

        var stopwatch = Stopwatch.StartNew();
        string textureBaseName = Path.GetFileName(textureOutputBase);
        if (atlasLabel.Length == 0)
        {
            atlasLabel = textureBaseName;
        }

        List<string> inputPaths = ContentPaths.ReadPathList(inputList);
        if (inputPaths.Count == 0)
        {
            throw new ContentBuildException("No input image files specified.");
        }
        ContentPaths.ThrowIfDuplicateNames(inputPaths, "region");

        ContentBuildOutput.Message($"Processing texture atlas '{atlasLabel}'...");
        List<Sprite> sprites = LoadSprites(inputPaths);
        List<Sprite> primaries = MarkDuplicates(sprites);

        // Reserve each sprite's trimmed footprint plus the extrude band on every
        // side and the padding buffer.
        int outset = padding + (2 * extrude);
        foreach (Sprite sprite in primaries)
        {
            if (sprite.TrimWidth + outset > maxWidth || sprite.TrimHeight + outset > maxHeight)
            {
                throw new ContentBuildException($"Sprite '{sprite.Name}' ({sprite.TrimWidth}x{sprite.TrimHeight} trimmed) exceeds the maximum page size {maxWidth} x {maxHeight}.");
            }
        }

        // Insert by decreasing max side, then area, for better packing density.
        List<Sprite> remaining = primaries
            .OrderByDescending(s => Math.Max(s.TrimWidth, s.TrimHeight))
            .ThenByDescending(s => s.TrimWidth * s.TrimHeight)
            .ThenBy(s => s.Name, ContentPaths.NameComparer)
            .ToList();

        Directory.CreateDirectory(intermediateDir);
        string? outputDir = Path.GetDirectoryName(textureOutputBase);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        string outputDisplayBase = outputDisplayFile.Length > 0 ? Path.ChangeExtension(outputDisplayFile, null) : textureBaseName;

        var pages = new List<AtlasPage>();
        int spriteIndex = 0;
        while (remaining.Count > 0)
        {
            int pageIndex = pages.Count;
            (Dictionary<Sprite, Placement> placements, List<Sprite> unplaced) =
                PackPage(remaining, outset, maxWidth, maxHeight, powerOfTwo, square);

            // Placements so far are reservation origins; move them to the sprite's
            // inner (un-extruded) content, which is what the descriptor records.
            // The extruded pixels lie outside the recorded frame.
            int usedWidth = 1;
            int usedHeight = 1;
            foreach (Sprite sprite in placements.Keys.ToList())
            {
                Placement p = placements[sprite] with { X = placements[sprite].X + extrude, Y = placements[sprite].Y + extrude };
                placements[sprite] = p;
                usedWidth = Math.Max(usedWidth, p.X + p.Width + extrude);
                usedHeight = Math.Max(usedHeight, p.Y + p.Height + extrude);
            }
            if (square)
            {
                usedWidth = usedHeight = Math.Max(usedWidth, usedHeight);
            }
            if (powerOfTwo)
            {
                usedWidth = MaxRectsPacker.NextPowerOfTwo(usedWidth);
                usedHeight = MaxRectsPacker.NextPowerOfTwo(usedHeight);
            }

            string pageName = $"{textureBaseName}_{pageIndex}";
            string finalTexture = $"{textureOutputBase}_{pageIndex}{textureOutputExtension}";
            ContentBuildOutput.Message($"[{(outputDisplayFile.Length > 0 ? $"{outputDisplayBase}_{pageIndex}{textureOutputExtension}" : finalTexture)}]");

            var canvas = new RgbaImage(usedWidth, usedHeight);
            foreach (Sprite sprite in sprites)
            {
                if (!placements.TryGetValue(sprite, out Placement p))
                {
                    continue;
                }
                spriteIndex++;
                ContentBuildOutput.Message($"[{spriteIndex}/{primaries.Count}] {sprite.Path}");
                CopyTrimmed(sprite, canvas, p.X, p.Y);
                Extrude(sprite, canvas, p.X, p.Y, extrude);
            }

            string intermediatePng = Path.Combine(intermediateDir, pageName + ".png");
            canvas.SavePng(intermediatePng);

            // The framework's texture processor does the final encode (including
            // premultiplied alpha), so atlases inherit its encoder choice.
            ExternalProcess.RunShellCommand(
                ExternalProcess.ExpandCommandTemplate(encodeTemplate, intermediatePng, finalTexture, intermediateDir),
                "Texture encode command");

            // Duplicates share their primary's packed region.
            foreach (Sprite sprite in sprites)
            {
                if (sprite.Primary is not null && placements.TryGetValue(sprite.Primary, out Placement primaryPlacement))
                {
                    placements[sprite] = primaryPlacement;
                }
            }

            pages.Add(new AtlasPage(pageName + textureOutputExtension, placements));
            remaining = unplaced;
        }

        WriteDescriptor(outputFile, sprites, pages);

        string summarySource = inputDisplayDir.Length > 0 ? Path.Combine(inputDisplayDir, "*") : $"{atlasLabel} inputs";
        ContentBuildOutput.Message($"{summarySource} -> {(outputDisplayFile.Length > 0 ? outputDisplayFile : outputFile)} [{ContentBuildOutput.FormatElapsed(stopwatch)}s]");
    }

    private static List<Sprite> LoadSprites(List<string> inputPaths)
    {
        var sprites = new List<Sprite>(inputPaths.Count);
        foreach (string path in inputPaths)
        {
            if (!File.Exists(path))
            {
                throw new ContentBuildException($"Source image file not found: '{path}'");
            }
            var image = RgbaImage.Load(path);

            // Source images use straight alpha, so alpha == 0 is fully transparent.
            // A fully transparent image packs as a 1x1 placeholder so its region
            // still exists.
            (int x, int y, int width, int height) = ComputeAlphaTrim(image) ?? (0, 0, 1, 1);
            sprites.Add(new Sprite(Path.GetFileNameWithoutExtension(path), path, image, x, y, width, height));
        }
        return sprites;
    }

    // Tight bounds of the pixels with non-zero alpha, or null when fully transparent.
    private static (int X, int Y, int Width, int Height)? ComputeAlphaTrim(RgbaImage image)
    {
        int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
        byte[] pixels = image.Pixels;
        for (int y = 0; y < image.Height; y++)
        {
            int row = y * image.Stride;
            for (int x = 0; x < image.Width; x++)
            {
                if (pixels[row + (x * 4) + 3] != 0)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }
        return maxX < 0 ? null : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // Groups sprites whose trimmed pixels are identical.  The first by name in each
    // group is packed; the others reference it.  Returns the sprites to pack.
    private static List<Sprite> MarkDuplicates(List<Sprite> sprites)
    {
        int duplicateCount = 0;
        foreach (var group in sprites.GroupBy(ComputeTrimmedHash))
        {
            Sprite? primary = null;
            foreach (Sprite sprite in group.Order(SpriteNameComparer.Instance))
            {
                if (primary is null)
                {
                    primary = sprite;
                }
                else
                {
                    sprite.Primary = primary;
                    duplicateCount++;
                }
            }
        }
        if (duplicateCount > 0)
        {
            ContentBuildOutput.Message($"Detected {duplicateCount} duplicate texture(s); sharing atlas region(s) with primary.");
        }
        return sprites.Where(s => s.Primary is null).ToList();
    }

    private static string ComputeTrimmedHash(Sprite sprite)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> size = stackalloc byte[8];
        BitConverter.TryWriteBytes(size, sprite.TrimWidth);
        BitConverter.TryWriteBytes(size[4..], sprite.TrimHeight);
        hash.AppendData(size);
        for (int y = 0; y < sprite.TrimHeight; y++)
        {
            hash.AppendData(sprite.Image.Pixels, ((sprite.TrimY + y) * sprite.Image.Stride) + (sprite.TrimX * 4), sprite.TrimWidth * 4);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    // Packs as many sprites as fit on one page.  The page starts at the square root of
    // the remaining area and grows (doubling the shorter side, or both if square) until
    // everything fits or it reaches the maximum size; the rest spills to the next page.
    private static (Dictionary<Sprite, Placement> Placements, List<Sprite> Unplaced) PackPage(
        List<Sprite> sprites, int outset, int maxWidth, int maxHeight, bool powerOfTwo, bool square)
    {
        long area = 0;
        foreach (Sprite sprite in sprites)
        {
            area += (long)(sprite.TrimWidth + outset) * (sprite.TrimHeight + outset);
        }
        int width = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(area)));
        int height = width;

        while (true)
        {
            int tryWidth = width;
            int tryHeight = height;
            if (square)
            {
                tryWidth = tryHeight = Math.Max(tryWidth, tryHeight);
            }
            if (powerOfTwo)
            {
                tryWidth = MaxRectsPacker.NextPowerOfTwo(tryWidth);
                tryHeight = MaxRectsPacker.NextPowerOfTwo(tryHeight);
            }
            // NOTE: Both sides: a page only spills once neither can grow
            if (tryWidth >= maxWidth)
            {
                tryWidth = maxWidth;
            }
            if (tryHeight >= maxHeight)
            {
                tryHeight = maxHeight;
            }
            bool atMax = (tryWidth == maxWidth) && (tryHeight == maxHeight);

            var packer = new MaxRectsPacker(tryWidth, tryHeight);
            var placements = new Dictionary<Sprite, Placement>();
            var unplaced = new List<Sprite>();
            foreach (Sprite sprite in sprites)
            {
                if (packer.TryInsert(sprite.TrimWidth + outset, sprite.TrimHeight + outset, out int x, out int y))
                {
                    placements[sprite] = new Placement(x, y, sprite.TrimWidth, sprite.TrimHeight);
                }
                else if (atMax)
                {
                    unplaced.Add(sprite);
                }
                else
                {
                    break;
                }
            }

            if (placements.Count == sprites.Count || atMax)
            {
                if (placements.Count == 0)
                {
                    // Unreachable: every sprite was checked to fit on an empty page.
                    throw new ContentBuildException($"Unable to place any remaining sprite on a {tryWidth} x {tryHeight} page.");
                }
                return (placements, unplaced);
            }

            if (square || width <= height)
            {
                width *= 2;
            }
            else
            {
                height *= 2;
            }
        }
    }

    private static void CopyTrimmed(Sprite sprite, RgbaImage canvas, int dx, int dy)
    {
        int rowBytes = sprite.TrimWidth * 4;
        for (int y = 0; y < sprite.TrimHeight; y++)
        {
            Buffer.BlockCopy(
                sprite.Image.Pixels, ((sprite.TrimY + y) * sprite.Image.Stride) + (sprite.TrimX * 4),
                canvas.Pixels, ((dy + y) * canvas.Stride) + (dx * 4),
                rowBytes);
        }
    }

    // Repeats the trimmed sprite's outermost rows and columns outward into an
    // 'extrude'-pixel band on every side; the corners take the corner pixels.
    private static void Extrude(Sprite sprite, RgbaImage canvas, int dx, int dy, int extrude)
    {
        if (extrude <= 0)
        {
            return;
        }
        byte[] src = sprite.Image.Pixels;
        byte[] dst = canvas.Pixels;
        int srcStride = sprite.Image.Stride;
        int dstStride = canvas.Stride;
        int tx = sprite.TrimX, ty = sprite.TrimY, tw = sprite.TrimWidth, th = sprite.TrimHeight;
        int rowBytes = tw * 4;

        int topRow = (ty * srcStride) + (tx * 4);
        int bottomRow = ((ty + th - 1) * srcStride) + (tx * 4);
        for (int e = 0; e < extrude; e++)
        {
            Buffer.BlockCopy(src, topRow, dst, ((dy - extrude + e) * dstStride) + (dx * 4), rowBytes);
            Buffer.BlockCopy(src, bottomRow, dst, ((dy + th + e) * dstStride) + (dx * 4), rowBytes);
        }

        for (int y = 0; y < th; y++)
        {
            int left = ((ty + y) * srcStride) + (tx * 4);
            int right = left + ((tw - 1) * 4);
            int dstRow = (dy + y) * dstStride;
            for (int e = 0; e < extrude; e++)
            {
                Buffer.BlockCopy(src, left, dst, dstRow + ((dx - extrude + e) * 4), 4);
                Buffer.BlockCopy(src, right, dst, dstRow + ((dx + tw + e) * 4), 4);
            }
        }

        FillCorner(src, srcStride, tx, ty, dst, dstStride, dx - extrude, dy - extrude, extrude);
        FillCorner(src, srcStride, tx + tw - 1, ty, dst, dstStride, dx + tw, dy - extrude, extrude);
        FillCorner(src, srcStride, tx, ty + th - 1, dst, dstStride, dx - extrude, dy + th, extrude);
        FillCorner(src, srcStride, tx + tw - 1, ty + th - 1, dst, dstStride, dx + tw, dy + th, extrude);
    }

    private static void FillCorner(byte[] src, int srcStride, int sx, int sy, byte[] dst, int dstStride, int dx, int dy, int size)
    {
        int srcPixel = (sy * srcStride) + (sx * 4);
        for (int y = 0; y < size; y++)
        {
            int dstRow = ((dy + y) * dstStride) + (dx * 4);
            for (int x = 0; x < size; x++)
            {
                Buffer.BlockCopy(src, srcPixel, dst, dstRow + (x * 4), 4);
            }
        }
    }

    private static void WriteDescriptor(string path, List<Sprite> sprites, List<AtlasPage> pages)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("textures");
            foreach (AtlasPage page in pages)
            {
                writer.WriteStartObject();
                writer.WriteString("name", page.Name);
                writer.WriteStartObject("regions");
                foreach (Sprite sprite in sprites.Where(page.Placements.ContainsKey).Order(SpriteNameComparer.Instance))
                {
                    Placement p = page.Placements[sprite];
                    writer.WriteStartObject(sprite.Name);
                    WriteRect(writer, "frame", p.X, p.Y, p.Width, p.Height);
                    bool trimmed = sprite.TrimX != 0 || sprite.TrimY != 0 ||
                                   sprite.TrimWidth != sprite.Image.Width || sprite.TrimHeight != sprite.Image.Height;
                    if (trimmed)
                    {
                        WriteRect(writer, "source", p.X - sprite.TrimX, p.Y - sprite.TrimY, sprite.Image.Width, sprite.Image.Height);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static void WriteRect(Utf8JsonWriter writer, string name, int x, int y, int width, int height)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("x", x);
        writer.WriteNumber("y", y);
        writer.WriteNumber("w", width);
        writer.WriteNumber("h", height);
        writer.WriteEndObject();
    }

    private sealed class Sprite(string name, string path, RgbaImage image, int trimX, int trimY, int trimWidth, int trimHeight)
    {
        public string Name { get; } = name;

        public string Path { get; } = path;

        public RgbaImage Image { get; } = image;

        public int TrimX { get; } = trimX;

        public int TrimY { get; } = trimY;

        public int TrimWidth { get; } = trimWidth;

        public int TrimHeight { get; } = trimHeight;

        /// <summary>The sprite with identical trimmed pixels whose region this sprite shares.</summary>
        public Sprite? Primary { get; set; }
    }

    private sealed class SpriteNameComparer : IComparer<Sprite>
    {
        public static readonly SpriteNameComparer Instance = new();

        public int Compare(Sprite? x, Sprite? y) => ContentPaths.NameComparer.Compare(x?.Name, y?.Name);
    }

    private readonly record struct Placement(int X, int Y, int Width, int Height);

    private sealed record AtlasPage(string Name, Dictionary<Sprite, Placement> Placements);
}

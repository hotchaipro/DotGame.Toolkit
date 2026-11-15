// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Toolkit.IO;

namespace DotGame.Toolkit.Graphics;

internal sealed class TextureAtlasManifest
{
    /// <summary>
    /// The texture entries declared by the manifest, in file order. Each entry
    /// references a base texture by name and owns the regions packed into it.
    /// </summary>
    public List<TextureAtlasTextureEntry> Textures { get; } = new();

    internal static async Task<TextureAtlasManifest> ReadAsync(
        Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var data = await stream.ReadAllBytesAsync().ConfigureAwait(false);
        var reader = new Utf8JsonReader(data.Span);

        var manifest = new TextureAtlasManifest();

        // Expect start object
        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();

            if (string.Equals(propertyName, "textures", StringComparison.OrdinalIgnoreCase))
            {
                ReadTextures(ref reader, manifest.Textures);
            }
            else
            {
                // Skip unknown property value
                reader.Read();
                reader.Skip();
            }
        }

        return manifest;
    }

    // Schema: "textures" is an array of texture entries. Each entry carries a
    // "name" referencing a base texture and a "regions" dictionary of sprites
    // packed into it. Additional per-texture metadata can be added as purely
    // additive fields without breaking the schema.
    private static void ReadTextures(
        ref Utf8JsonReader reader,
        List<TextureAtlasTextureEntry> textures)
    {
        ReadToken(ref reader, JsonTokenType.StartArray);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(
                    $"Expected {JsonTokenType.StartObject} but found {reader.TokenType}.");
            }

            textures.Add(ReadTextureEntry(ref reader));
        }
    }

    private static TextureAtlasTextureEntry ReadTextureEntry(
        ref Utf8JsonReader reader)
    {
        string? name = null;
        var regions = new Dictionary<string, TextureAtlasRegion>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();

            if (string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase))
            {
                reader.Read();
                name = reader.GetString();
            }
            else if (string.Equals(propertyName, "regions", StringComparison.OrdinalIgnoreCase))
            {
                ReadRegions(ref reader, regions);
            }
            else
            {
                // Skip unknown property value (forward-compat for future fields).
                reader.Read();
                reader.Skip();
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new JsonException("Texture entry is missing required 'name' property.");
        }

        return new TextureAtlasTextureEntry(name, regions);
    }

    private static void ReadRegions(
        ref Utf8JsonReader reader,
        Dictionary<string, TextureAtlasRegion> regions)
    {
        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()
                ?? throw new JsonException("Expected region name.");

            var region = ReadRegion(ref reader);
            regions.Add(name, region);
        }
    }

    // Schema (minimal, two-rectangle form):
    //   frame:  { x, y, w, h }   trimmed pixels in atlas-page coordinates
    //   source: { x, y, w, h }   untrimmed sprite bounds in the *same*
    //                            atlas-page coordinate space; omitted when
    //                            equal to frame (i.e. not trimmed)
    //   center: { x, y, w, h }   optional 9-slice interior (stretchable)
    //                            rectangle in atlas-page coordinates;
    //                            must be contained within source. The 8
    //                            outer slices and the cap insets are
    //                            derived from source - center, matching
    //                            Android's .9.png convention.
    //   layout: { x, y, w, h }   optional layout (visual) bounds in
    //                            atlas-page coordinates; must be contained
    //                            within source. The outset (e.g. a baked-in
    //                            drop shadow drawn outside the layout
    //                            bounds) is derived from source - layout,
    //                            matching Android's nine-patch layout bounds.
    //                            Applies to both sprites and nine-patches.
    private static TextureAtlasRegion ReadRegion(
        ref Utf8JsonReader reader)
    {
        int frameX = 0;
        int frameY = 0;
        int frameWidth = 0;
        int frameHeight = 0;

        bool haveSource = false;
        int sourceX = 0;
        int sourceY = 0;
        int sourceWidth = 0;
        int sourceHeight = 0;

        bool haveCenter = false;
        int centerX = 0;
        int centerY = 0;
        int centerWidth = 0;
        int centerHeight = 0;

        bool haveLayout = false;
        int layoutX = 0;
        int layoutY = 0;
        int layoutWidth = 0;
        int layoutHeight = 0;

        ReadToken(ref reader, JsonTokenType.StartObject);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();

            reader.Read();

            if (string.Equals(propertyName, "frame", StringComparison.OrdinalIgnoreCase)
                && reader.TokenType == JsonTokenType.StartObject)
            {
                ReadXYWHObject(ref reader, out frameX, out frameY, out frameWidth, out frameHeight);
            }
            else if (string.Equals(propertyName, "source", StringComparison.OrdinalIgnoreCase)
                && reader.TokenType == JsonTokenType.StartObject)
            {
                ReadXYWHObject(ref reader, out sourceX, out sourceY, out sourceWidth, out sourceHeight);
                haveSource = true;
            }
            else if (string.Equals(propertyName, "center", StringComparison.OrdinalIgnoreCase)
                && reader.TokenType == JsonTokenType.StartObject)
            {
                ReadXYWHObject(ref reader, out centerX, out centerY, out centerWidth, out centerHeight);
                haveCenter = true;
            }
            else if (string.Equals(propertyName, "layout", StringComparison.OrdinalIgnoreCase)
                && reader.TokenType == JsonTokenType.StartObject)
            {
                ReadXYWHObject(ref reader, out layoutX, out layoutY, out layoutWidth, out layoutHeight);
                haveLayout = true;
            }
            else
            {
                reader.Skip();
            }
        }

        // When 'source' is omitted the sprite was not trimmed and
        // source == frame.
        if (!haveSource)
        {
            sourceX = frameX;
            sourceY = frameY;
            sourceWidth = frameWidth;
            sourceHeight = frameHeight;
        }

        // Derive cap insets from source - center. center must be contained
        // within source; otherwise the manifest is malformed.
        ThicknessInt? caps = null;
        if (haveCenter)
        {
            int sourceRight = sourceX + sourceWidth;
            int sourceBottom = sourceY + sourceHeight;
            int centerRight = centerX + centerWidth;
            int centerBottom = centerY + centerHeight;

            if (centerX < sourceX || centerY < sourceY
                || centerRight > sourceRight || centerBottom > sourceBottom
                || centerWidth < 0 || centerHeight < 0)
            {
                throw new JsonException(
                    "Region 'center' must lie entirely within 'source'.");
            }

            caps = new ThicknessInt(
                centerX - sourceX,
                centerY - sourceY,
                sourceRight - centerRight,
                sourceBottom - centerBottom);
        }

        // Derive the outset from source - layout. layout must be contained
        // within source; otherwise the manifest is malformed.
        ThicknessInt? outset = null;
        if (haveLayout)
        {
            int sourceRight = sourceX + sourceWidth;
            int sourceBottom = sourceY + sourceHeight;
            int layoutRight = layoutX + layoutWidth;
            int layoutBottom = layoutY + layoutHeight;

            if (layoutX < sourceX || layoutY < sourceY
                || layoutRight > sourceRight || layoutBottom > sourceBottom
                || layoutWidth < 0 || layoutHeight < 0)
            {
                throw new JsonException(
                    "Region 'layout' must lie entirely within 'source'.");
            }

            outset = new ThicknessInt(
                layoutX - sourceX,
                layoutY - sourceY,
                sourceRight - layoutRight,
                sourceBottom - layoutBottom);
        }

        return new TextureAtlasRegion(
            new RectangleInt(frameX, frameY, frameWidth, frameHeight),
            new RectangleInt(sourceX, sourceY, sourceWidth, sourceHeight),
            caps,
            outset);
    }

    // Reads a TexturePacker { x, y, w, h } object into four integers.
    private static void ReadXYWHObject(
        ref Utf8JsonReader reader,
        out int x,
        out int y,
        out int w,
        out int h)
    {
        x = 0;
        y = 0;
        w = 0;
        h = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();

            if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase))
            {
                x = reader.GetInt32();
            }
            else if (string.Equals(name, "y", StringComparison.OrdinalIgnoreCase))
            {
                y = reader.GetInt32();
            }
            else if (string.Equals(name, "w", StringComparison.OrdinalIgnoreCase))
            {
                w = reader.GetInt32();
            }
            else if (string.Equals(name, "h", StringComparison.OrdinalIgnoreCase))
            {
                h = reader.GetInt32();
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ReadToken(
        ref Utf8JsonReader reader,
        JsonTokenType expectedToken)
    {
        if (!reader.Read() || reader.TokenType != expectedToken)
        {
            throw new JsonException(
                $"Expected {expectedToken} but found {reader.TokenType}.");
        }
    }

    internal readonly struct TextureAtlasRegion(
        RectangleInt frame,
        RectangleInt source,
        ThicknessInt? caps,
        ThicknessInt? outset)
    {
        /// <summary>
        /// The trimmed pixels in atlas-page coordinates
        /// (TexturePacker's <c>frame</c>).
        /// </summary>
        public RectangleInt Frame { get; } = frame;

        /// <summary>
        /// The untrimmed (logical) sprite bounds, in the same atlas-page
        /// coordinate space as <see cref="Frame"/>. Equals <see cref="Frame"/>
        /// for non-trimmed regions; otherwise <see cref="Frame"/> sits
        /// strictly inside it.
        /// </summary>
        public RectangleInt Source { get; } = source;

        /// <summary>
        /// Optional 9-slice cap insets in untrimmed-sprite coordinates
        /// (TexturePacker's <c>border</c> field). <c>null</c> when the
        /// region is not a nine-patch.
        /// </summary>
        public ThicknessInt? Caps { get; } = caps;

        /// <summary>
        /// Optional outset in untrimmed-sprite coordinates, derived from the
        /// manifest's <c>layout</c> rectangle. <c>null</c> when absent.
        /// </summary>
        public ThicknessInt? Outset { get; } = outset;

        public bool IsTrimmed => this.Source != this.Frame;
    }

    internal sealed class TextureAtlasTextureEntry
    {
        public TextureAtlasTextureEntry(
            string name,
            Dictionary<string, TextureAtlasRegion> regions)
        {
            this.Name = name;
            this.Regions = regions;
        }

        /// <summary>
        /// Content name of the base texture that backs this entry. Resolved by
        /// the loader relative to the manifest's content directory.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Regions packed into this base texture, keyed by sprite name.
        /// </summary>
        public Dictionary<string, TextureAtlasRegion> Regions { get; }
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// A kind of content made from all the items of a folder: a texture atlas from TextureAtlasContent items, a sound
/// bank from SoundBankContent items. Its settings are the items' metadata that apply to the whole atlas or bank,
/// as ContentPipeline.Toolkit.targets reads them, with their defaults there.
/// </summary>
internal sealed class ContentGroupKind
{
    public static readonly ContentGroupKind TextureAtlas = new(
        "TextureAtlasContent",
        "DotGameTextureAtlas",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "spritesheet",
            ["Compression"] = "Lossless",
            ["LosslessQuality"] = "10",
            ["LossyQuality"] = "8",
            ["GpuCompression"] = "false",
            ["Preload"] = "false",
        });

    public static readonly ContentGroupKind SoundBank = new(
        "SoundBankContent",
        "DotGameSoundBank",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "soundbank",
            ["Compression"] = "Lossless",
            ["LossyQuality"] = "8",
            ["LosslessFormat"] = "Default",
            ["LossyFormat"] = "Default",
            ["Preload"] = "false",
        });

    public static readonly IReadOnlyList<ContentGroupKind> All = [TextureAtlas, SoundBank];

    private ContentGroupKind(string itemType, string extenderName, IReadOnlyDictionary<string, string> defaults)
    {
        this.ItemType = itemType;
        this.ExtenderName = extenderName;
        this.Defaults = defaults;
    }

    /// <summary>The items' type, e.g. TextureAtlasContent.</summary>
    public string ItemType { get; }

    /// <summary>The name of the automation extender that shows these settings in a folder's Properties window.</summary>
    public string ExtenderName { get; }

    /// <summary>The settings, each with the value the pipeline uses when an item has none.</summary>
    public IReadOnlyDictionary<string, string> Defaults { get; }

    public static ContentGroupKind? FromExtenderName(string? extenderName)
    {
        foreach (ContentGroupKind kind in All)
        {
            if (string.Equals(kind.ExtenderName, extenderName, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }
}

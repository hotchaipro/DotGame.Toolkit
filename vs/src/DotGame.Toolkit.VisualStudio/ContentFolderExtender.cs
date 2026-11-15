// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Threading;

namespace DotGame.Toolkit.VisualStudio;

/// <summary>
/// The settings of a folder's atlas or bank in its Properties window. A setting the items don't give shows the
/// pipeline's default; one they give different values shows blank. Setting one writes it to the project file.
/// </summary>
[ComVisible(true)]
public abstract class ContentFolderExtender
{
    private readonly ContentFolderProject folder;
    private readonly JoinableTaskFactory joinableTaskFactory;
    private ContentFolderSnapshot snapshot;

    internal ContentFolderExtender(ContentFolderProject folder, ContentFolderSnapshot snapshot, JoinableTaskFactory joinableTaskFactory)
    {
        this.folder = folder;
        this.snapshot = snapshot;
        this.joinableTaskFactory = joinableTaskFactory;
    }

    internal string Get(string name)
    {
        if (!this.snapshot.Values.TryGetValue(name, out string? value) || value == null)
        {
            return string.Empty;
        }

        if (value.Length > 0)
        {
            return value;
        }

        // An atlas with GPU compression is lossy unless it says otherwise
        if (this.folder.Kind == ContentGroupKind.TextureAtlas
            && string.Equals(name, "Compression", StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Get("GpuCompression"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return "Lossy";
        }

        return this.folder.Kind.Defaults[name];
    }

    internal void Set(string name, string? value)
    {
        if (string.Equals(value ?? string.Empty, this.Get(name), StringComparison.Ordinal))
        {
            return;
        }

        this.joinableTaskFactory.Run(async () =>
        {
            await this.folder.WriteAsync(name, value, default);
            this.snapshot = await this.folder.ReadAsync(default) ?? this.snapshot;
        });
    }
}

/// <summary>A texture atlas's settings, on its folder.</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public sealed class TextureAtlasFolderExtender : ContentFolderExtender
{
    private const string Category = "Texture Atlas";

    internal TextureAtlasFolderExtender(ContentFolderProject folder, ContentFolderSnapshot snapshot, JoinableTaskFactory joinableTaskFactory)
        : base(folder, snapshot, joinableTaskFactory)
    {
    }

    [Category(Category)]
    [DisplayName("Atlas Name")]
    [Description("The atlas's name (default spritesheet): the file name of its descriptor (.json) and pages, and the key the game loads it by. Blank when the folder's items make more than one atlas: set each one's Name on its files.")]
    public string AtlasName
    {
        get => this.Get("Name");
        set => this.Set("Name", value);
    }

    [Category(Category)]
    [DisplayName("Compression")]
    [Description("Selects the codec used to encode each atlas page: Lossless (default) keeps every pixel's color, exactly or within Lossless Quality, and keeps sharp edges and flat colors; Lossy only looks right, at Lossy Quality.")]
    [TypeConverter(typeof(CompressionConverter))]
    public string Compression
    {
        get => this.Get("Compression");
        set => this.Set("Compression", value);
    }

    [Category(Category)]
    [DisplayName("Lossless Quality")]
    [Description("Quality scale 1..10 (default 10) for Lossless compression only: 10 is exact, lower lets colors change a little where that saves bytes, never further than asked for.")]
    [TypeConverter(typeof(QualityConverter))]
    public string LosslessQuality
    {
        get => this.Get("LosslessQuality");
        set => this.Set("LosslessQuality", value);
    }

    [Category(Category)]
    [DisplayName("Lossy Quality")]
    [Description("Quality scale 1..10 (default 8) for Lossy compression only.")]
    [TypeConverter(typeof(QualityConverter))]
    public string LossyQuality
    {
        get => this.Get("LossyQuality");
        set => this.Set("LossyQuality", value);
    }

    [Category(Category)]
    [DisplayName("GPU Compression")]
    [Description("Whether to store the atlas's pages in a format the platform's GPU samples compressed (default false): ASTC on Android and iOS, a quarter of the memory or less, with nothing to decode at load, but larger to download than lossy WebP. GPU formats are lossy, so the pages are too. Platforms without one use their lossy encoding.")]
    [TypeConverter(typeof(BooleanValueConverter))]
    public string GpuCompression
    {
        get => this.Get("GpuCompression");
        set => this.Set("GpuCompression", value);
    }

    [Category(Category)]
    [DisplayName("Preload")]
    [Description("Whether the game needs the atlas before its first frame (default false). On the web, the page starts downloading it at once, alongside the game's code; elsewhere content is read from the app, so it changes nothing.")]
    [TypeConverter(typeof(BooleanValueConverter))]
    public string Preload
    {
        get => this.Get("Preload");
        set => this.Set("Preload", value);
    }
}

/// <summary>A sound bank's settings, on its folder.</summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDispatch)]
public sealed class SoundBankFolderExtender : ContentFolderExtender
{
    private const string Category = "Sound Bank";

    internal SoundBankFolderExtender(ContentFolderProject folder, ContentFolderSnapshot snapshot, JoinableTaskFactory joinableTaskFactory)
        : base(folder, snapshot, joinableTaskFactory)
    {
    }

    [Category(Category)]
    [DisplayName("Bank Name")]
    [Description("The bank's name (default soundbank): the file name of its descriptor (.json) and audio, and the key the game loads it by. Blank when the folder's items make more than one bank: set each one's Name on its files.")]
    public string BankName
    {
        get => this.Get("Name");
        set => this.Set("Name", value);
    }

    [Category(Category)]
    [DisplayName("Compression")]
    [Description("Selects the audio codec used for the bank: Lossless (default) or Lossy (at Lossy Quality).")]
    [TypeConverter(typeof(CompressionConverter))]
    public string Compression
    {
        get => this.Get("Compression");
        set => this.Set("Compression", value);
    }

    [Category(Category)]
    [DisplayName("Lossy Quality")]
    [Description("Quality scale 1..10 (default 8) used by Lossy compression. Ignored when Compression is Lossless.")]
    [TypeConverter(typeof(QualityConverter))]
    public string LossyQuality
    {
        get => this.Get("LossyQuality");
        set => this.Set("LossyQuality", value);
    }

    [Category(Category)]
    [DisplayName("Lossless Format")]
    [Description("WAV (default), read as it is, or FLAC: about half the size, but decoded as it loads. Ignored when Compression is Lossy.")]
    [TypeConverter(typeof(LosslessFormatConverter))]
    public string LosslessFormat
    {
        get => this.Get("LosslessFormat");
        set => this.Set("LosslessFormat", value);
    }

    [Category(Category)]
    [DisplayName("Lossy Format")]
    [Description("The platform's own lossy format (default: Opus on the web, desktop and Android, AAC on iOS), or Opus on every platform: smaller than AAC, but iOS decodes it with more CPU. Ignored when Compression is Lossless.")]
    [TypeConverter(typeof(LossyFormatConverter))]
    public string LossyFormat
    {
        get => this.Get("LossyFormat");
        set => this.Set("LossyFormat", value);
    }

    [Category(Category)]
    [DisplayName("Preload")]
    [Description("Whether the game needs the bank before its first frame (default false). On the web, the page starts downloading it at once, alongside the game's code; elsewhere content is read from the app, so it changes nothing.")]
    [TypeConverter(typeof(BooleanValueConverter))]
    public string Preload
    {
        get => this.Get("Preload");
        set => this.Set("Preload", value);
    }
}

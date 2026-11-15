// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;

/// <summary>
/// Builds one SpriteFontContent item: renders the glyphs of the primary font and
/// its fallback fonts with glyphgen (msdfgen / FreeType), packs them into one or
/// more atlas pages, encodes each page with the framework's lossless texture
/// processor and writes the .spritefont descriptor.
/// </summary>
/// <remarks>
/// <para>
/// All settings are read from the per-item settings sidecar written by the
/// targets' _ResolveSpriteFontContent task, which is also the incremental-build
/// invalidation signal.  One <c>key=value</c> per line:
/// <code>
/// font=&lt;abs path&gt;          primary font (the item's Include)
/// fallback=&lt;abs path&gt;      zero or more, in preference order
/// charsetFile=&lt;abs path&gt;   zero or more
/// charset=&lt;code points&gt;    space-separated decimal code points (literal Charset)
/// size, pxRange, pxRangeOffset, format, kerning, maxWidth, maxHeight, powerOfTwo
/// encode=&lt;command&gt;         lossless texture processor command template
/// </code>
/// </para>
/// <para>
/// Outputs <c>&lt;output-base&gt;.spritefont</c> and one
/// <c>&lt;output-base&gt;_&lt;n&gt;&lt;texture-output-extension&gt;</c> per atlas page.
/// </para>
/// </remarks>
internal static class SpriteFontCommand
{
    public const string Name = "spritefont";
    public const string ErrorCode = "FNT0001";
    private const string MissingCharactersWarningCode = "FNT0002";
    private const string NoCapHeightMessageCode = "FNT0003";
    private const int MaxMissingCharactersListed = 40;

    public static void Run(CommandLineOptions options)
    {
        string settingsFile = options.GetPath("settings");
        string glyphGen = options.GetPath("glyphgen");
        string outputBase = options.GetPath("output-base");
        string textureOutputExtension = options.GetRequired("texture-output-extension");
        string intermediateDir = options.GetPath("intermediate-dir");
        string displayName = options.GetString("display-name");
        options.ThrowIfUnrecognized();

        var stopwatch = Stopwatch.StartNew();
        string name = Path.GetFileName(outputBase);
        FontSettings settings = FontSettings.Read(settingsFile);

        var buildOptions = new SpriteFontBuildOptions
        {
            GlyphGenExe = glyphGen,
            PrimaryFont = settings.Font,
            FallbackFonts = [.. settings.Fallbacks],
            CharsetFiles = [.. settings.CharsetFiles],
            Charset = settings.Charset,
            Size = settings.GetSingle("size"),
            PxRange = settings.GetSingle("pxRange"),
            PxRangeOffset = settings.GetSingle("pxRangeOffset"),
            Format = settings.GetFormat(),
            Kerning = settings.GetBoolean("kerning"),
            MaxPageWidth = settings.GetInt32("maxWidth"),
            MaxPageHeight = settings.GetInt32("maxHeight"),
            PowerOfTwo = settings.GetBoolean("powerOfTwo"),
            IntermediateDir = intermediateDir,
            Name = name,
        };

        string label = displayName.Length > 0 ? displayName : name + ".spritefont";
        ContentBuildOutput.Message($"Processing font '{label}' ({settings.Get("format")}, size {settings.Get("size")})...");

        SpriteFontBuildResult result = SpriteFontBuilder.Build(buildOptions, ContentBuildOutput.Message);

        if (result.Glyphs.Count == 0)
        {
            throw new ContentBuildException($"Font '{label}' has none of the requested characters; check its Charset and FontFile.");
        }

        if (result.MissingCodePoints.Count > 0)
        {
            string missing = string.Join(' ', result.MissingCodePoints
                .Take(MaxMissingCharactersListed)
                .Select(c => "U+" + c.ToString("X4", CultureInfo.InvariantCulture)));
            if (result.MissingCodePoints.Count > MaxMissingCharactersListed)
            {
                missing += " ...";
            }
            ContentBuildOutput.Warning(settings.Font, MissingCharactersWarningCode,
                $"Font '{label}': {result.MissingCodePoints.Count} character(s) not found in the font or its fallbacks: {missing}");
        }

        // A message, not a warning: text fonts all have a cap height, so this is a symbol or icon
        // font, and centering its glyphs on the line box is what's wanted
        if (result.CapHeight is null)
        {
            ContentBuildOutput.Message($"{NoCapHeightMessageCode}: Font '{label}' doesn't give its cap height (no OS/2 sCapHeight, and no TrueType 'H' glyph); text centered by its capitals is centered by its line box instead");
        }

        string encodeTemplate = settings.Get("encode");
        string outputDir = Path.GetDirectoryName(Path.GetFullPath(outputBase))!;
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(intermediateDir);
        DeleteStalePages(outputDir, name, textureOutputExtension);

        var pageNames = new List<string>(result.Pages.Count);
        for (int i = 0; i < result.Pages.Count; i++)
        {
            FontPage page = result.Pages[i];
            string pageName = $"{name}_{i}";
            pageNames.Add(pageName);

            string intermediatePng = Path.Combine(intermediateDir, pageName + ".png");
            new RgbaImage(page.Width, page.Height, page.Rgba).SavePng(intermediatePng);

            string finalTexture = $"{outputBase}_{i}{textureOutputExtension}";
            ExternalProcess.RunShellCommand(
                ExternalProcess.ExpandCommandTemplate(encodeTemplate, intermediatePng, finalTexture, intermediateDir),
                "Texture encode command");
            ContentBuildOutput.Message($"  page {i}: {page.Width}x{page.Height}");
        }

        SpriteFontBuilder.WriteSpriteFont(outputBase + ".spritefont", buildOptions, result, pageNames);

        ContentBuildOutput.Message($"{Path.GetFileName(settings.Font)} -> {label} [{result.Glyphs.Count} glyphs, {result.Kerning.Count} kerning pairs, {result.Pages.Count} page(s), {ContentBuildOutput.FormatElapsed(stopwatch)}s]");
    }

    /// <summary>
    /// Removes pages left over from a previous build that produced more pages.
    /// </summary>
    private static void DeleteStalePages(string outputDir, string name, string extension)
    {
        var pattern = new Regex("^" + Regex.Escape(name) + @"_\d+" + Regex.Escape(extension) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (string file in Directory.EnumerateFiles(outputDir, name + "_*"))
        {
            if (pattern.IsMatch(Path.GetFileName(file)))
            {
                File.Delete(file);
            }
        }
    }

    private sealed class FontSettings
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        private readonly string _path;

        private FontSettings(string path)
        {
            this._path = path;
        }

        public List<string> Fallbacks { get; } = [];

        public List<string> CharsetFiles { get; } = [];

        public string Font => this.Get("font");

        public string Charset { get; private set; } = "";

        public static FontSettings Read(string path)
        {
            if (!File.Exists(path))
            {
                throw new ContentBuildException($"Font settings file not found: '{path}'.");
            }

            var settings = new FontSettings(path);
            foreach (string line in File.ReadAllLines(path))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                string key = line[..separator];
                string value = line[(separator + 1)..];
                switch (key)
                {
                    case "font":
                        settings._values[key] = ContentPaths.Normalize(value);
                        break;
                    case "fallback":
                        settings.Fallbacks.Add(ContentPaths.Normalize(value));
                        break;
                    case "charsetFile":
                        settings.CharsetFiles.Add(ContentPaths.Normalize(value));
                        break;
                    case "charset":
                        settings.Charset = DecodeCodePoints(value);
                        break;
                    default:
                        settings._values[key] = value;
                        break;
                }
            }
            return settings;
        }

        public string Get(string key) =>
            this._values.TryGetValue(key, out string? value) ? value : throw new ContentBuildException($"Font settings file '{this._path}' is missing '{key}'.");

        public float GetSingle(string key) =>
            float.TryParse(this.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : throw new ContentBuildException($"Font setting '{key}' is not a number: '{this.Get(key)}'.");

        public int GetInt32(string key) =>
            int.TryParse(this.Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : throw new ContentBuildException($"Font setting '{key}' is not an integer: '{this.Get(key)}'.");

        public bool GetBoolean(string key) =>
            this._values.TryGetValue(key, out string? value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        public GlyphFormat GetFormat() =>
            Enum.TryParse(this.Get("format"), ignoreCase: true, out GlyphFormat format) && Enum.IsDefined(format)
                ? format
                : throw new ContentBuildException($"Unknown font format '{this.Get("format")}'. Expected Bitmap, Sdf, Msdf or Mtsdf.");

        private static string DecodeCodePoints(string value)
        {
            var builder = new StringBuilder();
            foreach (string part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int codePoint) || !Rune.IsValid(codePoint))
                {
                    throw new ContentBuildException($"Invalid code point '{part}' in the font charset.");
                }
                builder.Append(char.ConvertFromUtf32(codePoint));
            }
            return builder.ToString();
        }
    }
}

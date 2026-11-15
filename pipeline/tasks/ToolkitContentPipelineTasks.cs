// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace DotGame.Toolkit.ContentPipeline.Tasks;

// _ResolveSpriteFontContent - applies defaults, validates every SpriteFontContent item,
// and writes a per-item settings sidecar (only when its content changes)
// that the build tool reads.  The sidecar holds every value that affects
// the output, so a metadata change invalidates the incremental build
// without touching any source timestamp.
//
// Metadata is read in C# rather than with MSBuild item functions because
// Charset literals may legitimately contain ';' and other characters that
// MSBuild would split or transform.
//
// Stamps on each item:
//   FontName, _SettingsFile, _SourceFiles (font + fallbacks + charset files),
//   _ExtraKey (content cache key part; no absolute paths),
//   _EncodeCommandResolved, _PremultiplyAlpha, _OutputExtension
public sealed class _ResolveSpriteFontContent : Task
{
    [Required]
    public ITaskItem[] Fonts { get; set; }

    [Required]
    public string ProjectDirectory { get; set; }

    [Required]
    public string IntermediateDir { get; set; }

    [Required]
    public string EncodeCommand { get; set; }

    [Required]
    public string OutputExtension { get; set; }

    [Required]
    public string MaxWidth { get; set; }

    [Required]
    public string MaxHeight { get; set; }

    [Required]
    public string PowerOfTwo { get; set; }

    [Output]
    public ITaskItem[] ResolvedFonts { get; set; }

    public override bool Execute()
    {
        var inv = CultureInfo.InvariantCulture;
        var results = new List<ITaskItem>();
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int maxWidth, maxHeight;
        if (!int.TryParse(this.MaxWidth, NumberStyles.Integer, inv, out maxWidth) || maxWidth < 64 ||
            !int.TryParse(this.MaxHeight, NumberStyles.Integer, inv, out maxHeight) || maxHeight < 64)
        {
            this.Log.LogError("SpriteFontAtlasMaxWidth / SpriteFontAtlasMaxHeight must be integers >= 64 (got '{0}' x '{1}').", this.MaxWidth, this.MaxHeight);
            return false;
        }
        bool pot = string.Equals(this.PowerOfTwo, "true", StringComparison.OrdinalIgnoreCase);

        // Project files use '\' separators; on macOS and Linux '\' is a valid file name character, so normalize.
        Func<string, string> nativePath = p => p.Replace('\\', Path.DirectorySeparatorChar);
        Func<string, string> fullPath = p => Path.GetFullPath(Path.IsPathRooted(nativePath(p)) ? nativePath(p) : Path.Combine(this.ProjectDirectory, nativePath(p)));
        Func<string, List<string>> splitPaths = v => (v ?? "")
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).Select(fullPath).ToList();

        foreach (var input in this.Fonts)
        {
            var item = new TaskItem(input);
            string id = input.ItemSpec;
            bool ok = true;
            Action<string> error = message => { this.Log.LogError("SpriteFontContent item '{0}': {1}", id, message); ok = false; };

            string name = input.GetMetadata("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = input.GetMetadata("Filename");
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\"))
            {
                error("Name '" + name + "' is not a valid file name.");
            }

            float size = 32f;
            string sizeText = input.GetMetadata("Size");
            if (!string.IsNullOrWhiteSpace(sizeText) &&
                (!float.TryParse(sizeText, NumberStyles.Float, inv, out size) || size < 4f || size > 512f))
            {
                error("Size='" + sizeText + "' must be a number between 4 and 512.");
            }

            string format = input.GetMetadata("GlyphFormat");
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "Mtsdf";
            }

            switch (format.ToLowerInvariant())
            {
                case "msdf": format = "msdf"; break;
                case "mtsdf": format = "mtsdf"; break;
                case "sdf": format = "sdf"; break;
                case "bitmap": format = "bitmap"; break;
                default: error("GlyphFormat='" + format + "' must be Msdf, Mtsdf, Sdf or Bitmap."); break;
            }

            // Room for an outline, glow or shadow of up to Size / 8 pixels, as most games draw some
            float pxRange = Math.Max(2f, size / 4f);
            string pxRangeText = input.GetMetadata("PxRange");
            int pxRangeWhole;
            if (!string.IsNullOrWhiteSpace(pxRangeText))
            {
                if (!int.TryParse(pxRangeText, NumberStyles.Integer, inv, out pxRangeWhole) || pxRangeWhole <= 0 || pxRangeWhole > size)
                {
                    error("PxRange='" + pxRangeText + "' must be a whole number of pixels, no larger than Size.");
                }
                else
                {
                    pxRange = pxRangeWhole;
                }
            }

            float offset = 0f;
            string offsetText = input.GetMetadata("PxRangeOffset");
            if (!string.IsNullOrWhiteSpace(offsetText) &&
                (!float.TryParse(offsetText, NumberStyles.Float, inv, out offset) || Math.Abs(offset) >= pxRange / 2f))
            {
                error("PxRangeOffset='" + offsetText + "' must be a number strictly between -PxRange/2 and +PxRange/2 (PxRange=" + pxRange.ToString(inv) + ").");
            }

            string kerningText = input.GetMetadata("Kerning");
            bool kerning = false;
            if (!string.IsNullOrWhiteSpace(kerningText) && !bool.TryParse(kerningText, out kerning))
            {
                error("Kerning='" + kerningText + "' must be true or false.");
            }

            string font = input.GetMetadata("FullPath");
            if (!File.Exists(font))
            {
                error("font file not found: " + font);
            }

            var fallbacks = splitPaths(input.GetMetadata("Fallback"));
            foreach (var f in fallbacks)
            {
                if (!File.Exists(f))
                {
                    error("Fallback font file not found: " + f);
                }
            }

            var charsetFiles = splitPaths(input.GetMetadata("CharsetFile"));
            foreach (var f in charsetFiles)
            {
                if (!File.Exists(f))
                {
                    error("CharsetFile not found: " + f);
                }
            }

            string relativeDir = input.GetMetadata("OutputRelativeDir");
            string outputKey = relativeDir + name;
            string previous;
            if (outputs.TryGetValue(outputKey, out previous))
            {
                error("output '" + outputKey + ".spritefont' is also produced by '" + previous + "'. Set a unique Name.");
            }
            else
            {
                outputs[outputKey] = id;
            }

            if (!ok)
            {
                continue;
            }

            var charset = new StringBuilder();
            string literal = input.GetMetadata("Charset");
            for (int i = 0; i < literal.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(literal[i]) && i + 1 < literal.Length && char.IsLowSurrogate(literal[i + 1]))
                {
                    cp = char.ConvertToUtf32(literal[i], literal[i + 1]);
                    i++;
                }
                else
                {
                    cp = literal[i];
                }
                if (charset.Length > 0)
                {
                    charset.Append(' ');
                }

                charset.Append(cp.ToString(inv));
            }

            bool premultiply = format == "bitmap";
            string encode = this.EncodeCommand.Replace("{Quality}", "8").Replace("{PremultiplyAlpha}", premultiply ? "true" : "false");

            string settingsBody =
                "size=" + size.ToString("R", inv) + "\n" +
                "pxRange=" + pxRange.ToString("R", inv) + "\n" +
                "pxRangeOffset=" + offset.ToString("R", inv) + "\n" +
                "format=" + format + "\n" +
                "kerning=" + (kerning ? "true" : "false") + "\n" +
                "maxWidth=" + maxWidth.ToString(inv) + "\n" +
                "maxHeight=" + maxHeight.ToString(inv) + "\n" +
                "powerOfTwo=" + (pot ? "true" : "false") + "\n" +
                "fallbacks=" + fallbacks.Count.ToString(inv) + "\n" +
                "charsetFiles=" + charsetFiles.Count.ToString(inv) + "\n" +
                "charset=" + charset + "\n";

            var settings = new StringBuilder();
            settings.Append("font=").Append(font).Append('\n');
            foreach (var f in fallbacks)
            {
                settings.Append("fallback=").Append(f).Append('\n');
            }

            foreach (var f in charsetFiles)
            {
                settings.Append("charsetFile=").Append(f).Append('\n');
            }

            settings.Append(settingsBody);
            settings.Append("encode=").Append(encode).Append('\n');

            string settingsFile = Path.Combine(nativePath(this.IntermediateDir), nativePath(relativeDir), name + ".font.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
            string text = settings.ToString();
            if (!File.Exists(settingsFile) || File.ReadAllText(settingsFile) != text)
            {
                File.WriteAllText(settingsFile, text);
            }

            var sources = new List<string> { font };
            sources.AddRange(fallbacks);
            sources.AddRange(charsetFiles);

            item.SetMetadata("FontName", name);
            item.SetMetadata("_SettingsFile", settingsFile);
            item.SetMetadata("_SourceFiles", string.Join(";", sources));
            item.SetMetadata("_ExtraKey", "font=" + name + "\n" + "ext=" + this.OutputExtension + "\n" + settingsBody);
            item.SetMetadata("_EncodeCommandResolved", encode);
            item.SetMetadata("_PremultiplyAlpha", premultiply ? "true" : "false");
            item.SetMetadata("_OutputExtension", this.OutputExtension);
            results.Add(item);
        }

        this.ResolvedFonts = results.ToArray();

        return !this.Log.HasLoggedErrors;
    }
}

// _FindPagedContentManifestEntries - one manifest entry per sprite font or texture atlas
// (its descriptor) plus one per page found in the build output, <name>_<n>: the page count
// depends on what was packed. Items of one atlas (the same folder and name) give one set.
public sealed class _FindPagedContentManifestEntries : Task
{
    [Required]
    public ITaskItem[] Items { get; set; }

    // The metadata with the descriptor's and pages' base name: FontName or Name
    [Required]
    public string NameMetadata { get; set; }

    // The descriptor's extension: .spritefont or .json
    [Required]
    public string DescriptorExtension { get; set; }

    [Required]
    public string ContentOutputDir { get; set; }

    [Output]
    public ITaskItem[] Entries { get; set; }

    public override bool Execute()
    {
        var entries = new List<ITaskItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var font in this.Items)
        {
            string name = font.GetMetadata(this.NameMetadata);
            string ext = font.GetMetadata("_OutputExtension");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ext))
            {
                continue; // not prepared (e.g. design-time build)
            }
            string dir = font.GetMetadata("OutputRelativeDir");
            if (!seen.Add(dir + name))
            {
                continue;
            }
            var descriptor = new TaskItem(dir + name);
            descriptor.SetMetadata("RelativePath", dir + name + this.DescriptorExtension);
            entries.Add(descriptor);

            // OutputRelativeDir comes from Link metadata, with Windows separators
            string pageDir = Path.Combine(this.ContentOutputDir, dir.Replace('\\', Path.DirectorySeparatorChar));
            for (int page = 0; ; page++)
            {
                string pageName = name + "_" + page;
                if (!File.Exists(Path.Combine(pageDir, pageName + ext)))
                {
                    break;
                }
                var entry = new TaskItem(dir + pageName);
                entry.SetMetadata("RelativePath", dir + pageName + ext);
                string premultiplied = font.GetMetadata("_PremultiplyAlpha");
                if (!string.IsNullOrEmpty(premultiplied))
                {
                    entry.SetMetadata("PremultipliedAlpha", premultiplied);
                }
                entries.Add(entry);
            }
        }
        this.Entries = entries.ToArray();

        return !this.Log.HasLoggedErrors;
    }
}

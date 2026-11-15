// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.ContentPipeline.Testing;
using DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;
using DotGame.Toolkit.ContentPipeline.Tasks;
using DotGame.Toolkit.Fonts;
using Microsoft.Build.Framework;
using Xunit;
using GlyphFormat = DotGame.Toolkit.ContentPipeline.BuildTool.Fonts.GlyphFormat;

namespace DotGame.Toolkit.ContentPipeline.Tests.Fonts;

/// <summary>
/// The .spritefont writer against the runtime's reader, the charset, glyphgen's output and the font
/// item's task.
/// </summary>
public sealed class SpriteFontWriterTests
{
    private static SpriteFontData WriteAndRead(TestDirectory directory, GlyphFormat format = GlyphFormat.Mtsdf, bool kerning = false, double? capHeight = 0.7)
    {
        string fontPath = directory.Path_("font.ttf");
        File.WriteAllBytes(fontPath, new SfntBuilder().Head(1000).Hhea(800, -200, 100).Post(-100, 50).Build());
        var result = new SpriteFontBuildResult
        {
            Primary = FontFile.Load(fontPath),
            CapHeight = capHeight,
            Pages = [new FontPage { Width = 64, Height = 32 }],
            SourceFamilies = ["Hot Sans"],
            SourceFiles = ["font.ttf"],
            Glyphs =
            [
                new GlyphGenGlyph { CodePoint = 'A', Page = 0, X = 1, Y = 2, Width = 10, Height = 12, Advance = 0.5, Left = 0.1, Top = 0.75, Right = 0.4, Bottom = -0.25 },
                new GlyphGenGlyph { CodePoint = 'V', Page = 0, X = 20, Y = 2, Width = 9, Height = 12, Advance = 0.45 },
            ],
            Kerning = [new KerningPair { First = 'A', Second = 'V', Adjust = -1.5f }],
        };
        var options = new SpriteFontBuildOptions { Size = 32f, PxRange = 4f, PxRangeOffset = 0.5f, Format = format, Kerning = kerning };
        string output = directory.Path_("out.spritefont");

        SpriteFontBuilder.WriteSpriteFont(output, options, result, ["out_0"]);

        return SpriteFontReader.Read(File.ReadAllBytes(output));
    }

    [Fact]
    public void Header_ReadsBack()
    {
        using var directory = new TestDirectory();

        SpriteFontData font = WriteAndRead(directory);

        Assert.Equal(SpriteFontReader.SupportedVersion, font.Version);
        Assert.Equal(32f, font.EmSize);
        Assert.Equal(4f, font.DistanceRange);
        Assert.Equal(-0.5f, font.DistanceRangeMiddle);
    }

    [Fact]
    public void Metrics_AreScaledToTheEmSize()
    {
        using var directory = new TestDirectory();

        SpriteFontData font = WriteAndRead(directory);

        // 800 / 1000 of 32, and so on
        Assert.Equal(25.6f, font.Ascender, 3);
        Assert.Equal(-6.4f, font.Descender, 3);
        Assert.Equal(35.2f, font.LineHeight, 3);
        Assert.Equal(22.4f, font.CapHeight, 3);
    }

    [Fact]
    public void Pages_ReadBack()
    {
        using var directory = new TestDirectory();

        SpriteFontData font = WriteAndRead(directory);

        SpriteFontPageData page = Assert.Single(font.Pages);
        Assert.Equal(("out_0", 64, 32), (page.Name, page.Width, page.Height));
    }

    [Fact]
    public void GlyphRecords_ReadBack_WithYDown()
    {
        using var directory = new TestDirectory();

        SpriteFontData font = WriteAndRead(directory);

        Assert.Equal(2, font.GlyphCount);
        SpriteFontReader.ReadGlyph(font.Glyphs.Span, 0, out int codePoint, out int page, out int x, out int y, out int width, out int height, out float advance, out float left, out float top, out float right, out float bottom);
        Assert.Equal('A', codePoint);
        Assert.Equal((0, 1, 2, 10, 12), (page, x, y, width, height));
        Assert.Equal(16f, advance, 3);
        Assert.Equal(3.2f, left, 3);
        Assert.Equal(-24f, top, 3);
        Assert.Equal(12.8f, right, 3);
        Assert.Equal(8f, bottom, 3);
    }

    [Fact]
    public void Kerning_IsWrittenOnlyWhenAskedFor()
    {
        using var withKerning = new TestDirectory();
        using var without = new TestDirectory();

        Assert.Equal(1, WriteAndRead(withKerning, kerning: true).Kerning.Length / SpriteFontReader.KerningRecordSize);
        Assert.Equal(0, WriteAndRead(without, kerning: false).Kerning.Length);
    }

    [Fact]
    public void BitmapFont_HasNoDistanceRange()
    {
        using var directory = new TestDirectory();

        Assert.Null(WriteAndRead(directory, GlyphFormat.Bitmap).DistanceRange);
    }

    [Fact]
    public void Reader_SkipsMembersItDoesntKnow()
    {
        // Additive format changes rely on this
        var stream = new MemoryStream();
        var writer = new DotGame.Toolkit.ContentPipeline.BuildTool.Serialization.PbonWriter(stream);
        writer.StartObject();
        writer.Member(1).Write(1);
        writer.Member(99).StartArray().Write("future").EndArray();
        writer.Member(3).Write(16f);
        writer.Member(7).StartArray().StartObject().Member(1).Write("p").Member(2).Write(1).Member(3).Write(1).EndObject().EndArray();
        writer.EndObject();

        Assert.Equal(16f, SpriteFontReader.Read(stream.ToArray()).EmSize);
    }

    [Fact]
    public void Reader_RefusesANewerVersion()
    {
        var stream = new MemoryStream();
        var writer = new DotGame.Toolkit.ContentPipeline.BuildTool.Serialization.PbonWriter(stream);
        writer.StartObject().Member(1).Write(SpriteFontReader.SupportedVersion + 1).EndObject();

        Assert.Throws<InvalidDataException>(() => SpriteFontReader.Read(stream.ToArray()));
    }

    [Fact]
    public void Charset_MergesLiteralAndFiles_Sorted_AndWithoutControlCharacters()
    {
        using var directory = new TestDirectory();
        string file = directory.WriteFile("charset.txt", "﻿b\r\n\tc🎮a");

        List<int> charset = SpriteFontBuilder.ResolveCharset("ba", [file]);

        Assert.Equal(['a', 'b', 'c', 0x1F3AE], charset);
    }

    [Fact]
    public void Charset_DefaultsToPrintableAscii()
    {
        List<int> charset = SpriteFontBuilder.ResolveCharset("", []);

        Assert.Equal(95, charset.Count);
        Assert.Equal(' ', charset[0]);
        Assert.Equal('~', charset[^1]);
    }

    [Fact]
    public void Charset_FailsOnAMissingFile()
    {
        Assert.Throws<FileNotFoundException>(() => SpriteFontBuilder.ResolveCharset("", [Path.Combine(Path.GetTempPath(), "no-such-charset.txt")]));
    }

    [Fact]
    public void GlyphGenOutput_RefusesAnotherFormatOrVersion()
    {
        Assert.Throws<InvalidDataException>(() => SpriteFontBuilder.ReadGlyphGenOutput([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => SpriteFontBuilder.ReadGlyphGenOutput("NOPE\u0002\0\0\0"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => SpriteFontBuilder.ReadGlyphGenOutput("GLGN\u0001\0\0\0"u8.ToArray()));
    }

    private static (bool Succeeded, _ResolveSpriteFontContent Task, RecordingBuildEngine Engine) Resolve(TestDirectory directory, params ITaskItem[] fonts)
    {
        var engine = new RecordingBuildEngine();
        var task = new _ResolveSpriteFontContent
        {
            BuildEngine = engine,
            Fonts = fonts,
            ProjectDirectory = directory.Root,
            IntermediateDir = directory.Path_("obj"),
            EncodeCommand = "enc {InputFile} {OutputFile} {Quality} {PremultiplyAlpha}",
            OutputExtension = ".ktx2",
            MaxWidth = "2048",
            MaxHeight = "2048",
            PowerOfTwo = "false",
        };
        return (task.Execute(), task, engine);
    }

    private static Microsoft.Build.Utilities.TaskItem Font(TestDirectory directory, params (string Name, string Value)[] metadata)
    {
        string path = directory.WriteFile("font.ttf", "not read by the task");
        return TaskItems.Item(path, [("OutputRelativeDir", "Content\\"), .. metadata]);
    }

    [Fact]
    public void ResolveFont_DefaultsItsNameAndSettings()
    {
        using var directory = new TestDirectory();

        var (succeeded, task, engine) = Resolve(directory, Font(directory));

        Assert.True(succeeded, string.Join('\n', engine.Errors));
        ITaskItem font = Assert.Single(task.ResolvedFonts);
        Assert.Equal("font", font.GetMetadata("FontName"));
        string settings = File.ReadAllText(font.GetMetadata("_SettingsFile"));
        Assert.Contains("size=32\n", settings, StringComparison.Ordinal);
        Assert.Contains("format=mtsdf\n", settings, StringComparison.Ordinal);
        // A quarter of the size: room for an outline, glow or shadow reaching 4 pixels
        Assert.Contains("pxRange=8\n", settings, StringComparison.Ordinal);
        Assert.Contains("kerning=false\n", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveFont_PremultipliesOnlyBitmapFonts()
    {
        using var directory = new TestDirectory();

        var (_, task, _) = Resolve(directory, Font(directory, ("Name", "A")), Font(directory, ("Name", "B"), ("GlyphFormat", "Bitmap")));

        Assert.Equal("false", task.ResolvedFonts[0].GetMetadata("_PremultiplyAlpha"));
        Assert.Contains(" 8 false", task.ResolvedFonts[0].GetMetadata("_EncodeCommandResolved"), StringComparison.Ordinal);
        Assert.Equal("true", task.ResolvedFonts[1].GetMetadata("_PremultiplyAlpha"));
    }

    [Fact]
    public void ResolveFont_KeepsTheSettingsFilesTime_WhenNothingChanged()
    {
        using var directory = new TestDirectory();
        var (_, first, _) = Resolve(directory, Font(directory));
        string settings = first.ResolvedFonts[0].GetMetadata("_SettingsFile");
        DateTime old = DateTime.UtcNow.AddHours(-1);
        File.SetLastWriteTimeUtc(settings, old);

        Resolve(directory, Font(directory));
        Assert.Equal(old, File.GetLastWriteTimeUtc(settings));

        Resolve(directory, Font(directory, ("Size", "48")));
        Assert.NotEqual(old, File.GetLastWriteTimeUtc(settings));
    }

    [Fact]
    public void ResolveFont_TheKeyHoldsTheSettings_ButNoAbsolutePaths()
    {
        using var directory = new TestDirectory();

        var (_, task, _) = Resolve(directory, Font(directory, ("Size", "48")));

        string key = task.ResolvedFonts[0].GetMetadata("_ExtraKey");
        Assert.Contains("size=48", key, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Root, key, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Size", "3")]
    [InlineData("Size", "513")]
    [InlineData("Size", "big")]
    [InlineData("GlyphFormat", "Vector")]
    [InlineData("PxRange", "0")]
    [InlineData("PxRange", "1000")]
    [InlineData("PxRangeOffset", "99")]
    [InlineData("Kerning", "maybe")]
    [InlineData("Name", "a/b")]
    [InlineData("Fallback", "no-such-font.ttf")]
    [InlineData("CharsetFile", "no-such-charset.txt")]
    public void ResolveFont_RefusesABadSetting_NamingIt(string name, string value)
    {
        using var directory = new TestDirectory();

        var (succeeded, _, engine) = Resolve(directory, Font(directory, (name, value)));

        Assert.False(succeeded);
        Assert.Contains(engine.Errors, error => error.Contains(value, StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveFont_RefusesTwoFontsWithOneOutput()
    {
        using var directory = new TestDirectory();

        var (succeeded, _, engine) = Resolve(directory, Font(directory, ("Name", "Same")), Font(directory, ("Name", "Same"), ("Size", "48")));

        Assert.False(succeeded);
        Assert.Contains(engine.Errors, error => error.Contains("Set a unique Name", StringComparison.Ordinal));
    }

    [Fact]
    public void FindManifestEntries_ListsTheDescriptorAndEachPage()
    {
        using var directory = new TestDirectory();
        directory.WriteFile("out/Content/big_0.ktx2", "x");
        directory.WriteFile("out/Content/big_1.ktx2", "x");
        var task = new _FindPagedContentManifestEntries
        {
            BuildEngine = new RecordingBuildEngine(),
            ContentOutputDir = directory.Path_("out"),
            NameMetadata = "FontName",
            DescriptorExtension = ".spritefont",
            Items = [TaskItems.Item("big.ttf", ("FontName", "big"), ("_OutputExtension", ".ktx2"), ("OutputRelativeDir", "Content\\"), ("_PremultiplyAlpha", "false"))],
        };

        Assert.True(task.Execute());

        // An item normalizes its own separators for the platform
        Assert.Equal(["Content/big", "Content/big_0", "Content/big_1"], task.Entries.Select(entry => entry.ItemSpec.Replace('\\', '/')));
        Assert.Equal("Content/big.spritefont", task.Entries[0].GetMetadata("RelativePath").Replace('\\', '/'));
        Assert.Equal("false", task.Entries[1].GetMetadata("PremultipliedAlpha"));
    }

    [Fact]
    public void FindManifestEntries_ListsAnAtlasOnce_WithEveryPage()
    {
        using var directory = new TestDirectory();
        directory.WriteFile("out/Sprites/sprites_0.webp", "x");
        directory.WriteFile("out/Sprites/sprites_1.webp", "x");
        var task = new _FindPagedContentManifestEntries
        {
            BuildEngine = new RecordingBuildEngine(),
            ContentOutputDir = directory.Path_("out"),
            NameMetadata = "Name",
            DescriptorExtension = ".json",
            Items =
            [
                TaskItems.Item("a.png", ("Name", "sprites"), ("_OutputExtension", ".webp"), ("OutputRelativeDir", "Sprites\\")),
                TaskItems.Item("b.png", ("Name", "sprites"), ("_OutputExtension", ".webp"), ("OutputRelativeDir", "Sprites\\")),
            ],
        };

        Assert.True(task.Execute());

        Assert.Equal(["Sprites/sprites", "Sprites/sprites_0", "Sprites/sprites_1"], task.Entries.Select(entry => entry.ItemSpec.Replace('\\', '/')));
        Assert.Equal("Sprites/sprites.json", task.Entries[0].GetMetadata("RelativePath").Replace('\\', '/'));
        Assert.Equal("Sprites/sprites_1.webp", task.Entries[2].GetMetadata("RelativePath").Replace('\\', '/'));
        Assert.Equal(string.Empty, task.Entries[1].GetMetadata("PremultipliedAlpha"));
    }
}

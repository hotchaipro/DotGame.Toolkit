// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text.Json;
using DotGame.ContentPipeline.Testing;
using DotGame.Framework.ContentPipeline.BuildTool.Audio;
using DotGame.Framework.ContentPipeline.BuildTool.Graphics;
using DotGame.Framework.Platform.Common.Content;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Integration;

/// <summary>
/// Toolkit content with the real commands, against golden hashes: see the framework's RealToolTests.
/// </summary>
[Trait(Traits.Category, Traits.Slow)]
public sealed class RealToolkitToolTests
{
    private static string Project => ToolkitPaths.Tests;

    private static ContentProject RealProject()
    {
        var project = new ContentProject { ImportLibrary = true };
        foreach (string property in new[] { "TextureContentLosslessProcessorCommand", "TextureContentNearLosslessProcessorCommand", "TextureContentLossyProcessorCommand", "ShaderContentProcessorCommand", "SoundContentFlacProcessorCommand", "SoundContentWavProcessorCommand", "SoundContentLossyProcessorCommand", "MusicContentProcessorCommand" })
        {
            project.Properties.Remove(property);
        }

        project.Properties["ContentPipelineBuildTool"] = Path.Combine(AppContext.BaseDirectory, "DotGame.Framework.ContentPipeline.BuildTool.dll");
        return project;
    }

    private static string RuntimeId => $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}";

    [Fact]
    public void Atlas_OnDesktop_NeedsNoExternalTool_AndMatchesTheGolden()
    {
        using var project = RealProject();
        project.Properties["TextureAtlasMaxWidth"] = "256";
        project.Properties["TextureAtlasMaxHeight"] = "256";
        foreach ((string name, RgbaImage image) in new[] { ("a", Images.Sample(9, 7)), ("b", Images.Sample(4, 11)), ("c", Images.Rectangle(6, 6, 200, 100, 50, left: 1, top: 1, right: 5, bottom: 4)) })
        {
            string path = Path.Combine(project.ProjectDir, "sprites", name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            image.SavePng(path);
            project.AddItem("TextureAtlasContent", $"sprites/{name}.png", $"Content\\{name}.png").With("Name", "ui");
        }

        project.Build().ShouldSucceed();

        Ktx2Texture page = Ktx2Texture.Read(File.ReadAllBytes(project.OutputPath("Content/ui_0.ktx2")), "page");
        Assert.True(page.Premultiplied);
        string identity = $"{ContentVersions.Of("ToolkitContentPipelineTextureAtlasVersion")}/{ContentVersions.Of("ContentPipelineTextureVersion")}";
        Goldens.Check(Project, "atlas.ktx2.pixels", identity, page.Data.ToArray());
        Goldens.Check(Project, "atlas.ktx2.descriptor", identity, File.ReadAllBytes(project.OutputPath("Content/ui.json")));

        // A real restore
        Dictionary<string, string> hashes = project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash);
        project.DeleteBuildOutputs();
        project.Build().ShouldSucceed();
        Assert.Equal(hashes, project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash));
    }

    [Fact]
    public void Bank_NeedsNoExternalTool_AndPlacesEachSoundAtItsOffset()
    {
        using var project = RealProject();
        project.Properties["FFmpegExe"] = "no-such-ffmpeg";
        Directory.CreateDirectory(Path.Combine(project.ProjectDir, "sounds"));

        // Tones of known length: a quarter second, then half a second
        foreach ((string name, int frames) in new[] { ("a", 11025), ("b", 22050) })
        {
            Tones.WriteWav(Path.Combine(project.ProjectDir, "sounds", name + ".wav"), Tones.Sine(44100, 1, frames));
            project.AddItem("SoundBankContent", $"sounds/{name}.wav", $"Content\\{name}.wav").With("Name", "sfx");
        }

        project.Build().ShouldSucceed();

        JsonElement sounds = JsonElement.Parse(project.ReadOutput("Content/sfx.json")).GetProperty("sounds");
        Assert.Equal(0, sounds.GetProperty("a").GetProperty("offset").GetDouble());
        Assert.Equal(0.25, sounds.GetProperty("a").GetProperty("duration").GetDouble(), 3);
        double offset = sounds.GetProperty("b").GetProperty("offset").GetDouble();
        Assert.Equal(0.35, offset, 3);

        // The file's length agrees with the descriptor: PCM has no encoder delay
        Assert.Equal(offset + 0.5, AudioReader.Read(project.OutputPath("Content/sfx_0.flac")).Frames / 44100.0, 3);
    }

    [Fact]
    public void LossyBank_OnTheWeb_IsOpusAt48Kilohertz_WithItsOffsetsExact()
    {
        // Opus decodes at 48 kHz, so a lossy web bank is built at that rate: each sound starts on
        // the sample its offset names once decoded (Ogg's pre-skip is the decoder's to drop)
        string ffmpeg = RealTools.Require("ffmpeg");
        using var project = RealProject();
        project.Properties["ContentPipelinePlatform"] = "webgl";
        project.Properties["FFmpegExe"] = ffmpeg;
        Directory.CreateDirectory(Path.Combine(project.ProjectDir, "sounds"));
        foreach ((string name, string duration) in new[] { ("a", "0.25"), ("b", "0.5") })
        {
            (int exitCode, string made) = ProcessRunner.Run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"sine=frequency=440:sample_rate=44100:duration={duration}", "-ac", "1", Path.Combine(project.ProjectDir, "sounds", name + ".wav")]);
            Assert.True(exitCode == 0, made);
            project.AddItem("SoundBankContent", $"sounds/{name}.wav", $"Content\\{name}.wav").With("Name", "sfx").With("Compression", "Lossy");
        }

        project.Build().ShouldSucceed();

        string audio = Assert.Single(project.OutputFiles(), file => file.EndsWith("sfx_0.opus", StringComparison.Ordinal));
        string json = Assert.Single(project.OutputFiles(), file => file.EndsWith("sfx.json", StringComparison.Ordinal));
        JsonElement sounds = JsonElement.Parse(File.ReadAllText(project.OutputPath(json))).GetProperty("sounds");
        double offset = sounds.GetProperty("b").GetProperty("offset").GetDouble();
        Assert.Equal(0.35, offset, 1.0 / 48000);
        Assert.Equal(Math.Round(offset * 48000), offset * 48000, 1e-6);

        string raw = project.OutputPath(audio) + ".raw";
        (int decoded, string report) = ProcessRunner.Run(ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-i", project.OutputPath(audio), "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "48000", raw]);
        Assert.True(decoded == 0, report);
        byte[] pcm = File.ReadAllBytes(raw);
        short Left(int frame) => BitConverter.ToInt16(pcm, frame * 4);

        // The tone is 4,096 at its peak, and passes 300 on its second sample; the gap before is quiet
        int expected = (int)Math.Round(offset * 48000);
        int onset = (int)Math.Round((0.25 + 0.02) * 48000);
        while (Math.Abs((int)Left(onset)) < 300)
        {
            onset++;
        }
        Assert.InRange(onset - expected, 0, 8);
        File.Delete(raw);
    }

    [Fact]
    public void Font_WithRealTools_MatchesTheGolden()
    {
        string ffmpeg = RealTools.Require("ffmpeg");
        string slangc = RealTools.Require("slangc");
        string spirvCross = RealTools.Require("spirv-cross");
        string spirv2dxil = RealTools.Spirv2Dxil();
        using var project = RealProject();
        project.Properties["FFmpegExe"] = ffmpeg;
        project.Properties["SlangCExe"] = slangc;
        project.Properties["SpirvCrossExe"] = spirvCross;
        project.Properties["Spirv2DxilExe"] = spirv2dxil;
        string font = Path.Combine(ToolkitPaths.Root, "sample", "Content", "Fonts", "Inter-Medium.ttf");
        project.AddItem("SpriteFontContent", font, "Content\\Fonts\\Inter-Medium.ttf")
            .With("Name", "Body")
            .With("Size", "24")
            .With("Charset", "AVab019")
            .With("Kerning", "true");

        project.Build().ShouldSucceed();

        // The glyphs are floating-point work by a per-platform binary: a golden per platform
        string identity = $"{ContentVersions.Of("ToolkitContentPipelineSpriteFontVersion")}+{RuntimeId}+{RealTools.Identity(GlyphGen())}";
        Goldens.Check(Project, "font.spritefont", identity, File.ReadAllBytes(project.OutputPath("Content/Fonts/Body.spritefont")));
        Goldens.Check(Project, "font.page.pixels", identity, Ktx2Texture.Read(File.ReadAllBytes(project.OutputPath("Content/Fonts/Body_0.ktx2")), "page").Data.ToArray());
        Assert.Contains("Content/_toolkit/Shaders/spritefont.frag.dxil", project.OutputFiles());
    }

    private static string GlyphGen()
    {
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
        return Path.Combine(ToolkitPaths.Root, "native", "glyphgen", $"{os}-{architecture}", OperatingSystem.IsWindows() ? "glyphgen.exe" : "glyphgen");
    }
}

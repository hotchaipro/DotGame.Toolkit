// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text.Json;
using DotGame.ContentPipeline.Testing;
using DotGame.Toolkit.Fonts;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Integration;

/// <summary>
/// Sound banks and fonts through the real targets and build tool, with fake encoders; fonts also
/// use the real glyphgen, which ships in the repository, and the sample's font.
/// </summary>
[Trait(Traits.Category, Traits.Slow)]
public sealed class BankAndFontBuildTests
{
    private static readonly string SampleFont = Path.Combine(ToolkitPaths.Root, "sample", "Content", "Fonts", "Inter-Medium.ttf");

    private static ContentProject Project()
    {
        var project = new ContentProject { ImportLibrary = true };
        return project;
    }

    private static ContentItem AddSound(ContentProject project, string name, string bank = "sfx")
    {
        project.WriteSound($"sounds/{name}.wav", name.Length + name[0]);
        return project.AddItem("SoundBankContent", $"sounds/{name}.wav", $"Content\\{name}.wav").With("Name", bank);
    }

    private static ContentItem AddFont(ContentProject project, string name = "Body")
    {
        return project.AddItem("SpriteFontContent", SampleFont, "Content\\Fonts\\Inter-Medium.ttf")
            .With("Name", name)
            .With("Size", "16")
            .With("Charset", "AVab");
    }

    [Fact]
    public void Font_MarkedPreload_ReachesTheWebPagesPreloads_WithTheTextShader()
    {
        using var project = Project();
        AddFont(project, "Body").With("Preload", "true");
        AddFont(project, "Other");
        project.Properties["StaticWebAssetsSdkBuildTasksAssembly"] = "present";
        project.ExtraProjectXml = """
            <Target Name="WritePreloads" DependsOnTargets="_PrepareContentItems;_PrepareSpriteFontContentItems;_AddToolkitWebPreloadContent">
              <WriteLinesToFile File="$(MSBuildProjectDirectory)/preloads.txt" Lines="@(_WebPreloadContent)" Overwrite="true" />
            </Target>
            """;

        project.Build("WritePreloads").ShouldSucceed();

        string[] preloads = File.ReadAllLines(Path.Combine(project.ProjectDir, "preloads.txt"));
        var stems = preloads.Select(static line => line.Replace('\\', '/')).ToArray();
        Assert.Contains("Content/Fonts/Body", stems);
        Assert.DoesNotContain("Content/Fonts/Other", stems);
        Assert.Contains(stems, static stem => stem.Contains("_toolkit/Shaders/spritefont", StringComparison.Ordinal));
    }

    [Fact]
    public void Bank_CleanBuild_WritesTheDescriptorAudioAndManifestEntries()
    {
        using var project = Project();
        AddSound(project, "hit");
        AddSound(project, "miss");

        project.Build().ShouldSucceed();

        Assert.Equal(["Content/sfx.json", "Content/sfx_0.flac"], project.OutputFiles());
        JsonElement descriptor = JsonElement.Parse(project.ReadOutput("Content/sfx.json"));
        Assert.Equal("sfx_0", descriptor.GetProperty("name").GetString());
        Assert.Equal(0.2, descriptor.GetProperty("sounds").GetProperty("miss").GetProperty("offset").GetDouble(), 3);
        List<ManifestEntry> entries = project.Manifest();
        Assert.Equal("sfx.json", Assert.Single(entries, entry => entry.Key == "sfx").Path);
        Assert.Equal("sfx_0.flac", Assert.Single(entries, entry => entry.Key == "sfx_0").Path);
    }

    [Fact]
    public void Bank_LosslessFormatWav_WritesAWavBank()
    {
        using var project = Project();
        AddSound(project, "hit").With("LosslessFormat", "WAV");
        AddSound(project, "miss").With("LosslessFormat", "WAV");
        AddSound(project, "tap", "ui");

        project.Build().ShouldSucceed();

        Assert.Equal(["Content/sfx.json", "Content/sfx_0.wav", "Content/ui.json", "Content/ui_0.flac"], project.OutputFiles());
        Assert.Equal("sfx_0.wav", Assert.Single(project.Manifest(), entry => entry.Key == "sfx_0").Path);
    }

    [Fact]
    public void Bank_WithNoName_IsCalledSoundbank()
    {
        using var project = Project();
        project.WriteSound("sounds/hit.wav", 3);
        project.AddItem("SoundBankContent", "sounds/hit.wav", "Content\\hit.wav");

        project.Build().ShouldSucceed();

        Assert.Equal(["Content/soundbank.json", "Content/soundbank_0.flac"], project.OutputFiles());
    }

    [Fact]
    public void Bank_NoOpBuild_RunsNothing()
    {
        using var project = Project();
        AddSound(project, "hit");
        project.Build().ShouldSucceed();

        Assert.Empty(project.Build().ShouldSucceed().Calls);
    }

    [Fact]
    public void Bank_ChangedSound_RemovedSound_AndChangedCompression_Rebuild()
    {
        using var project = Project();
        ContentItem hit = AddSound(project, "hit");
        ContentItem miss = AddSound(project, "miss");
        project.Build().ShouldSucceed();

        project.WriteSound("sounds/hit.wav", 999);
        Assert.Single(project.Build().ShouldSucceed().CallsOf("sound"));

        project.RemoveItem(miss);
        project.Build().ShouldSucceed();
        Assert.False(JsonElement.Parse(project.ReadOutput("Content/sfx.json")).GetProperty("sounds").TryGetProperty("miss", out _));

        hit.With("Compression", "Lossy");
        BuildResult lossy = project.Build().ShouldSucceed();
        Assert.Contains("--lossy", Assert.Single(lossy.CallsOf("sound")).Arguments, StringComparison.Ordinal);
        Assert.Equal(["Content/sfx.json", "Content/sfx_0.opus"], project.OutputFiles());
    }

    [Fact]
    public void Bank_CleanBuild_RestoresFromTheCache_ByteIdentical()
    {
        using var project = Project();
        AddSound(project, "hit");
        AddSound(project, "miss");
        project.Build().ShouldSucceed();
        Dictionary<string, string> hashes = project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash);

        project.DeleteBuildOutputs();
        BuildResult restored = project.Build().ShouldSucceed();

        Assert.Empty(restored.Calls);
        Assert.Equal(hashes, project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash));
    }

    [Fact]
    public void Bank_FailedEncoder_FailsTheNextBuildToo()
    {
        using var project = Project();
        AddSound(project, "hit");
        project.SetToolConfig("fail.sound", "partial");

        project.Build().ShouldFail();
        project.Build().ShouldFail();
    }

    [Fact]
    public void Bank_EmptyName_FailsTheBuild()
    {
        using var project = Project();
        project.WriteSound("sounds/hit.wav", 3);
        project.AddItem("SoundBankContent", "sounds/hit.wav", "Content\\hit.wav").With("Name", "");

        Assert.Contains("empty Name", project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Bank_Lossy_OnDesktop_IsAFormatDesktopCanLoad()
    {
        using var project = Project();
        AddSound(project, "hit").With("Compression", "Lossy");

        project.Build().ShouldSucceed();

        string audio = Assert.Single(project.OutputFiles(), file => file.Contains("sfx_0", StringComparison.Ordinal));
        string loader = File.ReadAllText(Path.Combine(Paths.FrameworkRoot, "platform", "desktop", "Content", "SoundLoader.cs"));
        Assert.True(
            audio.EndsWith(".wav", StringComparison.Ordinal) || (audio.EndsWith(".opus", StringComparison.Ordinal) && loader.Contains("SoundDecoder", StringComparison.Ordinal)),
            $"The bank's audio is '{audio}', which the desktop loader doesn't read.");
    }

    [Fact]
    public void Font_CleanBuild_WritesAReadableDescriptorPagesAndItsShader()
    {
        using var project = Project();
        AddFont(project);

        BuildResult build = project.Build().ShouldSucceed();

        Assert.Contains("Content/Fonts/Body.spritefont", project.OutputFiles());
        Assert.Contains("Content/Fonts/Body_0.ktx2", project.OutputFiles());
        Assert.Contains("Content/_toolkit/Shaders/spritefont.frag.spv", project.OutputFiles());
        SpriteFontData font = SpriteFontReader.Read(File.ReadAllBytes(project.OutputPath("Content/Fonts/Body.spritefont")));
        Assert.Equal(4, font.GlyphCount);
        Assert.Equal(16f, font.EmSize);

        // A distance field is data, so its page isn't premultiplied
        Assert.Contains("--premultiply false", Assert.Single(build.CallsOf("texture")).Arguments, StringComparison.Ordinal);
        List<ManifestEntry> entries = project.Manifest();
        Assert.Equal("Fonts/Body.spritefont", Assert.Single(entries, entry => entry.Key == "Fonts/Body").Path);
        Assert.Contains("\"PremultipliedAlpha\", \"false\"", Assert.Single(entries, entry => entry.Key == "Fonts/Body_0").Properties, StringComparison.Ordinal);
    }

    [Fact]
    public void Font_NoOpBuild_RunsNothing()
    {
        using var project = Project();
        AddFont(project);
        project.Build().ShouldSucceed();

        Assert.Empty(project.Build().ShouldSucceed().Calls);
    }

    [Theory]
    [InlineData("Size", "20")]
    [InlineData("Charset", "AVabc")]
    [InlineData("GlyphFormat", "Sdf")]
    [InlineData("Kerning", "true")]
    [InlineData("PxRange", "3")]
    public void Font_ChangedSetting_RebuildsTheFont(string name, string value)
    {
        using var project = Project();
        ContentItem font = AddFont(project);
        project.Build().ShouldSucceed();

        font.With(name, value);

        Assert.Single(project.Build().ShouldSucceed().CallsOf("texture"));
    }

    [Fact]
    public void Font_WithKerning_WritesThePairs()
    {
        using var project = Project();
        AddFont(project).With("Kerning", "true");

        project.Build().ShouldSucceed();

        SpriteFontData font = SpriteFontReader.Read(File.ReadAllBytes(project.OutputPath("Content/Fonts/Body.spritefont")));
        Assert.True(font.KerningCount > 0, "The sample font kerns A and V.");
    }

    [Fact]
    public void Font_CleanBuild_RestoresFromTheCache_ByteIdentical()
    {
        using var project = Project();
        AddFont(project);
        project.Build().ShouldSucceed();
        Dictionary<string, string> hashes = project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash);

        project.DeleteBuildOutputs();
        BuildResult restored = project.Build().ShouldSucceed();

        Assert.Empty(restored.Calls);
        Assert.Equal(hashes, project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash));
    }

    [Fact]
    public void Font_FailedEncoder_FailsTheNextBuildToo()
    {
        using var project = Project();
        AddFont(project);
        project.SetToolConfig("fail.texture", "partial");

        project.Build().ShouldFail();
        project.Build().ShouldFail();
    }

    [Fact]
    public void Font_Removed_LosesItsOutputs_ButKeepsOthers()
    {
        using var project = Project();
        ContentItem body = AddFont(project, "Body");
        AddFont(project, "Title").With("Size", "24");
        project.Build().ShouldSucceed();

        project.RemoveItem(body);
        project.Build().ShouldSucceed();

        Assert.DoesNotContain(project.OutputFiles(), file => file.Contains("Fonts/Body", StringComparison.Ordinal));
        Assert.Contains("Content/Fonts/Title.spritefont", project.OutputFiles());
    }

    [Fact]
    public void Font_TwoWithOneName_FailTheBuild()
    {
        using var project = Project();
        AddFont(project, "Same");
        AddFont(project, "Same").With("Size", "24");

        Assert.Contains("Set a unique Name", project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Font_WithNoneOfItsGlyphs_FailsTheBuild()
    {
        using var project = Project();
        AddFont(project).With("Charset", "一");

        project.Build().ShouldFail();
    }

    [Fact]
    public void Font_MissingSomeGlyphs_Warns()
    {
        using var project = Project();
        AddFont(project).With("Charset", "A一");

        BuildResult build = project.Build().ShouldSucceed();

        Assert.Contains("FNT0002", build.Output, StringComparison.Ordinal);
    }
}

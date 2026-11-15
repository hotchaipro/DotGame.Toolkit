// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text.Json;
using DotGame.ContentPipeline.Testing;
using DotGame.Framework.ContentPipeline.BuildTool.Graphics;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Integration;

/// <summary>
/// Atlases through the real targets and the real packer, with a fake page encoder.
/// </summary>
[Trait(Traits.Category, Traits.Slow)]
public sealed class AtlasBuildTests
{
    private static ContentProject Project()
    {
        var project = new ContentProject { ImportLibrary = true };
        project.Properties["TextureAtlasMaxWidth"] = "256";
        project.Properties["TextureAtlasMaxHeight"] = "256";
        return project;
    }

    private static void Sprite(ContentProject project, string atlas, string name, RgbaImage image)
    {
        string path = Path.Combine(project.ProjectDir, "sprites", atlas, name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        image.SavePng(path);
    }

    private static ContentItem AddSprite(ContentProject project, string name, RgbaImage? image = null, string atlas = "ui")
    {
        Sprite(project, atlas, name, image ?? Images.Rectangle(8, 8, (byte)name[0], 1, 1));
        return project.AddItem("TextureAtlasContent", $"sprites/{atlas}/{name}.png", $"Content\\{name}.png").With("Name", atlas);
    }

    private static JsonElement Descriptor(ContentProject project, string atlas = "ui") =>
        JsonElement.Parse(project.ReadOutput($"Content/{atlas}.json"));

    private static List<string> Regions(JsonElement descriptor) =>
        [.. descriptor.GetProperty("textures").EnumerateArray().SelectMany(texture => texture.GetProperty("regions").EnumerateObject()).Select(region => region.Name)];

    [Fact]
    public void Atlas_WithNoName_IsCalledSpritesheet()
    {
        using var project = new ContentProject { ImportLibrary = true };
        Sprite(project, "ui", "a", Images.Rectangle(8, 8, 97, 1, 1));
        project.AddItem("TextureAtlasContent", "sprites/ui/a.png", "Content\\a.png");

        project.Build().ShouldSucceed();

        Assert.Contains("Content/spritesheet.json", project.OutputFiles());
    }

    [Fact]
    public void GpuCompression_EncodesPagesWithThePlatformsGpuCommand_WithBlockPadding()
    {
        using var project = Project();
        project.Properties["TextureContentGpuProcessorCommand"] = project.Properties["TextureContentLossyProcessorCommand"] + " --gpu";
        project.Properties["TextureContentGpuProcessorOutputExtension"] = ".astc.ktx2";
        AddSprite(project, "a").With("GpuCompression", "true");

        BuildResult build = project.Build().ShouldSucceed();

        Assert.Contains("Content/ui_0.astc.ktx2", project.OutputFiles());
        Assert.Contains(build.Calls, call => call.Arguments.Contains("--gpu", StringComparison.Ordinal));
        string options = Path.Combine(project.ProjectDir, "obj", "ContentPipeline", "Intermediate", "Content", "ui.packing.options");
        Assert.Contains("padding=4", File.ReadAllText(options), StringComparison.Ordinal);
    }

    [Fact]
    public void GpuCompression_WithLossless_IsAnError()
    {
        using var project = Project();
        AddSprite(project, "a").With("GpuCompression", "true").With("Compression", "Lossless");

        project.Build().ShouldFail();
    }

    [Fact]
    public void CleanBuild_WritesTheDescriptorPagesAndManifestEntries()
    {
        using var project = Project();
        AddSprite(project, "a");
        AddSprite(project, "b");

        project.Build().ShouldSucceed();

        Assert.Equal(["Content/ui.json", "Content/ui_0.ktx2"], project.OutputFiles());
        Assert.Equal(["a", "b"], Regions(Descriptor(project)));
        List<ManifestEntry> entries = project.Manifest();
        Assert.Equal("ui.json", Assert.Single(entries, entry => entry.Key == "ui").Path);
        Assert.Equal("ui_0.ktx2", Assert.Single(entries, entry => entry.Key == "ui_0").Path);
    }

    [Fact]
    public void NoOpBuild_RunsNothing()
    {
        using var project = Project();
        AddSprite(project, "a");
        project.Build().ShouldSucceed();
        var outputs = project.SnapshotOutputs();

        BuildResult second = project.Build().ShouldSucceed();

        Assert.Empty(second.Calls);
        Assert.Equal(outputs, project.SnapshotOutputs());
    }

    [Fact]
    public void ChangedSprite_RebuildsTheAtlas()
    {
        using var project = Project();
        AddSprite(project, "a");
        project.Build().ShouldSucceed();

        Sprite(project, "ui", "a", Images.Rectangle(9, 9, 200, 1, 1));

        Assert.Single(project.Build().ShouldSucceed().CallsOf("texture"));
    }

    [Fact]
    public void AddedSprite_WithAnOldTimestamp_RebuildsTheAtlas()
    {
        using var project = Project();
        AddSprite(project, "a");
        project.Build().ShouldSucceed();

        AddSprite(project, "b");
        File.SetLastWriteTimeUtc(Path.Combine(project.ProjectDir, "sprites", "ui", "b.png"), DateTime.UtcNow.AddYears(-1));
        project.Build().ShouldSucceed();

        Assert.Equal(["a", "b"], Regions(Descriptor(project)));
    }

    [Fact]
    public void RemovedSprite_RebuildsTheAtlas()
    {
        using var project = Project();
        AddSprite(project, "a");
        ContentItem b = AddSprite(project, "b");
        project.Build().ShouldSucceed();

        project.RemoveItem(b);
        project.Build().ShouldSucceed();

        Assert.Equal(["a"], Regions(Descriptor(project)));
    }

    [Fact]
    public void ChangedQuality_RebuildsTheAtlas()
    {
        using var project = Project();
        ContentItem sprite = AddSprite(project, "a").With("Compression", "Lossy").With("LossyQuality", "5");
        project.Build().ShouldSucceed();

        sprite.With("LossyQuality", "6");

        Assert.Contains("--quality 6", Assert.Single(project.Build().ShouldSucceed().CallsOf("texture")).Arguments, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TextureAtlasPadding", "4")]
    [InlineData("TextureAtlasExtrude", "3")]
    [InlineData("TextureAtlasMaxWidth", "128")]
    [InlineData("TextureAtlasMaxHeight", "128")]
    [InlineData("TextureAtlasPowerOfTwo", "true")]
    [InlineData("TextureAtlasSquare", "true")]
    public void ChangedPackingOption_RebuildsTheAtlas(string property, string value)
    {
        using var project = Project();
        AddSprite(project, "a");
        project.Build().ShouldSucceed();

        project.Properties[property] = value;

        Assert.Single(project.Build().ShouldSucceed().CallsOf("texture"));
    }

    [Fact]
    public void CleanBuild_RestoresFromTheCache_ByteIdentical_WithNoEncoder()
    {
        using var project = Project();
        AddSprite(project, "a");
        AddSprite(project, "b");
        project.Build().ShouldSucceed();
        Dictionary<string, string> hashes = project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash);

        project.DeleteBuildOutputs();
        BuildResult restored = project.Build().ShouldSucceed();

        Assert.Empty(restored.Calls);
        Assert.Contains("[cached]", restored.Output, StringComparison.Ordinal);
        Assert.Equal(hashes, project.SnapshotOutputs().ToDictionary(file => file.Key, file => file.Value.Hash));
    }

    [Fact]
    public void ShrinkingToFewerPages_RemovesTheOldPages()
    {
        using var project = Project();
        project.Properties["TextureAtlasMaxWidth"] = "32";
        project.Properties["TextureAtlasMaxHeight"] = "32";
        project.Properties["TextureAtlasExtrude"] = "0";
        AddSprite(project, "a", Images.Rectangle(30, 30, 1, 0, 0));
        ContentItem b = AddSprite(project, "b", Images.Rectangle(30, 30, 2, 0, 0));
        project.Build().ShouldSucceed();
        Assert.Contains("Content/ui_1.ktx2", project.OutputFiles());

        project.RemoveItem(b);
        project.Build().ShouldSucceed();

        Assert.Equal(["Content/ui.json", "Content/ui_0.ktx2"], project.OutputFiles());
    }

    [Fact]
    public void TwoAtlases_KeepTheirOwnSprites()
    {
        using var project = Project();
        AddSprite(project, "a", atlas: "first");
        AddSprite(project, "b", atlas: "second");

        project.Build().ShouldSucceed();

        Assert.Equal(["a"], Regions(Descriptor(project, "first")));
        Assert.Equal(["b"], Regions(Descriptor(project, "second")));
    }

    [Fact]
    public void ChangingOneAtlas_LeavesTheOtherAlone()
    {
        using var project = Project();
        AddSprite(project, "a", atlas: "first");
        AddSprite(project, "b", atlas: "second");
        project.Build().ShouldSucceed();

        Sprite(project, "first", "a", Images.Rectangle(9, 9, 200, 1, 1));

        Assert.Single(project.Build().ShouldSucceed().CallsOf("texture"));
    }

    [Fact]
    public void FailedEncoder_FailsTheNextBuildToo_AndReachesNoCache()
    {
        // The descriptor is written last, so a composite gets this right
        using var project = Project();
        AddSprite(project, "a");
        project.SetToolConfig("fail.texture", "partial");

        project.Build().ShouldFail();
        project.Build().ShouldFail();

        Assert.False(File.Exists(project.OutputPath("Content/ui.json")));
        project.SetToolConfig("fail.texture", null);
        project.Build().ShouldSucceed();
        Assert.StartsWith("FAKE", project.ReadOutput("Content/ui_0.ktx2"), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyName_FailsTheBuild()
    {
        using var project = Project();
        Sprite(project, "ui", "a", Images.Rectangle(4, 4, 1, 1, 1));
        project.AddItem("TextureAtlasContent", "sprites/ui/a.png", "Content\\a.png").With("Name", "");

        Assert.Contains("empty Name", project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidCompression_FailsTheBuild()
    {
        using var project = Project();
        AddSprite(project, "a").With("Compression", "Bogus");

        Assert.Contains("invalid Compression", project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "LossyQuality", "6", "sets the other compression's quality")]
    [InlineData("Lossy", "LosslessQuality", "6", "sets the other compression's quality")]
    [InlineData(null, "LosslessQuality", "0", "invalid LosslessQuality")]
    public void InvalidQuality_FailsTheBuild(string? compression, string name, string value, string message)
    {
        using var project = Project();
        ContentItem sprite = AddSprite(project, "a").With(name, value);
        if (compression is not null)
        {
            sprite.With("Compression", compression);
        }

        Assert.Contains(message, project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    // Below 10, a lossless atlas goes through the encoder that may change colors a little, with its quality
    [Theory]
    [InlineData(null, "--compression Lossless --premultiply")]
    [InlineData("6", "--compression Lossless --quality 6 --premultiply")]
    public void LosslessQuality_PicksTheLosslessEncoding(string? quality, string arguments)
    {
        using var project = Project();
        ContentItem sprite = AddSprite(project, "a");
        if (quality is not null)
        {
            sprite.With("LosslessQuality", quality);
        }

        Assert.Contains(arguments, Assert.Single(project.Build().ShouldSucceed().CallsOf("texture")).Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateRegionNames_FailTheBuild()
    {
        using var project = Project();
        AddSprite(project, "a", atlas: "ui");
        Sprite(project, "other", "A", Images.Rectangle(8, 8, 9, 9, 9));
        project.AddItem("TextureAtlasContent", "sprites/other/A.png", "Content\\A.png").With("Name", "ui");

        Assert.Contains("region name", project.Build().ShouldFail().Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AwkwardProjectFolder_Builds()
    {
        using var project = new ContentProject("My Jeu été 日本語") { ImportLibrary = true };
        project.Properties["TextureAtlasMaxWidth"] = "256";
        project.Properties["TextureAtlasMaxHeight"] = "256";
        AddSprite(project, "a");

        project.Build().ShouldSucceed();

        Assert.Equal(["a"], Regions(Descriptor(project)));
    }

    [Fact]
    public void PlainTextureWithItsAtlasKey_IsAnError()
    {
        // Both would be the key "ui", and the game would load whichever the manifest kept last
        using var project = Project();
        AddSprite(project, "a");
        project.AddContent("TextureContent", "ui", ".png");

        BuildResult build = project.Build().ShouldFail();

        Assert.Contains("Two content items have the key 'ui'", build.Output, StringComparison.Ordinal);
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text.Json;
using DotGame.ContentPipeline.Testing;
using DotGame.Framework.ContentPipeline.BuildTool;
using DotGame.Framework.ContentPipeline.BuildTool.Graphics;
using DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Graphics;

/// <summary>
/// The atlas command in process, with the fake tool as the page encoder: the descriptor, and the
/// pixels of the pages it hands the encoder. The page limit is always given: the default is for the
/// targets to choose.
/// </summary>
public sealed class TextureAtlasCommandTests : IDisposable
{
    private readonly TestDirectory _directory = new();
    private readonly List<string> _sprites = [];

    public void Dispose() => this._directory.Dispose();

    private void Sprite(string name, RgbaImage image)
    {
        string path = this._directory.Path_("src", name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        image.SavePng(path);
        this._sprites.Add(path);
    }

    private JsonElement Build(int maxWidth = 256, int maxHeight = 256, int padding = 0, int extrude = 0, params string[] switches)
    {
        string list = this._directory.WriteFile("obj/atlas.inputs.txt", string.Join(';', this._sprites));
        string template = this._directory.WriteFile("obj/atlas.encode.cmd", $"\"{Paths.FakeTool}\" texture --input \"{{InputFile}}\" --output \"{{OutputFile}}\"");
        string output = this._directory.Path_("out", "atlas.json");
        TextureAtlasCommand.Run(CommandLineOptions.Parse([
            "--input-list", list,
            "--output", output,
            "--texture-output-base", this._directory.Path_("out", "atlas"),
            "--texture-output-extension", ".ktx2",
            "--encode-command-file", template,
            "--intermediate-dir", this._directory.Path_("obj"),
            "--padding", padding.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--extrude", extrude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--max-width", maxWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--max-height", maxHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
            .. switches,
        ]));
        return JsonElement.Parse(File.ReadAllText(output));
    }

    private RgbaImage Page(int index = 0) => RgbaImage.Load(this._directory.Path_("obj", $"atlas_{index}.png"));

    private static JsonElement Region(JsonElement atlas, string name, int page = 0) => atlas.GetProperty("textures")[page].GetProperty("regions").GetProperty(name);

    private static (int X, int Y, int W, int H) Rect(JsonElement region, string name = "frame")
    {
        JsonElement rect = region.GetProperty(name);
        return (rect.GetProperty("x").GetInt32(), rect.GetProperty("y").GetInt32(), rect.GetProperty("w").GetInt32(), rect.GetProperty("h").GetInt32());
    }

    private static byte[] Pixel(RgbaImage image, int x, int y) => image.Pixels.AsSpan((y * image.Stride) + (x * 4), 4).ToArray();

    [Fact]
    public void Descriptor_NamesThePage_AndItsRegions_Sorted()
    {
        this.Sprite("zebra", Images.Rectangle(4, 4, 1, 1, 1));
        this.Sprite("Apple", Images.Rectangle(4, 4, 2, 2, 2));
        this.Sprite("mango", Images.Rectangle(4, 4, 3, 3, 3));

        JsonElement atlas = this.Build();

        JsonElement texture = Assert.Single(atlas.GetProperty("textures").EnumerateArray());
        Assert.Equal("atlas_0.ktx2", texture.GetProperty("name").GetString());
        Assert.Equal(["Apple", "mango", "zebra"], texture.GetProperty("regions").EnumerateObject().Select(region => region.Name));
    }

    [Fact]
    public void Frame_PointsAtTheSpritesPixels()
    {
        this.Sprite("red", Images.Rectangle(4, 3, 200, 10, 20));
        this.Sprite("blue", Images.Rectangle(5, 2, 30, 40, 250));

        JsonElement atlas = this.Build();
        RgbaImage page = this.Page();

        (int x, int y, int w, int h) = Rect(Region(atlas, "red"));
        Assert.Equal((4, 3), (w, h));
        Assert.Equal([200, 10, 20, 255], Pixel(page, x, y));
        Assert.Equal([200, 10, 20, 255], Pixel(page, x + w - 1, y + h - 1));

        (x, y, _, _) = Rect(Region(atlas, "blue"));
        Assert.Equal([30, 40, 250, 255], Pixel(page, x, y));
    }

    [Fact]
    public void UntrimmedSprite_HasNoSource()
    {
        this.Sprite("full", Images.Rectangle(4, 4, 9, 9, 9));

        Assert.False(Region(this.Build(), "full").TryGetProperty("source", out _));
    }

    [Fact]
    public void TrimmedSprite_KeepsItsLogicalBounds()
    {
        // A 10 x 8 image with pixels only from (2, 3) to (5, 6)
        this.Sprite("trimmed", Images.Rectangle(10, 8, 9, 9, 9, left: 2, top: 3, right: 5, bottom: 6));

        JsonElement region = Region(this.Build(), "trimmed");

        (int x, int y, int w, int h) = Rect(region);
        Assert.Equal((3, 3), (w, h));
        Assert.Equal((x - 2, y - 3, 10, 8), Rect(region, "source"));
    }

    [Fact]
    public void FullyTransparentSprite_IsAOnePixelPlaceholder()
    {
        this.Sprite("empty", new RgbaImage(10, 10));

        JsonElement region = Region(this.Build(), "empty");

        Assert.Equal((1, 1), (Rect(region).W, Rect(region).H));
    }

    [Fact]
    public void IdenticalSprites_ShareARegion_EvenAtDifferentTrims()
    {
        this.Sprite("a", Images.Rectangle(4, 4, 9, 9, 9));
        this.Sprite("b", Images.Rectangle(8, 8, 9, 9, 9, left: 2, top: 2, right: 6, bottom: 6));

        JsonElement atlas = this.Build();

        Assert.Equal(Rect(Region(atlas, "a")), Rect(Region(atlas, "b")));
        Assert.True(Region(atlas, "b").TryGetProperty("source", out _));
        Assert.False(Region(atlas, "a").TryGetProperty("source", out _));
    }

    [Fact]
    public void Extrude_RepeatsTheEdgesOutward_OutsideTheFrame()
    {
        this.Sprite("red", Images.Rectangle(2, 2, 200, 10, 20));

        JsonElement atlas = this.Build(extrude: 2);
        RgbaImage page = this.Page();

        (int x, int y, _, _) = Rect(Region(atlas, "red"));
        Assert.Equal((2, 2), (x, y));
        Assert.Equal((6, 6), (page.Width, page.Height));

        // Every corner and edge of the band
        foreach ((int px, int py) in new[] { (0, 0), (5, 0), (0, 5), (5, 5), (2, 0), (0, 2), (5, 3), (3, 5) })
        {
            Assert.Equal([200, 10, 20, 255], Pixel(page, px, py));
        }
    }

    [Fact]
    public void Padding_SeparatesTheExtrudedFootprints()
    {
        this.Sprite("a", Images.Rectangle(8, 8, 1, 2, 3));
        this.Sprite("b", Images.Rectangle(8, 8, 4, 5, 6));
        const int Padding = 3;
        const int Extrude = 2;

        JsonElement atlas = this.Build(padding: Padding, extrude: Extrude);

        (int ax, int ay, _, _) = Rect(Region(atlas, "a"));
        (int bx, int by, _, _) = Rect(Region(atlas, "b"));
        int separation = Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by)) - 8;
        Assert.True(separation >= Padding + (2 * Extrude), $"Frames {separation} apart.");
    }

    [Fact]
    public void Pages_NeverExceedTheLimit_AndAFullPageSpills()
    {
        for (int i = 0; i < 5; i++)
        {
            this.Sprite($"s{i}", Images.Rectangle(40, 40, (byte)i, 0, 0));
        }

        JsonElement atlas = this.Build(maxWidth: 64, maxHeight: 64);

        Assert.Equal(5, atlas.GetProperty("textures").GetArrayLength());
        Assert.True(File.Exists(this._directory.Path_("out", "atlas_4.ktx2")));
        foreach (JsonElement texture in atlas.GetProperty("textures").EnumerateArray())
        {
            Assert.Single(texture.GetProperty("regions").EnumerateObject());
        }

        Assert.True(this.Page(4).Width <= 64);
    }

    [Fact]
    public void Pages_GrowInBothDirectionsBeforeSpilling()
    {
        // All three fit on the 64 x 64 limit, two above one
        for (int i = 0; i < 3; i++)
        {
            this.Sprite($"s{i}", Images.Rectangle(32, 32, (byte)i, 0, 0));
        }

        Assert.Equal(1, this.Build(maxWidth: 64, maxHeight: 64).GetProperty("textures").GetArrayLength());
    }

    [Fact]
    public void Sprite_TooLargeForAPage_FailsNamingIt()
    {
        this.Sprite("huge", Images.Rectangle(40, 40, 1, 1, 1));

        var error = Assert.Throws<ContentBuildException>(() => this.Build(maxWidth: 32, maxHeight: 256));

        Assert.Contains("huge", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sprite_TooLargeOnceExtruded_Fails()
    {
        this.Sprite("big", Images.Rectangle(32, 32, 1, 1, 1));

        Assert.Throws<ContentBuildException>(() => this.Build(maxWidth: 32, maxHeight: 32, extrude: 1));
    }

    [Fact]
    public void PowerOfTwoAndSquare_ShapeThePage()
    {
        this.Sprite("wide", Images.Rectangle(20, 3, 1, 1, 1));

        this.Build(extrude: 0, switches: ["--power-of-two", "--square"]);

        RgbaImage page = this.Page();
        Assert.Equal(page.Width, page.Height);
        Assert.Equal(32, page.Width);
    }

    [Fact]
    public void Square_MakesThePageSquare()
    {
        this.Sprite("wide", Images.Rectangle(20, 3, 1, 1, 1));

        this.Build(extrude: 0, switches: ["--square"]);

        Assert.Equal((20, 20), (this.Page().Width, this.Page().Height));
    }

    [Fact]
    public void Output_DoesNotDependOnTheInputOrder()
    {
        this.Sprite("a", Images.Rectangle(9, 5, 1, 1, 1));
        this.Sprite("b", Images.Rectangle(4, 7, 2, 2, 2));
        this.Sprite("c", Images.Rectangle(3, 3, 3, 3, 3));
        this.Build();
        string first = File.ReadAllText(this._directory.Path_("out", "atlas.json"));
        byte[] firstPage = this.Page().Pixels;

        this._sprites.Reverse();
        this.Build();

        Assert.Equal(first, File.ReadAllText(this._directory.Path_("out", "atlas.json")));
        Assert.Equal(firstPage, this.Page().Pixels);
    }

    [Fact]
    public void DuplicateRegionNames_Fail()
    {
        this.Sprite("hero", Images.Rectangle(4, 4, 1, 1, 1));
        string other = this._directory.Path_("other", "HERO.png");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        Images.Rectangle(4, 4, 2, 2, 2).SavePng(other);
        this._sprites.Add(other);

        Assert.Throws<ContentBuildException>(() => this.Build());
    }

    [Fact]
    public void MissingSprite_FailsNamingIt()
    {
        this._sprites.Add(this._directory.Path_("src", "none.png"));

        var error = Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.Contains("none.png", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedEncoder_LeavesNoDescriptor()
    {
        this.Sprite("a", Images.Rectangle(4, 4, 1, 1, 1));
        this._directory.SetToolConfig("fail.texture", "partial");

        Assert.Throws<ContentBuildException>(() => this.Build());

        Assert.False(File.Exists(this._directory.Path_("out", "atlas.json")));
    }

    [Fact]
    public void NonAsciiRegionNames_AreWrittenAsTheyAre()
    {
        this.Sprite("été", Images.Rectangle(4, 4, 1, 1, 1));

        this.Build();

        Assert.Contains("\"été\"", File.ReadAllText(this._directory.Path_("out", "atlas.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void DecodedPage_MatchesTheGolden()
    {
        this.Sprite("a", Images.Sample(9, 7));
        this.Sprite("b", Images.Rectangle(6, 6, 200, 100, 50, left: 1, top: 1, right: 5, bottom: 4));
        this.Sprite("c", Images.Sample(4, 11));

        this.Build(padding: 1, extrude: 1);

        string identity = ContentVersions.Of("ToolkitContentPipelineTextureAtlasVersion");
        Goldens.Check(ToolkitPaths.Tests, "atlas.page.pixels", identity, this.Page().Pixels);
        Goldens.Check(ToolkitPaths.Tests, "atlas.descriptor", identity, File.ReadAllBytes(this._directory.Path_("out", "atlas.json")));
    }
}

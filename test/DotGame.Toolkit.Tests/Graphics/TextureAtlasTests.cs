// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using DotGame.Framework;
using DotGame.Framework.Content;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// The atlas manifest's format, and loading an atlas from it.
/// </summary>
public sealed class TextureAtlasTests
{
    private static Task<TextureAtlasManifest> ReadManifest(
        string json)
    {
        return TextureAtlasManifest.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }

    // A manifest of one page, with one region of the given members
    private static async Task<TextureAtlasManifest.TextureAtlasRegion> ReadRegion(
        string members)
    {
        var manifest = await ReadManifest($$"""{ "textures": [ { "name": "page", "regions": { "sprite": { {{members}} } } } ] }""");
        return manifest.Textures[0].Regions["sprite"];
    }

    private const string Frame = """ "frame": { "x": 10, "y": 20, "w": 30, "h": 40 } """;

    // The manifest

    [Fact]
    public async Task Manifest_ReadsPagesAndRegions_InFileOrder()
    {
        var manifest = await ReadManifest("""
            {
              "textures": [
                { "name": "a_0.webp", "regions": { "one": { "frame": { "x": 1, "y": 2, "w": 3, "h": 4 } } } },
                { "name": "a_1.webp", "regions": { "two": { "frame": { "x": 5, "y": 6, "w": 7, "h": 8 } }, "three": { "frame": { "x": 9, "y": 9, "w": 9, "h": 9 } } } }
              ]
            }
            """);

        Assert.Equal(["a_0.webp", "a_1.webp"], manifest.Textures.Select(t => t.Name));
        Assert.Equal(new RectangleInt(1, 2, 3, 4), manifest.Textures[0].Regions["one"].Frame);
        Assert.Equal(new RectangleInt(5, 6, 7, 8), manifest.Textures[1].Regions["two"].Frame);
        Assert.Equal(2, manifest.Textures[1].Regions.Count);
    }

    [Fact]
    public async Task Manifest_RegionWithoutASource_IsUntrimmed()
    {
        var region = await ReadRegion(Frame);

        Assert.Equal(new RectangleInt(10, 20, 30, 40), region.Source);
        Assert.False(region.IsTrimmed);
        Assert.Null(region.Caps);
        Assert.Null(region.Outset);
    }

    [Fact]
    public async Task Manifest_RegionWithASource_IsTrimmed()
    {
        var region = await ReadRegion(Frame + """, "source": { "x": 5, "y": 15, "w": 50, "h": 60 } """);

        Assert.Equal(new RectangleInt(10, 20, 30, 40), region.Frame);
        Assert.Equal(new RectangleInt(5, 15, 50, 60), region.Source);
        Assert.True(region.IsTrimmed);
    }

    [Fact]
    public async Task Manifest_Center_GivesTheCaps_MeasuredFromTheSource()
    {
        var region = await ReadRegion(
            Frame + """, "source": { "x": 0, "y": 0, "w": 100, "h": 100 }, "center": { "x": 10, "y": 20, "w": 60, "h": 40 } """);

        Assert.Equal(new ThicknessInt(10, 20, 30, 40), region.Caps);
    }

    [Fact]
    public async Task Manifest_Center_MeasuresFromTheFrame_WhenUntrimmed()
    {
        var region = await ReadRegion(Frame + """, "center": { "x": 15, "y": 25, "w": 20, "h": 30 } """);

        Assert.Equal(new ThicknessInt(5, 5, 5, 5), region.Caps);
    }

    [Fact]
    public async Task Manifest_Layout_GivesTheOutset_MeasuredFromTheSource()
    {
        var region = await ReadRegion(
            Frame + """, "source": { "x": 0, "y": 0, "w": 100, "h": 100 }, "layout": { "x": 1, "y": 2, "w": 90, "h": 80 } """);

        Assert.Equal(new ThicknessInt(1, 2, 9, 18), region.Outset);
    }

    [Fact]
    public async Task Manifest_MembersMayComeInAnyOrder()
    {
        var region = await ReadRegion(
            """ "center": { "h": 80, "w": 80, "y": 10, "x": 10 }, "source": { "x": 0, "y": 0, "w": 100, "h": 100 }, "frame": { "x": 0, "y": 0, "w": 100, "h": 100 } """);

        Assert.Equal(new ThicknessInt(10), region.Caps);
    }

    [Fact]
    public async Task Manifest_NamesIgnoreCase()
    {
        var manifest = await ReadManifest(
            """{ "TEXTURES": [ { "NAME": "page", "REGIONS": { "Sprite": { "FRAME": { "X": 1, "Y": 2, "W": 3, "H": 4 } } } } ] }""");

        Assert.Equal("page", manifest.Textures[0].Name);
        Assert.Equal(new RectangleInt(1, 2, 3, 4), manifest.Textures[0].Regions["sPRITE"].Frame);
    }

    [Fact]
    public async Task Manifest_SkipsUnknownMembers_AtEveryLevel()
    {
        var manifest = await ReadManifest("""
            {
              "version": 2,
              "meta": { "nested": [ 1, { "deep": true } ] },
              "textures": [
                {
                  "format": { "kind": "webp" },
                  "name": "page",
                  "future": [ [ ] ],
                  "regions": {
                    "sprite": {
                      "rotated": false,
                      "pivot": { "x": 0.5, "y": 0.5 },
                      "frame": { "x": 1, "z": { "a": 1 }, "y": 2, "w": 3, "h": 4 },
                      "tags": [ "a", "b" ]
                    }
                  }
                }
              ],
              "after": "end"
            }
            """);

        Assert.Equal(new RectangleInt(1, 2, 3, 4), manifest.Textures[0].Regions["sprite"].Frame);
    }

    [Theory]
    [InlineData("-1", "0", "10", "10")] // Starts before the source
    [InlineData("0", "-1", "10", "10")]
    [InlineData("95", "0", "10", "10")] // Ends after it
    [InlineData("0", "95", "10", "10")]
    [InlineData("0", "0", "-1", "10")] // Inverted
    [InlineData("0", "0", "10", "-1")]
    public async Task Manifest_CenterOrLayoutOutsideTheSource_IsMalformed(
        string x,
        string y,
        string width,
        string height)
    {
        const string Source = """ "frame": { "x": 0, "y": 0, "w": 100, "h": 100 } """;
        string rectangle = $$"""{ "x": {{x}}, "y": {{y}}, "w": {{width}}, "h": {{height}} }""";

        await Assert.ThrowsAsync<JsonException>(() => ReadRegion($$"""{{Source}}, "center": {{rectangle}}"""));
        await Assert.ThrowsAsync<JsonException>(() => ReadRegion($$"""{{Source}}, "layout": {{rectangle}}"""));
    }

    [Theory]
    [InlineData("[]")] // Not an object
    [InlineData("""{ "textures": {} }""")] // Not an array
    [InlineData("""{ "textures": [ 1 ] }""")] // Not an entry
    [InlineData("""{ "textures": [ { "regions": {} } ] }""")] // No name
    [InlineData("""{ "textures": [ { "name": "", "regions": {} } ] }""")]
    [InlineData("""{ "textures": [ { "name": "a", "regions": [] } ] }""")] // Not a dictionary
    [InlineData("""{ "textures": [ { "name": "a", "regions": { "b": 1 } } ] }""")] // Not a region
    [InlineData("""{ "textures": [ { "name": "a", """)] // Truncated
    public async Task Manifest_Malformed_Throws(
        string json)
    {
        await Assert.ThrowsAnyAsync<JsonException>(() => ReadManifest(json));
    }

    [Fact]
    public async Task Manifest_RejectsANullStream()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => TextureAtlasManifest.ReadAsync(null!));
    }

    // The loader

    private static FakeContent ContentWithAtlas(
        string path,
        string json)
    {
        var content = new FakeContent();
        content.Service.RegisterLoader(new TextureAtlasLoader(content.Service));
        content.AddFile(path, json);
        return content;
    }

    private const string TwoPages = """
        {
          "textures": [
            { "name": "ui_0", "regions": {
              "button": { "frame": { "x": 0, "y": 0, "w": 32, "h": 16 } },
              "panel": {
                "frame": { "x": 40, "y": 8, "w": 80, "h": 80 },
                "source": { "x": 30, "y": 0, "w": 100, "h": 100 },
                "center": { "x": 50, "y": 20, "w": 60, "h": 60 },
                "layout": { "x": 35, "y": 5, "w": 90, "h": 90 }
              }
            } },
            { "name": "ui_1", "regions": { "icon": { "frame": { "x": 2, "y": 4, "w": 8, "h": 8 } } } }
          ]
        }
        """;

    [Fact]
    public void Loader_RejectsANullContentService()
    {
        Assert.Throws<ArgumentNullException>(() => new TextureAtlasLoader(null!));
    }

    [Fact]
    public async Task TryGet_FindsARegion_OrSaysThereIsNone()
    {
        var content = ContentWithAtlas("Art/Atlases/ui", TwoPages);
        var atlas = await content.Service.LoadAsync<TextureAtlas>("Art/Atlases/ui");

        Assert.True(atlas.TryGetRegion("button", out var region));
        Assert.Same(atlas.GetRegion("button"), region);
        Assert.False(atlas.TryGetRegion("nothing", out _));
    }

    [Fact]
    public async Task ALoadedPage_RefusesDispose_AndGoesWithItsAtlas()
    {
        var content = ContentWithAtlas("Art/Atlases/ui", TwoPages);
        var atlas = await content.Service.LoadAsync<TextureAtlas>("Art/Atlases/ui");
        Assert.True(content.Service.TryGet<DotGame.Framework.Graphics.Texture>("Art/Atlases/ui_0", out var page));

        Assert.Throws<InvalidOperationException>(() =>
        {
            page.Dispose();
        });

        await content.Service.UnloadAsync(atlas);

        Assert.False(content.Service.TryGet<DotGame.Framework.Graphics.Texture>("Art/Atlases/ui_0", out _));
        Assert.False(content.Service.TryGet<DotGame.Framework.Graphics.Texture>("Art/Atlases/ui_1", out _));
        Assert.True(page.IsDisposed);
    }

    [Fact]
    public async Task Loader_LoadsPagesBesideTheManifest()
    {
        var content = ContentWithAtlas("Art/Atlases/ui", TwoPages);

        var atlas = await content.Service.LoadAsync<TextureAtlas>("Art/Atlases/ui");

        Assert.Equal(["Art/Atlases/ui_0", "Art/Atlases/ui_1"], content.LoadedTextures);
        Assert.Equal(["button", "icon", "panel"], atlas.RegionNames.Order());
    }

    [Fact]
    public async Task Loader_AtTheContentRoot_LoadsPagesThere()
    {
        var content = ContentWithAtlas("ui", TwoPages);

        await content.Service.LoadAsync<TextureAtlas>("ui");

        Assert.Equal(["ui_0", "ui_1"], content.LoadedTextures);
    }

    [Fact]
    public async Task Loader_NormalizesBackslashesInThePath()
    {
        var content = ContentWithAtlas("Art/ui", TwoPages);

        await content.Service.LoadAsync<TextureAtlas>("Art\\ui");

        Assert.Equal(["Art/ui_0", "Art/ui_1"], content.LoadedTextures);
    }

    [Fact]
    public async Task Loader_RegionsAreOnTheirOwnPages_AsTheManifestDescribes()
    {
        var content = ContentWithAtlas("ui", TwoPages);

        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");

        Assert.Same(content.Textures["ui_0"], atlas.GetRegion("button").Texture);
        Assert.Same(content.Textures["ui_1"], atlas.GetRegion("icon").Texture);

        var panel = atlas.GetRegion("panel");
        Assert.Equal(new RectangleInt(30, 0, 100, 100), panel.Bounds);
        Assert.Equal(new RectangleInt(40, 8, 80, 80), panel.PackedFrame);
        Assert.Equal(new ThicknessInt(20, 20, 20, 20), panel.Caps);
        Assert.Equal(new ThicknessInt(5), panel.Outset);
    }

    [Fact]
    public async Task Loader_RegionNamesIgnoreCase()
    {
        var content = ContentWithAtlas("ui", TwoPages);

        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");

        Assert.Same(atlas.GetRegion("button"), atlas.GetRegion("BUTTON"));
    }

    [Fact]
    public async Task Loader_ANinePatchFromTheManifest_DrawsWithItsTrimAndOutset()
    {
        var content = ContentWithAtlas("ui", TwoPages);
        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");
        var batch = new RecordingBatch();

        NinePatch.FromRegion(atlas.GetRegion("panel")).Draw(batch.Batch, new RectangleInt(0, 0, 90, 90));

        // Drawn as the 100 x 100 source, out by the outset of 5, with the trim of 10, 8, 10 and
        // 12 back in from there
        Assert.Equal(5, batch.Calls.Min(c => c.Left));
        Assert.Equal(3, batch.Calls.Min(c => c.Top));
        Assert.Equal(85, batch.Calls.Max(c => c.Right));
        Assert.Equal(83, batch.Calls.Max(c => c.Bottom));
    }

    [Fact]
    public async Task Loader_NoPages_IsInvalid()
    {
        var content = ContentWithAtlas("ui", "{}");

        await Assert.ThrowsAsync<InvalidDataException>(() => content.Service.LoadAsync<TextureAtlas>("ui"));
    }

    [Fact]
    public async Task Loader_AMissingManifest_Fails()
    {
        var content = ContentWithAtlas("ui", "{}");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => content.Service.LoadAsync<TextureAtlas>("other"));
    }

    [Fact]
    public async Task Loader_AMissingPage_Fails_AndTheLoadCanBeRetried()
    {
        var content = ContentWithAtlas("ui", TwoPages);
        content.MissingContent = "ui_1";

        await Assert.ThrowsAsync<FileNotFoundException>(() => content.Service.LoadAsync<TextureAtlas>("ui"));

        content.MissingContent = null;
        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");
        Assert.NotNull(atlas.GetRegion("icon"));
    }

    [Theory]
    [InlineData("""{ "textures": [ { "name": "p", "regions": { "a": { } } } ] }""")] // No frame
    [InlineData("""{ "textures": [ { "name": "p", "regions": { "a": { "frame": { "x": 0, "y": 0, "w": 0, "h": 5 } } } } ] }""")] // Empty frame
    [InlineData("""{ "textures": [ { "name": "p", "regions": { "a": { "frame": { "x": 2000, "y": 0, "w": 10, "h": 5 } } } } ] }""")] // Off the page
    public async Task Loader_ARegionThatCannotExist_Fails(
        string json)
    {
        var content = ContentWithAtlas("ui", json);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => content.Service.LoadAsync<TextureAtlas>("ui"));
    }

    [Theory]
    [InlineData("""{ "textures": [ { "name": "p", "regions": { "a": { "frame": { "w": 1, "h": 1 } }, "A": { "frame": { "w": 1, "h": 1 } } } } ] }""")]
    [InlineData("""{ "textures": [ { "name": "p", "regions": { "a": { "frame": { "w": 1, "h": 1 } } } }, { "name": "q", "regions": { "A": { "frame": { "w": 1, "h": 1 } } } } ] }""")]
    public async Task Loader_ARepeatedRegionName_Fails(
        string json)
    {
        var content = ContentWithAtlas("ui", json);

        await Assert.ThrowsAsync<ArgumentException>(() => content.Service.LoadAsync<TextureAtlas>("ui"));
    }

    // The atlas

    [Fact]
    public async Task Atlas_Get_ThrowsForAnUnknownRegion_NamingIt()
    {
        var content = ContentWithAtlas("ui", TwoPages);
        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");

        var exception = Assert.Throws<KeyNotFoundException>(() => atlas.GetRegion("nope"));

        Assert.Contains("nope", exception.Message);
    }

    [Fact]
    public async Task Atlas_Dispose_DisposesEveryPage_AndForgetsItsRegions()
    {
        var content = ContentWithAtlas("ui", TwoPages);
        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");
        Assert.False(((IContent)atlas).IsDisposed);

        ((IDisposable)atlas).Dispose();

        Assert.True(((IContent)atlas).IsDisposed);
        Assert.True(content.Textures["ui_0"].IsDisposed);
        Assert.True(content.Textures["ui_1"].IsDisposed);
        Assert.Empty(atlas.RegionNames);
    }

    [Fact]
    public void Atlas_NeedsAPage()
    {
        Assert.Throws<ArgumentException>(() => new TextureAtlas([], []));
        Assert.Throws<ArgumentNullException>(() => new TextureAtlas(null!, []));
        Assert.Throws<ArgumentNullException>(() => new TextureAtlas([TestTextures.Create()], null!));
    }

    [Fact]
    public async Task Service_SharesAnAtlas_UntilItsDisposed_ThenLoadsItAgain()
    {
        var content = ContentWithAtlas("ui", TwoPages);

        var atlas = await content.Service.LoadAsync<TextureAtlas>("ui");
        Assert.Same(atlas, await content.Service.LoadAsync<TextureAtlas>("ui"));

        ((IDisposable)atlas).Dispose();

        var reloaded = await content.Service.LoadAsync<TextureAtlas>("ui");
        Assert.NotSame(atlas, reloaded);
        Assert.False(((IContent)reloaded).IsDisposed);
        Assert.False(reloaded.GetRegion("icon").Texture.IsDisposed);
    }
}

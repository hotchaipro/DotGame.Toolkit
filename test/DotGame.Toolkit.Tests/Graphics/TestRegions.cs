// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// Creates texture regions and atlases for tests, over fake textures.
/// </summary>
internal static class TestRegions
{
    private static readonly Texture SharedTexture = TestTextures.Create(1 << 16, 1 << 16);

    public static TextureRegion Region(
        int width = 32,
        int height = 32,
        int x = 0)
    {
        return new TextureRegion(SharedTexture.AsSource(), new RectangleInt(x, 0, width, height));
    }

    public static TextureRegion[] Regions(
        int count)
    {
        return Enumerable.Range(0, count).Select(i => Region(x: i * 32)).ToArray();
    }

    public static TextureAtlas Atlas(
        params string[] regionNames)
    {
        var regions = new Dictionary<string, TextureRegion>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < regionNames.Length; i++)
        {
            regions.Add(regionNames[i], Region(x: i * 32));
        }

        return new TextureAtlas([TestTextures.Create()], regions);
    }
}

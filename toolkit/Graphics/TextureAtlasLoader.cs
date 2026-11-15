// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotGame.Framework.Content;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Graphics;

/// <summary>Loads texture atlases from content. <c>InitializeToolkit</c> registers it.</summary>
public sealed class TextureAtlasLoader : IContentLoader<TextureAtlas>
{
    private readonly ContentService _contentService;

    /// <summary>Makes the loader.</summary>
    public TextureAtlasLoader(
        ContentService contentService)
    {
        ArgumentNullException.ThrowIfNull(contentService);

        this._contentService = contentService;
    }

    /// <summary>Loads an atlas.</summary>
    public async Task<TextureAtlas> LoadAsync(
        string contentKey)
    {
        // Load the manifest first so we know which base textures to fetch.
        var manifest = await this.LoadManifestAsync(contentKey).ConfigureAwait(false);

        if (manifest.Textures.Count == 0)
        {
            throw new InvalidDataException(
                $"Texture atlas manifest '{contentKey}' does not declare any textures.");
        }

        // Resolve each texture entry's name relative to the manifest's
        // content directory and load all base textures in parallel.
        var manifestDirectory = GetContentDirectory(contentKey);

        var baseTextureTasks = new Task<Texture>[manifest.Textures.Count];
        for (int i = 0; i < manifest.Textures.Count; i++)
        {
            // Manifest page names are file names (e.g. "atlas_0.ktx2"); the page's
            // content key is its path without the extension
            var textureContentKey = CombineContentPath(
                manifestDirectory,
                Path.GetFileNameWithoutExtension(manifest.Textures[i].Name));

            baseTextureTasks[i] = this._contentService.LoadAsync<Texture>(textureContentKey);
        }

        await Task.WhenAll(baseTextureTasks).ConfigureAwait(false);

        var baseTextures = new Texture[baseTextureTasks.Length];
        int totalRegionCount = 0;
        for (int i = 0; i < baseTextureTasks.Length; i++)
        {
            baseTextures[i] = baseTextureTasks[i].Result;
            totalRegionCount += manifest.Textures[i].Regions.Count;
        }

        // Create named texture regions from metadata.
        var regions = new Dictionary<string, TextureRegion>(
            totalRegionCount,
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < manifest.Textures.Count; i++)
        {
            var textureEntry = manifest.Textures[i];
            var pageSource = baseTextures[i].AsSource();

            foreach (var regionEntry in textureEntry.Regions)
            {
                var region = regionEntry.Value;

                // Both rectangles are stored in atlas-page coordinates,
                // so the loader is a straight copy — no arithmetic.
                regions.Add(
                    regionEntry.Key,
                    new TextureRegion(
                        pageSource,
                        region.Source,
                        region.Frame,
                        region.Caps,
                        region.Outset));
            }
        }

        return new TextureAtlas(baseTextures, regions, this._contentService);
    }

    private async Task<TextureAtlasManifest> LoadManifestAsync(
        string contentKey)
    {
        // Resolve the content key through the platform content
        // service so the actual pipeline-emitted relative path is used.
        var resolvedManifestPath = this._contentService.ResolveContentPath(contentKey);

        var manifestStream = await this._contentService.OpenStreamAsync(
            resolvedManifestPath).ConfigureAwait(false);

        await using (manifestStream.ConfigureAwait(false))
        {
            return await TextureAtlasManifest.ReadAsync(manifestStream).ConfigureAwait(false);
        }
    }

    private static string GetContentDirectory(
        string contentKey)
    {
        // Content paths use forward slashes. Return everything up to (but not
        // including) the final separator, or empty if the path has no
        // directory component.
        int lastSeparator = contentKey.LastIndexOf('/');
        return lastSeparator < 0
            ? string.Empty
            : contentKey.Substring(0, lastSeparator);
    }

    private static string CombineContentPath(
        string directory,
        string name)
    {
        return string.IsNullOrEmpty(directory)
            ? name
            : string.Concat(directory, "/", name);
    }
}

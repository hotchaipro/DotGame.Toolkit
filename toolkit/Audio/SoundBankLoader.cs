// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotGame.Framework.Audio;
using DotGame.Framework.Content;

namespace DotGame.Toolkit.Audio;

/// <summary>Loads sound banks from content. <c>InitializeToolkit</c> registers it.</summary>
public sealed class SoundBankLoader : IContentLoader<SoundBank>
{
    private readonly ContentService _contentService;

    /// <summary>Makes the loader.</summary>
    public SoundBankLoader(
        ContentService contentService)
    {
        ArgumentNullException.ThrowIfNull(contentService);

        this._contentService = contentService;
    }

    /// <summary>Loads a sound bank.</summary>
    public async Task<SoundBank> LoadAsync(
        string contentKey)
    {
        // Load the manifest first so we know which audio buffer to fetch.
        var manifest = await this.LoadManifestAsync(contentKey).ConfigureAwait(false);

        if (string.IsNullOrEmpty(manifest.Name))
        {
            throw new InvalidDataException(
                $"Sound bank manifest '{contentKey}' does not declare a base buffer name.");
        }

        // Resolve the buffer's name relative to the manifest's content
        // directory.
        var manifestDirectory = GetContentDirectory(contentKey);
        var bufferContentPath = CombineContentPath(manifestDirectory, manifest.Name);

        var baseSound = await this._contentService.LoadAsync<Sound>(bufferContentPath).ConfigureAwait(false);

        // Create a named segment for each manifest entry. Every
        // segment is a non-owning (offset, duration) view over the
        // same underlying decoded audio.
        var segments = new Dictionary<string, SoundSegment>(
            manifest.Segments.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.Segments)
        {
            var offset = TimeSpan.FromSeconds(entry.Value.Offset);
            var duration = TimeSpan.FromSeconds(entry.Value.Duration);

            segments.Add(entry.Key, new SoundSegment(baseSound, offset, duration));
        }

        return new SoundBank(baseSound, segments, this._contentService);
    }

    private async Task<SoundBankManifest> LoadManifestAsync(
        string contentKey)
    {
        var resolvedManifestPath = this._contentService.ResolveContentPath(contentKey);

        var manifestStream = await this._contentService.OpenStreamAsync(
            resolvedManifestPath).ConfigureAwait(false);

        await using (manifestStream.ConfigureAwait(false))
        {
            return await SoundBankManifest.ReadAsync(manifestStream).ConfigureAwait(false);
        }
    }

    private static string GetContentDirectory(
        string contentKey)
    {
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

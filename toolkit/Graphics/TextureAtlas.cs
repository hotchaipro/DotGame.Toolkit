// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using DotGame.Framework.Content;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Represents a texture atlas: a single packed texture (or small set of
/// packed textures) composed of many named rectangular regions, each of
/// which was originally a separate source texture.
/// </summary>
public sealed class TextureAtlas : IContent
{
    private readonly Texture[] _baseTextures;
    private readonly Dictionary<string, TextureRegion> _regions;

    internal TextureAtlas(
        Texture[] baseTextures,
        Dictionary<string, TextureRegion> regions,
        ContentService? content = null)
    {
        ArgumentNullException.ThrowIfNull(baseTextures);
        ArgumentNullException.ThrowIfNull(regions);

        if (baseTextures.Length == 0)
        {
            throw new ArgumentException(
                "A texture atlas must have at least one base texture.",
                nameof(baseTextures));
        }

        this._baseTextures = baseTextures;
        this._content = content;
        this._regions = regions;
    }

    private readonly ContentService? _content;

    private bool _isDisposed;

    /// <summary>Whether it has been released.</summary>
    public bool IsDisposed => this._isDisposed;

    /// <summary>
    /// Gets the names of every region in the atlas, in no particular order.
    /// </summary>
    public IReadOnlyCollection<string> RegionNames => this._regions.Keys;

    /// <summary>
    /// Gets a named texture region from the atlas, if it has one.
    /// </summary>
    /// <param name="name">The name of the original source texture that was packed into the atlas.</param>
    /// <param name="region">The region, if there is one.</param>
    /// <returns>Whether there is a region with that name.</returns>
    public bool TryGetRegion(
        string name,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TextureRegion? region)
    {
        return this._regions.TryGetValue(name, out region);
    }

    /// <summary>
    /// Gets a named texture region from the atlas.
    /// </summary>
    /// <param name="name">The name of the original source texture that was packed into the atlas.</param>
    /// <returns>The <see cref="TextureRegion"/> for the named region.</returns>
    /// <exception cref="KeyNotFoundException">No region with the specified name exists.</exception>
    public TextureRegion GetRegion(
        string name)
    {
        if (!this._regions.TryGetValue(name, out var region))
        {
            throw new KeyNotFoundException(
                $"Texture atlas does not contain a region named '{name}'.");
        }

        return region;
    }

    void IDisposable.Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;
        this._regions.Clear();
        foreach (var baseTexture in this._baseTextures)
        {
            // NOTE: Its pages were loaded by key, so they're the content service's to release
            if (this._content is not null)
            {
                _ = this._content.UnloadAsync(baseTexture);
            }
            else
            {
                baseTexture.Dispose();
            }
        }
    }
}

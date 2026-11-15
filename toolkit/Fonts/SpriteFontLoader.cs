// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotGame.Framework.Content;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.IO;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Loads <see cref="SpriteFont"/> content (<c>.spritefont</c> files built by the toolkit
/// SpriteFontContent pipeline) together with all of its atlas pages.
/// </summary>
public sealed class SpriteFontLoader : IContentLoader<SpriteFont>
{
    // NOTE: The toolkit content pipeline adds it to every project with SpriteFontContent items
    private const string TextShaderKey = "_toolkit/Shaders/spritefont";

    private readonly ContentService _contentService;

    /// <summary>Makes the loader.</summary>
    public SpriteFontLoader(
        ContentService contentService)
    {
        ArgumentNullException.ThrowIfNull(contentService);

        this._contentService = contentService;
    }

    /// <summary>Loads a font.</summary>
    public async Task<SpriteFont> LoadAsync(
        string contentKey)
    {
        // NOTE: Every font shares one: the content service keeps what it has loaded by key
        var textShader = this._contentService.LoadAsync<Shader>(TextShaderKey);

        // Resolve the content key through the platform content
        // service so the actual pipeline-emitted relative path is opened.
        var resolvedFontPath = this._contentService.ResolveContentPath(contentKey);

        ReadOnlyMemory<byte> bytes;
        using (var stream = await this._contentService.OpenStreamAsync(resolvedFontPath).ConfigureAwait(false))
        {
            bytes = await stream.ReadAllBytesAsync().ConfigureAwait(false);
        }

        var fontData = SpriteFontReader.Read(bytes);

        // Pages are named relative to the font's directory, without an extension.
        var directory = GetDirectory(contentKey);
        var loads = new Task<Texture>[fontData.Pages.Count];
        for (int i = 0; i < loads.Length; i++)
        {
            loads[i] = this._contentService.LoadAsync<Texture>(directory + fontData.Pages[i].Name);
        }

        var pages = new List<Texture>(await Task.WhenAll(loads).ConfigureAwait(false));

        return new SpriteFont(fontData, pages, await textShader.ConfigureAwait(false), this._contentService);
    }

    private static string GetDirectory(
        string contentKey)
    {
        var normalized = contentKey.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator >= 0 ? normalized.Substring(0, separator + 1) : string.Empty;
    }
}

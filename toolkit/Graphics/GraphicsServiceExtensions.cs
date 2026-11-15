// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Imaging;
using DotGame.Imaging.Jpeg;
using DotGame.Imaging.Png;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Textures from PNG and JPEG images that arrive at run time: one downloaded, or picked by the player.
/// To work with an image's pixels first, decode it with DotGame.Imaging's <see cref="PngCodec"/>,
/// <see cref="JpegCodec"/> or <see cref="ImageDecoder"/>.
/// </summary>
public static class GraphicsServiceExtensions
{
    /// <summary>
    /// Makes a texture from a PNG image's bytes. It's yours to dispose.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes aren't a PNG image, or it's damaged.</exception>
    /// <exception cref="NotSupportedException">
    /// The image is of a kind the decoder doesn't read (see <see cref="PngCodec.Decode"/>).
    /// </exception>
    public static Texture CreateTextureFromPng(
        this GraphicsService graphics,
        ReadOnlySpan<byte> png)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!PngCodec.TryGetDecodedSize(png, PixelLayout.Rgba32, out int width, out int height, out _))
        {
            throw new InvalidDataException("The bytes aren't a PNG image, or its header is damaged.");
        }

        // Every pixel is decoded, so the array isn't cleared first
        var pixels = GC.AllocateUninitializedArray<Color32>(width * height);
        PngCodec.Decode(png, MemoryMarshal.AsBytes(pixels.AsSpan()), PixelLayout.Rgba32, premultiplyAlpha: true);
        return graphics.CreateTexture(new Vector2Int(width, height), pixels);
    }

    /// <summary>
    /// Reads a PNG image from a stream and makes a texture from it. Reading is asynchronous, and
    /// decoding happens on the game thread, so a very large image takes a frame or more.
    /// </summary>
    public static async Task<Texture> CreateTextureFromPngAsync(
        this GraphicsService graphics,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(stream);

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        return graphics.CreateTextureFromPng(memory.GetBuffer().AsSpan(0, (int)memory.Length));
    }

    /// <summary>
    /// Makes a texture from a JPEG image's bytes. It's yours to dispose. A game that ships the JPEG
    /// decoder credits the Independent JPEG Group (see <see cref="JpegCodec"/>).
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes aren't a JPEG image, or it's damaged.</exception>
    /// <exception cref="NotSupportedException">
    /// The image is of a kind the decoder doesn't read (see <see cref="JpegCodec.Decode"/>).
    /// </exception>
    public static Texture CreateTextureFromJpeg(
        this GraphicsService graphics,
        ReadOnlySpan<byte> jpeg)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!JpegCodec.TryGetDecodedSize(jpeg, PixelLayout.Rgba32, out int width, out int height, out _))
        {
            throw new InvalidDataException("The bytes aren't a JPEG image, or its frame header is damaged or missing.");
        }

        // Every pixel is decoded, so the array isn't cleared first. A JPEG is opaque, so its pixels
        // are premultiplied as they are.
        var pixels = GC.AllocateUninitializedArray<Color32>(width * height);
        JpegCodec.Decode(jpeg, MemoryMarshal.AsBytes(pixels.AsSpan()), PixelLayout.Rgba32);
        return graphics.CreateTexture(new Vector2Int(width, height), pixels);
    }
}

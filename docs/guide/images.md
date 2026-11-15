# Images at run time

Textures normally come from the content pipeline. For an image that arrives while the game runs, one
downloaded or picked by the player, `GraphicsServiceExtensions` (in `DotGame.Toolkit.Graphics`) makes a
texture straight from a PNG or JPEG file's bytes. The texture is yours to dispose:

```csharp
private async Task<Texture> LoadAvatar(Stream stream)
{
    return await this.Graphics.CreateTextureFromPngAsync(stream);
}

private Texture LoadPhoto(byte[] jpeg)
{
    return this.Graphics.CreateTextureFromJpeg(jpeg);
}
```

They decode with DotGame.Imaging, so they give the same pixels on every platform. A damaged image
throws `InvalidDataException`, and a valid one of a kind the decoder doesn't read (an arithmetic-coded
JPEG, say) throws `NotSupportedException`, with the reason.

To work with the pixels first, or to read a WebP image, use DotGame.Imaging directly, as
[its guide](https://github.com/hotchaipro/DotGame.Imaging/blob/main/docs/guide.md) shows. A game that
decodes JPEG says so in its credits or documentation, as libjpeg-turbo's license asks: DotGame.Imaging's
[README](https://github.com/hotchaipro/DotGame.Imaging#license) has the wording.

## Saving a screenshot

Draw the scene to a render texture, read it back, and encode it with DotGame.Imaging's `PngCodec`. The
pixels read back are premultiplied and a PNG stores straight alpha, so ask the encoder to divide it out:

```csharp
private async Task SaveScreenshot(RenderTexture canvas, string path)
{
    var pixels = new Color32[canvas.Size.X * canvas.Size.Y];
    await this.Graphics.ReadPixelsAsync(canvas, pixels);

    var png = new ArrayBufferWriter<byte>();
    PngCodec.Encode(png, canvas.Size.X, canvas.Size.Y, MemoryMarshal.AsBytes(pixels.AsSpan()), PixelLayout.Rgba32, unpremultiplyAlpha: true);
    File.WriteAllBytes(path, png.WrittenSpan);
}
```

On the web, `File.*` doesn't last past the page, so hand the bytes to the browser instead.

## See also

- [`GraphicsServiceExtensions`](../api/GraphicsServiceExtensions.md)
- The toolkit sample's Run-time textures page

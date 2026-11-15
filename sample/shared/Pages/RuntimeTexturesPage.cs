// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Imaging;
using DotGame.Imaging.Png;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// Textures made while the game runs: pixels replaced every frame with
/// <see cref="Texture.SetData"/>, a region of them, and a PNG decoded from bytes.
/// </summary>
internal sealed class RuntimeTexturesPage : SamplePage
{
    private const int PaintSize = 64;
    private const int PatchSize = 16;
    private static readonly Color32 Background = Color32.FromRgba(70, 80, 100, 255);

    private readonly Color32[] _paint = new Color32[PaintSize * PaintSize];
    private readonly Color32[] _patch = new Color32[PatchSize * PatchSize];
    private Texture? _painted;
    private Texture? _patched;
    private Texture? _fromPng;
    private int _patchStep = -1;

    public RuntimeTexturesPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Textures at run time";

    public override string Hint => "Left: every pixel replaced each frame. Middle: a moving region. Right: a PNG decoded from bytes.";

    public override Task Load()
    {
        var graphics = this.Game.Graphics;
        this._painted ??= graphics.CreateTexture(new Vector2Int(PaintSize, PaintSize), this._paint);

        var gray = new Color32[PaintSize * PaintSize];
        Array.Fill(gray, Background);
        this._patched ??= graphics.CreateTexture(new Vector2Int(PaintSize, PaintSize), gray);

        if (this._fromPng is null)
        {
            // A checkerboard encoded to PNG here stands in for an image that was downloaded
            var pixels = new Color32[PaintSize * PaintSize];
            for (int y = 0; y < PaintSize; y++)
            {
                for (int x = 0; x < PaintSize; x++)
                {
                    bool isLight = (((x / 8) + (y / 8)) % 2) == 0;
                    pixels[(y * PaintSize) + x] = isLight ? Color32.FromRgba(240, 220, 90, 255) : Color32.FromRgba(90, 120, 200, 255);
                }
            }

            var png = new ArrayBufferWriter<byte>();
            PngCodec.Encode(png, PaintSize, PaintSize, MemoryMarshal.AsBytes(pixels.AsSpan()), PixelLayout.Rgba32, unpremultiplyAlpha: true);
            this._fromPng = graphics.CreateTextureFromPng(png.WrittenSpan);
        }

        return Task.CompletedTask;
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        float time = (float)this.Game.Time.Total.TotalSeconds;

        for (int y = 0; y < PaintSize; y++)
        {
            for (int x = 0; x < PaintSize; x++)
            {
                float wave = 0.5f + (0.5f * MathF.Sin((x * 0.2f) + (y * 0.15f) + (time * 2)));
                this._paint[(y * PaintSize) + x] = Color32.FromRgba((byte)(255 * wave), (byte)(255 * (1 - wave)), 160, 255);
            }
        }

        this._painted!.SetData(this._paint);

        // One square of the texture is replaced, and when it moves on, the square it left is put
        // back, so the square moves around the texture
        int step = (int)(time * 2) % ((PaintSize / PatchSize) * (PaintSize / PatchSize));
        if (step != this._patchStep)
        {
            if (this._patchStep >= 0)
            {
                Array.Fill(this._patch, Background);
                this._patched!.SetData(this._patch, PatchBounds(this._patchStep));
            }

            Array.Fill(this._patch, Color32.FromRgba(240, 120, 90, 255));
            this._patched!.SetData(this._patch, PatchBounds(step));
            this._patchStep = step;
        }

        using var batch = spriteBatch.Begin(new SpriteBatchSettings { Sampler = Sampler.PointClamp });
        var scale = new Vector2(3);
        spriteBatch.Draw(this._painted, new Vector2(bounds.X + 16, bounds.Y + 16), Color.White, scale: scale);
        spriteBatch.Draw(this._patched!, new Vector2(bounds.X + 224, bounds.Y + 16), Color.White, scale: scale);
        spriteBatch.Draw(this._fromPng!, new Vector2(bounds.X + 432, bounds.Y + 16), Color.White, scale: scale);
    }

    private static RectangleInt PatchBounds(
        int step)
    {
        int columns = PaintSize / PatchSize;
        return new RectangleInt((step % columns) * PatchSize, (step / columns) * PatchSize, PatchSize, PatchSize);
    }

    public override void Dispose()
    {
        this._painted?.Dispose();
        this._patched?.Dispose();
        this._fromPng?.Dispose();
        base.Dispose();
    }
}

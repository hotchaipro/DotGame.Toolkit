// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// A <see cref="TextureAtlas"/>: many images packed into one texture, so drawing them doesn't
/// switch textures, and a <see cref="NinePatch"/>: a panel that stretches to any size while its
/// corners keep their shape.
/// </summary>
/// <remarks>
/// <para>
/// The atlas is built from every image in <c>Content/Sprites</c> by a TextureAtlasContent item.
/// Transparent borders are trimmed when packing, but each <see cref="TextureRegion"/> keeps its
/// image's size, so trimmed and untrimmed images draw alike.
/// </para>
/// <para>
/// A nine-patch is made from an image and its caps: the borders that don't stretch. Here the
/// caps are given in code; an atlas exported by a tool that records caps (TexturePacker's
/// nine-patch borders) can use <see cref="NinePatch.FromRegion"/> instead.
/// </para>
/// <para>
/// The panel image has a drop shadow, which isn't part of the panel's bounds. An outset
/// (<see cref="TextureRegion.WithOutset"/>) says so: the shadow is drawn outside the bounds a
/// nine-patch is given, so panels line up by their edges. The page draws the panel with and
/// without its outset, each with its bounds outlined.
/// </para>
/// </remarks>
internal sealed class AtlasPage : SamplePage
{
    private readonly List<string> _names = new();
    private readonly NinePatch _noOutset;
    private Vector2 _corner;
    private int _dragPointerId = -1;
    private RectangleInt _resizable;

    public AtlasPage(
        ToolkitSample game)
        : base(game)
    {
        // The game's panel without its outset: the shadow is drawn within the bounds
        this._noOutset = new NinePatch(game.Atlas.GetRegion("panel"), caps: new ThicknessInt(24));
    }

    public override string Title => "Atlases and nine-patches";

    public override string Hint => "Drag the corner of the big panel to resize it.";

    public override void Enter()
    {
        // The region names are the image file names, without extensions
        this._names.Clear();
        foreach (var name in this.Game.Atlas.RegionNames)
        {
            this._names.Add(name);
        }

        this._names.Sort(StringComparer.Ordinal);

        // Placed when first drawn, below the rest of the page
        this._corner = default;
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Began:
                if (Vector2.Distance(e.Position, this._corner) < 48)
                {
                    this._dragPointerId = e.Id;
                }

                break;

            case PointerEventKind.Moved when e.Id == this._dragPointerId:
                this._corner = e.Position;
                break;

            case PointerEventKind.Ended:
            case PointerEventKind.Canceled:
                if (e.Id == this._dragPointerId)
                {
                    this._dragPointerId = -1;
                }

                break;
        }
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        var atlas = game.Atlas;
        using var batch = spriteBatch.Begin();

        // Every region of the atlas, with its name. Regions draw like textures, centered on
        // their origin; the animation frames are on the next page.
        int x = bounds.X + 20;
        int y = bounds.Y + 16;
        foreach (var name in this._names)
        {
            if (name.StartsWith("character_", StringComparison.Ordinal) || name.StartsWith("coin_", StringComparison.Ordinal))
            {
                continue;
            }

            var region = atlas.GetRegion(name);
            var scale = 64f / Math.Max(region.Size.X, region.Size.Y);
            spriteBatch.Draw(region, new Vector2(x + 40, y + 36), Color.White, Angle.Zero, (Vector2)region.Size * 0.5f, new Vector2(scale));
            game.DrawText(spriteBatch, name, new Vector2(x + 40 - (game.MeasureText(name, 15) / 2), y + 74), 15, Color.FromRgb(0x5A6478));
            x += 100;
            if (x > bounds.Right - 100)
            {
                x = bounds.X + 20;
                y += 104;
            }
        }

        y += 116;
        game.DrawText(spriteBatch, $"{atlas.RegionNames.Count} images in one atlas, with the animation frames", new Vector2(bounds.X + 20, y - 10), 16, Color.FromRgb(0x5A6478));

        // Nine-patches: one image, any size, tinted
        var panelTop = y + 20;
        game.Panel.Draw(spriteBatch, new RectangleInt(bounds.X + 20, panelTop, 72, 72));
        game.Panel.Draw(spriteBatch, new RectangleInt(bounds.X + 108, panelTop, 160, 72), Color.FromRgb(0xFFE6C8));
        game.Panel.Draw(spriteBatch, new RectangleInt(bounds.X + 284, panelTop, 72, 140), Color.FromRgb(0xD2F0D2));

        // The same bounds, outlined, with and without the outset: with it, the panel fills its
        // bounds and its shadow falls outside them; without it, the shadow takes up part of them
        var outline = Color.FromRgb(0x3C8CDC);
        var withOutset = new RectangleInt(bounds.X + 388, panelTop, 112, 72);
        var withoutOutset = new RectangleInt(bounds.X + 532, panelTop, 112, 72);
        game.Panel.Draw(spriteBatch, withOutset);
        this._noOutset.Draw(spriteBatch, withoutOutset);
        spriteBatch.DrawRectangle(withOutset, outline, 1);
        spriteBatch.DrawRectangle(withoutOutset, outline, 1);
        game.DrawText(spriteBatch, "With its outset", new Vector2(withOutset.X, withOutset.Bottom + 14), 15, Color.FromRgb(0x5A6478));
        game.DrawText(spriteBatch, "Without", new Vector2(withoutOutset.X, withoutOutset.Bottom + 14), 15, Color.FromRgb(0x5A6478));

        // The resizable one, which stays at least as large as its caps
        var origin = new Vector2(bounds.X + 20, panelTop + 160);
        if (this._corner == default)
        {
            this._corner = origin + new Vector2(Math.Min(bounds.Width - 60, 480), 200);
        }

        // Kept within the page, and stored so: a press finds the handle where it's drawn
        this._corner = Vector2.Clamp(this._corner, origin + new Vector2(56), new Vector2(bounds.Right - 20, bounds.Bottom - 20));
        var size = this._corner - origin;
        this._resizable = new RectangleInt((int)origin.X, (int)origin.Y, (int)size.X, (int)size.Y);
        game.Panel.Draw(spriteBatch, this._resizable);
        game.DrawText(spriteBatch, $"{this._resizable.Width} x {this._resizable.Height}", new Vector2(origin.X + 20, origin.Y + 16), 20, Color.FromRgb(0x3C465A));

        // The handle at its corner
        var handle = atlas.GetRegion("star");
        spriteBatch.Draw(handle, (Vector2)this._resizable.Position + size, Color.White, Angle.Zero, (Vector2)handle.Size * 0.5f, new Vector2(0.5f));
    }
}

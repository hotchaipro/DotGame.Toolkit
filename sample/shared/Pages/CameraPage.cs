// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// A <see cref="ViewportScaler"/>: draw in the size you designed for, whatever the screen's size and
/// shape. And a <see cref="Camera"/>: move, zoom and turn the view of a world. Their transforms
/// multiply, so a camera can look around a scaled design.
/// </summary>
/// <remarks>
/// <para>
/// The dark rectangle stands for the screen: drag its corner to change its shape, and the scaler
/// fits the 400 x 300 design into it by its mode. The world goes on past the design, which the
/// Expand modes and the camera show.
/// </para>
/// <para>
/// A pointer is in screen coordinates: <see cref="ViewportScaler.ScreenToDesign"/> and then
/// <see cref="Camera.ScreenToWorld"/> take it back into the world, where a tap leaves a dot. The
/// HUD is drawn with the scaler's transform alone, so it scales with the design but stays put when
/// the camera moves.
/// </para>
/// </remarks>
internal sealed class CameraPage : SamplePage
{
    private static readonly Vector2Int DesignSize = new(400, 300);
    private static readonly Vector2 DesignCenter = new(200, 150);

    // A white outline, so the labels in the world read over its squares
    private static readonly TextEffects Haloed = new()
    {
        Outline = new TextOutline(Color.White, Width: 3f),
    };

    private static readonly ViewportScaleMode[] Modes =
    [
        ViewportScaleMode.Fit,
        ViewportScaleMode.Fill,
        ViewportScaleMode.Stretch,
        ViewportScaleMode.IntegerFit,
        ViewportScaleMode.Expand,
        ViewportScaleMode.ExpandVertical,
        ViewportScaleMode.ExpandHorizontal,
    ];

    private static readonly string[] ModeDescriptions =
    [
        "All of the design shows, as large as fits, with bars where the screen is left over.",
        "The design fills the screen, cut off where it doesn't fit.",
        "The design fills the screen exactly, stretched to its shape.",
        "As Fit, at a whole number of screen pixels per design pixel, for pixel art.",
        "All of the design shows, and the screen left over shows more of the world, not bars.",
        "As Expand on a taller screen, showing more below the design; as Fit on a wider one.",
        "As Expand on a wider screen, showing more right of the design; as Fit on a taller one.",
    ];

    private static readonly string[] ModeLabels =
    [
        "Mode: Fit",
        "Mode: Fill",
        "Mode: Stretch",
        "Mode: IntegerFit",
        "Mode: Expand",
        "Mode: ExpandVertical",
        "Mode: ExpandHorizontal",
    ];

    private readonly ViewportScaler _scaler = new(DesignSize);
    private int _modeButtonWidth;
    private readonly Camera _camera = new();
    private int _mode;
    private Vector2? _tap;

    // The screen's bottom-right corner, which can be dragged; placed when first drawn
    private Vector2 _corner;
    private RectangleInt _screen;
    private int _cornerPointerId = -1;
    private int _panPointerId = -1;
    private Vector2 _panLast;

    private RectangleInt _modeButton;
    private RectangleInt _zoomInButton;
    private RectangleInt _zoomOutButton;
    private RectangleInt _turnButton;
    private RectangleInt _resetButton;

    public CameraPage(
        ToolkitSample game)
        : base(game)
    {
        this.ResetCamera();
    }

    public override string Title => "Camera and scaling";

    public override string Hint => "Drag in the screen to move the camera, and its corner to reshape it. A tap leaves a dot in the world.";

    private void ResetCamera()
    {
        // The camera shows the world point Position at Origin, the design's center
        this._camera.Origin = DesignCenter;
        this._camera.Position = DesignCenter;
        this._camera.Zoom = 1f;
        this._camera.Rotation = Angle.Zero;
    }

    // A point on the screen, in the world: back through the scaler, then the camera
    private Vector2 ScreenToWorld(
        Vector2 screen)
    {
        return this._camera.ScreenToWorld(this._scaler.ScreenToDesign(screen));
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerEventKind.Began:
                if (IsTap(e, this._modeButton))
                {
                    this._mode = (this._mode + 1) % Modes.Length;
                }
                else if (IsTap(e, this._zoomInButton))
                {
                    this._camera.Zoom = MathF.Min(this._camera.Zoom * 1.25f, 4f);
                }
                else if (IsTap(e, this._zoomOutButton))
                {
                    this._camera.Zoom = MathF.Max(this._camera.Zoom / 1.25f, 0.25f);
                }
                else if (IsTap(e, this._turnButton))
                {
                    this._camera.Rotation += Angle.FromDegrees(15);
                }
                else if (IsTap(e, this._resetButton))
                {
                    this.ResetCamera();
                    this._tap = null;
                }
                else if (Vector2.Distance(e.Position, this._corner) < 32)
                {
                    this._cornerPointerId = e.Id;
                }
                else if (this._scaler.Viewport.Contains(e.Position))
                {
                    this._tap = this.ScreenToWorld(e.Position);
                    this._panPointerId = e.Id;
                    this._panLast = e.Position;
                }

                break;

            case PointerEventKind.Moved when e.Id == this._cornerPointerId:
                this._corner = e.Position;
                break;

            case PointerEventKind.Moved when e.Id == this._panPointerId:
                // Moves the camera so the world point under the pointer stays under it
                this._camera.Position += this.ScreenToWorld(this._panLast) - this.ScreenToWorld(e.Position);
                this._panLast = e.Position;
                break;

            case PointerEventKind.Ended:
            case PointerEventKind.Canceled:
                if (e.Id == this._cornerPointerId)
                {
                    this._cornerPointerId = -1;
                }

                if (e.Id == this._panPointerId)
                {
                    this._panPointerId = -1;
                }

                break;
        }
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        int x = bounds.X + 20;
        int y = bounds.Y + 12;

        // The controls and what they show
        // The mode's button fits the longest of its labels, so it keeps its size as the mode changes
        if (this._modeButtonWidth == 0)
        {
            foreach (var label in ModeLabels)
            {
                this._modeButtonWidth = Math.Max(this._modeButtonWidth, (int)MathF.Ceiling(game.MeasureText(label, 20)) + 40);
            }
        }

        this._modeButton = new RectangleInt(x, y, this._modeButtonWidth, 56);
        int next = this._modeButton.Right + 16;
        this._zoomInButton = new RectangleInt(next, y, 120, 56);
        this._zoomOutButton = new RectangleInt(next + 136, y, 120, 56);
        this._turnButton = new RectangleInt(next + 272, y, 120, 56);
        this._resetButton = new RectangleInt(next + 408, y, 120, 56);

        // The screen, below them: wider than the design to start with, so Fit shows bars
        var screenTop = y + 136;
        if (this._corner == default)
        {
            this._corner = new Vector2(x + Math.Min(bounds.Width - 60, 640), screenTop + 280);
        }

        // Kept within the page, and stored so: a press finds the handle where it's drawn
        var corner = Vector2.Clamp(this._corner, new Vector2(x + 120, screenTop + 90), new Vector2(bounds.Right - 20, bounds.Bottom - 40));
        this._corner = corner;
        this._screen = new RectangleInt(x, screenTop, (int)corner.X - x, (int)corner.Y - screenTop);

        var mode = Modes[this._mode];
        this._scaler.Mode = mode;
        this._scaler.Update(this._screen);

        using (spriteBatch.Begin())
        {
            game.DrawButton(spriteBatch, this._modeButton, ModeLabels[this._mode]);
            game.DrawButton(spriteBatch, this._zoomInButton, "Zoom in");
            game.DrawButton(spriteBatch, this._zoomOutButton, "Zoom out");
            game.DrawButton(spriteBatch, this._turnButton, "Turn");
            game.DrawButton(spriteBatch, this._resetButton, "Reset");

            var dark = Color.FromRgb(0x3C465A);
            var light = Color.FromRgb(0x5A6478);
            game.DrawText(spriteBatch, $"{mode}: {ModeDescriptions[this._mode]}", new Vector2(x, y + 70), 17, dark);
            var scale = this._scaler.Scale;
            var position = this._camera.Position;
            game.DrawText(spriteBatch, $"Scale {scale.X:0.##} x {scale.Y:0.##}. The camera looks at ({position.X:0}, {position.Y:0}), zoomed {this._camera.Zoom:0.##}x, turned {this._camera.Rotation.Degrees % 360:0} degrees.", new Vector2(x, y + 96), 15, light);

            // The screen: dark, so the bars that Fit leaves show
            spriteBatch.FillRectangle(this._screen, Color.FromRgb(0x23272F));
        }

        // What the scaler shows of the design, and no more
        using (game.Graphics.UseScissor(this._scaler.Viewport))
        {
            // The world, through the camera and then the scaler
            using (spriteBatch.Begin(this._camera.Transform * this._scaler.Transform))
            {
                this.DrawWorld(spriteBatch);
            }

            // The HUD, through the scaler alone: it scales with the design, but the camera doesn't move it
            using (spriteBatch.Begin(this._scaler.Transform))
            {
                const string HudText = "HUD: scaled, not moved by the camera";
                var hudWidth = (int)game.MeasureText(HudText, 15) + 32;
                var hud = new RectangleInt(200 - (hudWidth / 2), 12, hudWidth, 32);
                game.Panel.Draw(spriteBatch, hud, Color.FromRgb(0xFFF3C4));
                game.DrawText(spriteBatch, HudText, new Vector2(hud.X + 16, hud.Y + 8), 15, Color.FromRgb(0x483D8B));
            }
        }

        using (spriteBatch.Begin())
        {
            game.DrawText(spriteBatch, $"The screen, {this._screen.Width} x {this._screen.Height}", new Vector2(x, this._screen.Bottom + 8), 15, Color.FromRgb(0x5A6478));
            var handle = game.Atlas.GetRegion("star");
            spriteBatch.Draw(handle, corner, Color.White, Angle.Zero, (Vector2)handle.Size * 0.5f, new Vector2(0.5f));
        }
    }

    private void DrawWorld(
        SpriteBatch spriteBatch)
    {
        var game = this.Game;

        // A checkerboard of 50-pixel squares: blue in the design, 400 x 300, and gray beyond it, as
        // far as the camera and the Expand modes show
        for (int row = -8; row < 14; row++)
        {
            for (int column = -10; column < 18; column++)
            {
                bool even = ((row + column) & 1) == 0;
                bool inDesign = (row >= 0) && (row < 6) && (column >= 0) && (column < 8);
                var color = inDesign
                    ? (even ? Color.FromRgb(0xD2E1F5) : Color.FromRgb(0xB9CEEE))
                    : (even ? Color.FromRgb(0xE4E8EE) : Color.FromRgb(0xD3D9E2));
                spriteBatch.FillRectangle(new Rectangle(column * 50, row * 50, 50, 50), color);
            }
        }

        // The design's edge and corners

        var ink = Color.FromRgb(0x483D8B);
        spriteBatch.DrawRectangle(new Rectangle(0, 0, DesignSize.X, DesignSize.Y), ink, 3);
        game.DrawText(spriteBatch, "The design, 400 x 300", new Vector2(200 - (game.MeasureText("The design, 400 x 300", 30) / 2), 128), 30, ink, Haloed);
        game.DrawText(spriteBatch, "0, 0", new Vector2(8, 6), 15, ink, Haloed);
        game.DrawText(spriteBatch, "400, 300", new Vector2(DesignSize.X - 8 - game.MeasureText("400, 300", 15), DesignSize.Y - 24), 15, ink, Haloed);

        // The last tap, where it landed in the world
        if (this._tap is { } tap)
        {
            spriteBatch.FillCircle(tap, 7, Color.White);
            spriteBatch.FillCircle(tap, 5, Color.FromRgb(0xFF4500));
            game.DrawText(spriteBatch, $"({tap.X:0}, {tap.Y:0})", tap + new Vector2(10, -8), 15, Color.FromRgb(0xC83200), Haloed);
        }
    }
}

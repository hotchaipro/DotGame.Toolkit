// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Draws a game designed for one size on a screen of any size: tell it the size you designed for and
/// draw in those coordinates, whatever the window or device.
/// </summary>
/// <example>
/// <code>
/// scaler.Update(this.ClientBounds);
/// using var scissor = this.Graphics.UseScissor(scaler.Viewport);   // Keeps Fit's bars clear
/// using (spriteBatch.Begin(scaler.Transform))
/// {
///     spriteBatch.Draw(background, Vector2.Zero, Color.White);
/// }
///
/// var pointer = scaler.ScreenToDesign(e.Position);
/// </code>
/// </example>
public sealed class ViewportScaler
{
    /// <param name="designSize">The size you draw in.</param>
    /// <param name="mode">How that fits onto the screen.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size isn't positive.</exception>
    /// <summary>Makes a scaler for the size you designed for.</summary>
    public ViewportScaler(
        Vector2Int designSize,
        ViewportScaleMode mode = ViewportScaleMode.Fit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(designSize.X, nameof(designSize));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(designSize.Y, nameof(designSize));

        this.DesignSize = designSize;
        this.Mode = mode;
        this.Update(new RectangleInt(Vector2Int.Zero, designSize));
    }

    /// <summary>
    /// Gets the size you draw in.
    /// </summary>
    public Vector2Int DesignSize { get; }

    /// <summary>
    /// Gets or sets how the design fits onto the screen. It applies from the next <see cref="Update"/>.
    /// </summary>
    public ViewportScaleMode Mode { get; set; }

    /// <summary>
    /// Gets the transform from design coordinates to the screen, for <c>SpriteBatch.Begin</c>.
    /// </summary>
    public Matrix3x2 Transform { get; private set; } = Matrix3x2.Identity;

    /// <summary>
    /// Gets the screen pixels per design pixel, along each axis.
    /// </summary>
    public Vector2 Scale { get; private set; } = Vector2.One;

    /// <summary>
    /// Gets the part of the screen the game draws in, clipped to the screen: the area between
    /// <see cref="ViewportScaleMode.Fit"/>'s bars. Use it as the scissor rectangle to keep them clear.
    /// </summary>
    public RectangleInt Viewport { get; private set; }

    /// <summary>
    /// Gets the part of the design coordinates that's on screen, for laying out to the edges of the
    /// screen and for leaving out what can't be seen. It's the whole design with
    /// <see cref="ViewportScaleMode.Fit"/>, less of it with <see cref="ViewportScaleMode.Fill"/>, and
    /// more of it with <see cref="ViewportScaleMode.Expand"/>, from the design's top left going on
    /// below and to the right.
    /// </summary>
    public Rectangle VisibleBounds { get; private set; }

    /// <summary>
    /// Works out the fit for the screen. Call it when the screen's size changes, or each frame: it's
    /// only arithmetic.
    /// </summary>
    /// <param name="screen">The area to fit into, such as <c>Game.ClientBounds</c> or <c>SafeAreaBounds</c>.</param>
    public void Update(
        RectangleInt screen)
    {
        float scaleX = screen.Width / (float)this.DesignSize.X;
        float scaleY = screen.Height / (float)this.DesignSize.Y;

        var scale = this.Mode switch
        {
            ViewportScaleMode.Fill => new Vector2(MathF.Max(scaleX, scaleY)),
            ViewportScaleMode.Stretch => new Vector2(scaleX, scaleY),

            // NOTE: At least 1, so a screen smaller than the design still shows it, cut off
            ViewportScaleMode.IntegerFit => new Vector2(MathF.Max(1f, MathF.Floor(MathF.Min(scaleX, scaleY)))),
            _ => new Vector2(MathF.Min(scaleX, scaleY)),
        };

        var size = new Vector2(this.DesignSize.X, this.DesignSize.Y) * scale;

        // NOTE: These scale as Fit does, so the design stays whole, and the sides left over are shown
        if (this.Mode is ViewportScaleMode.Expand or ViewportScaleMode.ExpandHorizontal)
        {
            size.X = screen.Width;
        }

        if (this.Mode is ViewportScaleMode.Expand or ViewportScaleMode.ExpandVertical)
        {
            size.Y = screen.Height;
        }

        // NOTE: Whole pixels, so a sprite's edges stay where the design put them
        float left = MathF.Floor(screen.X + ((screen.Width - size.X) * 0.5f));
        float top = MathF.Floor(screen.Y + ((screen.Height - size.Y) * 0.5f));

        this.Scale = scale;
        this.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(left, top);

        int visibleLeft = Math.Max((int)left, screen.X);
        int visibleTop = Math.Max((int)top, screen.Y);
        int visibleRight = Math.Min((int)MathF.Ceiling(left + size.X), screen.Right);
        int visibleBottom = Math.Min((int)MathF.Ceiling(top + size.Y), screen.Bottom);
        this.Viewport = new RectangleInt(
            visibleLeft,
            visibleTop,
            Math.Max(0, visibleRight - visibleLeft),
            Math.Max(0, visibleBottom - visibleTop));

        // NOTE: A minimized window has no size, and shows nothing
        if ((scale.X <= 0) || (scale.Y <= 0))
        {
            this.VisibleBounds = default;
            return;
        }

        float shownLeft = MathF.Max(left, screen.X);
        float shownTop = MathF.Max(top, screen.Y);
        float shownRight = MathF.Min(left + size.X, screen.Right);
        float shownBottom = MathF.Min(top + size.Y, screen.Bottom);
        this.VisibleBounds = new Rectangle(
            (shownLeft - left) / scale.X,
            (shownTop - top) / scale.Y,
            MathF.Max(0, shownRight - shownLeft) / scale.X,
            MathF.Max(0, shownBottom - shownTop) / scale.Y);
    }

    /// <summary>
    /// Gets where a point in the design appears on the screen.
    /// </summary>
    public Vector2 DesignToScreen(
        Vector2 design)
    {
        return Vector2.Transform(design, this.Transform);
    }

    /// <summary>
    /// Gets the point in the design at a point on the screen, such as a pointer's position.
    /// </summary>
    public Vector2 ScreenToDesign(
        Vector2 screen)
    {
        var offset = new Vector2(this.Transform.M31, this.Transform.M32);
        return (screen - offset) / this.Scale;
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Represents a rectangular sub-region of a <see cref="Texture"/>, such as
/// a sprite packed into a texture atlas. Implements
/// <see cref="ITextureSource"/> so it can be passed anywhere the framework
/// accepts a textured rectangle.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TextureRegion"/> is a non-owning view of its source
/// texture. It has no lifetime of its own and is not
/// <see cref="IDisposable"/>; the underlying <see cref="Texture"/> is
/// owned by whoever created it (typically the atlas that holds the base
/// texture).
/// </para>
/// <para>
/// <see cref="Bounds"/> describes the region's <em>logical</em>
/// rectangle in the coordinate space of the underlying texture — the
/// rectangle the sprite occupies from the caller's point of view (matching
/// TexturePacker's <c>sourceSize</c>/<c>spriteSourceSize</c>). For
/// atlas-packed sprites whose fully-transparent borders were trimmed during
/// packing, the pixels that actually live in the underlying texture may
/// cover only a sub-rectangle of <see cref="Bounds"/>, exposed as
/// <see cref="PackedFrame"/> (matching TexturePacker's <c>frame</c>).
/// </para>
/// </remarks>
public sealed class TextureRegion : ITextureSource
{
    /// <summary>
    /// Creates a region of the specified source texture.
    /// </summary>
    /// <param name="source">
    /// The texture this region is a view of. If <paramref name="source"/> is itself a
    /// <see cref="TextureRegion"/>, the rectangles are in that region's coordinate space and are
    /// composed with it, and the result points directly at the underlying texture.
    /// </param>
    /// <param name="bounds">
    /// The logical (untrimmed) rectangle this region covers, in the source's coordinate space. Must
    /// have positive width and height and lie within the source's bounds. Matches TexturePacker's
    /// <c>sourceSize</c>/<c>spriteSourceSize</c>.
    /// </param>
    /// <param name="frame">
    /// The part of the source holding the sprite's pixels, for a trimmed region, within
    /// <paramref name="bounds"/>; <c>null</c> for all of it. Matches TexturePacker's
    /// <c>frame</c>.
    /// </param>
    /// <param name="caps">
    /// 9-slice cap insets in the region's logical coordinate space, or <c>null</c> if it isn't a
    /// nine-patch.
    /// </param>
    /// <param name="outset">
    /// How far the region's logical rectangle extends outside its layout bounds (a baked-in drop
    /// shadow, for example), or <c>null</c> if it doesn't.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bounds"/> has non-positive size or doesn't fit within the source's
    /// bounds, or it doesn't intersect a trimmed source's frame, or <paramref name="frame"/> doesn't
    /// lie within the rectangles it must.
    /// </exception>
    public TextureRegion(
        ITextureSource source,
        RectangleInt bounds,
        RectangleInt? frame = null,
        ThicknessInt? caps = null,
        ThicknessInt? outset = null)
        : this((frame is { } trimmed) ? Resolve(source, bounds, trimmed) : Resolve(source, bounds), caps, outset)
    {
    }

    private TextureRegion(
        (Texture Texture, RectangleInt SourceRectangle, RectangleInt Frame) resolved,
        ThicknessInt? caps,
        ThicknessInt? outset)
    {
        this.Texture = resolved.Texture;
        this.Bounds = resolved.SourceRectangle;
        this.PackedFrame = resolved.Frame;
        this.Caps = caps;
        this.Outset = outset;
    }

    /// <summary>
    /// Gets the underlying texture this region is a view of.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the logical (untrimmed) rectangle, within the underlying
    /// texture, that this region occupies. Equivalent to TexturePacker's
    /// <c>sourceSize</c>/<c>spriteSourceSize</c>.
    /// </summary>
    public RectangleInt Bounds { get; }

    /// <summary>
    /// Gets the rectangle within <see cref="Texture"/> that contains this
    /// region's actual pixels (TexturePacker's <c>frame</c>). For a
    /// non-trimmed region this equals <see cref="Bounds"/>; for a
    /// region whose fully-transparent border was trimmed during atlas
    /// packing it is a (possibly smaller) sub-rectangle lying inside
    /// <see cref="Bounds"/>. For a region created by
    /// <see cref="WithOutset"/> it extends outside
    /// <see cref="Bounds"/> by the outset.
    /// </summary>
    public RectangleInt PackedFrame { get; }

    /// <summary>
    /// Gets the 9-slice cap insets authored for this region, in the
    /// region's logical (untrimmed) coordinate space, or <c>null</c> when
    /// the region is not a nine-patch. Populated by content loaders that
    /// understand nine-patch metadata (e.g. TexturePacker's <c>border</c>
    /// field).
    /// </summary>
    public ThicknessInt? Caps { get; }

    /// <summary>
    /// Gets the amount, in logical pixels, that the region's
    /// <see cref="Bounds"/> extends outside its layout (visual)
    /// bounds, such as a baked-in drop shadow or glow, or <c>null</c> when
    /// none was authored. Populated by content loaders from the atlas
    /// manifest's <c>layout</c> rectangle.
    /// </summary>
    public ThicknessInt? Outset { get; }

    /// <summary>
    /// Gets the logical (untrimmed) size of the region in pixels.
    /// </summary>
    public Vector2Int Size => new Vector2Int(this.Bounds.Width, this.Bounds.Height);

    /// <summary>
    /// Creates a view of this region whose logical
    /// <see cref="Bounds"/> is deflated by <paramref name="outset"/>
    /// while the drawn <see cref="PackedFrame"/> is unchanged, so pixels such as a
    /// baked-in drop shadow are drawn outside the logical bounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the inverse of trimming: the frame extends outside the
    /// logical rectangle instead of lying inside it. The existing
    /// trim-aware draw path offsets the frame accordingly, so there is no
    /// additional per-draw cost. Layout (via <see cref="Size"/>) uses the
    /// visual bounds, excluding the outset.
    /// </para>
    /// <para>
    /// The returned region is intended for plain sprites. It carries no
    /// <see cref="Caps"/> or <see cref="Outset"/>; use
    /// <see cref="NinePatch"/>'s outset for nine-patches instead.
    /// </para>
    /// </remarks>
    /// <param name="outset">
    /// The amount, in logical pixels, to deflate the logical rectangle by.
    /// Must not be negative.
    /// </param>
    /// <returns>
    /// The new region, or this region when <paramref name="outset"/> is zero.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outset"/> has a negative side, or leaves no positive
    /// logical size.
    /// </exception>
    public TextureRegion WithOutset(
        ThicknessInt outset)
    {
        if ((outset.Left < 0) || (outset.Top < 0) || (outset.Right < 0) || (outset.Bottom < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(outset), "Outset must not be negative.");
        }

        if (outset == ThicknessInt.Zero)
        {
            return this;
        }

        var source = this.Bounds;
        var layout = new RectangleInt(
            source.X + outset.Left,
            source.Y + outset.Top,
            source.Width - outset.Horizontal,
            source.Height - outset.Vertical);

        if ((layout.Width <= 0) || (layout.Height <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outset),
                "Outset must leave a positive logical width and height.");
        }

        return new TextureRegion((this.Texture, layout, this.PackedFrame), caps: null, outset: null);
    }

    private static (Texture Texture, RectangleInt SourceRectangle, RectangleInt Frame) Resolve(
        ITextureSource source,
        RectangleInt bounds)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "Source rectangle must have positive width and height.");
        }

        var parentRectangle = source.Bounds;
        var parentLocal = new RectangleInt(0, 0, parentRectangle.Width, parentRectangle.Height);
        if (!parentLocal.Contains(bounds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "Source rectangle must fit entirely within the source.");
        }

        var composedSource = new RectangleInt(
            parentRectangle.X + bounds.X,
            parentRectangle.Y + bounds.Y,
            bounds.Width,
            bounds.Height);

        var composedFrame = RectangleInt.Intersection(composedSource, source.PackedFrame);
        if (composedFrame.Width <= 0 || composedFrame.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "Source rectangle does not intersect the source's frame (the requested area was fully trimmed away).");
        }

        return (source.Texture, composedSource, composedFrame);
    }

    private static (Texture Texture, RectangleInt SourceRectangle, RectangleInt Frame) Resolve(
        ITextureSource source,
        RectangleInt bounds,
        RectangleInt frame)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                "Source rectangle must have positive width and height.");
        }

        if (frame.Width <= 0 || frame.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                "Frame must have positive width and height.");
        }

        if (!bounds.Contains(frame))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                "Frame must lie entirely within the source rectangle.");
        }

        var parentRectangle = source.Bounds;
        var parentLocal = new RectangleInt(0, 0, parentRectangle.Width, parentRectangle.Height);
        if (!parentLocal.Contains(frame))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                "Frame must fit entirely within the source.");
        }

        var composedSource = new RectangleInt(
            parentRectangle.X + bounds.X,
            parentRectangle.Y + bounds.Y,
            bounds.Width,
            bounds.Height);

        var composedFrame = new RectangleInt(
            parentRectangle.X + frame.X,
            parentRectangle.Y + frame.Y,
            frame.Width,
            frame.Height);

        composedFrame = RectangleInt.Intersection(composedFrame, source.PackedFrame);
        if (composedFrame.Width <= 0 || composedFrame.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                "Frame does not intersect the source's frame (the requested area was fully trimmed away).");
        }

        return (source.Texture, composedSource, composedFrame);
    }
}

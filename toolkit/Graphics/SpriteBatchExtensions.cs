// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Toolkit extension methods that let <see cref="SpriteBatch"/> draw
/// <see cref="ITextureSource"/> values (texture regions, trimmed atlas
/// sprites, etc.) by translating them into the framework's
/// <see cref="Texture"/>-plus-rectangle primitive.
/// </summary>
public static class SpriteBatchExtensions
{
    /// <summary>
    /// Queues a texture source, or a part of it, to be drawn during this batch.
    /// </summary>
    /// <param name="source">
    /// The texture source to draw. May be a plain <see cref="Texture"/>
    /// (via <see cref="AsSource(Texture)"/>), a trimmed atlas region, or
    /// any other <see cref="ITextureSource"/>. When the source is
    /// trimmed, only its <see cref="ITextureSource.PackedFrame"/> is drawn and
    /// the origin is adjusted so the pivot lands at the same logical
    /// pixel regardless of trimming.
    /// </param>
    /// <param name="origin">
    /// The point the source is positioned, rotated and scaled about, in its logical (untrimmed)
    /// pixels from its top-left corner.
    /// </param>
    /// <param name="scale">
    /// The scale along each axis, or <c>null</c> for none. A negative component mirrors the
    /// source about <paramref name="origin"/>, trimmed or not.
    /// </param>
    /// <param name="sourceRectangle">
    /// A sub-rectangle to draw, or <c>null</c> to draw the entire source (trim-aware). When
    /// <paramref name="source"/> is a trimmed region, this rectangle is interpreted in the
    /// source's logical coordinate space and is translated to the underlying GPU texture's
    /// coordinates, and only its part inside the trimmed frame is drawn. With a sub-rectangle,
    /// <paramref name="origin"/> is relative to the sub-rectangle's top-left corner.
    /// </param>
    /// <param name="flip">
    /// Mirrors the image inside its logical (untrimmed) rectangle, which doesn't move: it flips in
    /// place whatever the origin, and a trimmed source's pixels land mirrored where they belong.
    /// </param>
    /// <remarks>
    /// <see cref="SpriteBatch.Begin()"/> must be called before this method.
    /// </remarks>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="position">Where the source's origin is drawn.</param>
    /// <param name="color">The tint its colors are multiplied by.</param>
    /// <param name="rotation">The rotation about the origin.</param>
    public static void Draw(
        this SpriteBatch spriteBatch,
        ITextureSource source,
        Vector2 position,
        Color color,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        Rectangle? sourceRectangle = null,
        SpriteFlip flip = SpriteFlip.None)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(source);

        Rectangle effectiveSourceRectangle;
        Vector2 effectiveOrigin;
        var frame = source.PackedFrame;
        var logical = source.Bounds;

        if (sourceRectangle.HasValue)
        {
            // Translate caller's source-local sub-rectangle into the
            // underlying GPU texture's coordinate space. For a non-trimmed
            // source, SourceRectangle == Frame so this reduces to a simple
            // offset by Frame.XY.
            var r = sourceRectangle.Value;
            float left = logical.X + r.X;
            float top = logical.Y + r.Y;

            // A trimmed source has no pixels outside its frame (its neighbors in the atlas are
            // there), so only the part of the sub-rectangle inside the frame is drawn, the
            // origin moved by what's clipped from its near sides so the rest lands in place
            float clippedLeft = MathF.Max(left, frame.X);
            float clippedTop = MathF.Max(top, frame.Y);
            float clippedRight = MathF.Min(left + r.Width, frame.X + frame.Width);
            float clippedBottom = MathF.Min(top + r.Height, frame.Y + frame.Height);
            if ((clippedRight <= clippedLeft) || (clippedBottom <= clippedTop))
            {
                return;
            }

            effectiveSourceRectangle = new Rectangle(
                clippedLeft,
                clippedTop,
                clippedRight - clippedLeft,
                clippedBottom - clippedTop);
            // A flip mirrors the sub-rectangle in place, so what's clipped from its far side is what
            // moves the drawn part from its near side
            float nearX = ((flip & SpriteFlip.Horizontal) != 0) ? (left + r.Width) - clippedRight : clippedLeft - left;
            float nearY = ((flip & SpriteFlip.Vertical) != 0) ? (top + r.Height) - clippedBottom : clippedTop - top;
            effectiveOrigin = new Vector2(origin.X - nearX, origin.Y - nearY);
        }
        else
        {
            effectiveSourceRectangle = new Rectangle(frame.X, frame.Y, frame.Width, frame.Height);
            effectiveOrigin = AdjustOriginForTrim(frame, logical, origin, flip);
        }

        spriteBatch.Draw(source.Texture, position, color, rotation, effectiveOrigin, scale, effectiveSourceRectangle, flip);
    }

    /// <summary>
    /// Draws a region of a texture, trim-aware, as the <see cref="ITextureSource"/> overload does,
    /// without an interface call per property: for particles and other sprites by the thousand. Its parameters, the flip among them, are as that overload's.
    /// </summary>
    public static void Draw(
        this SpriteBatch spriteBatch,
        TextureRegion region,
        Vector2 position,
        Color color,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        SpriteFlip flip = SpriteFlip.None)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(region);

        var frame = region.PackedFrame;
        spriteBatch.Draw(
            region.Texture,
            position,
            color,
            rotation,
            AdjustOriginForTrim(frame, region.Bounds, origin, flip),
            scale,
            new Rectangle(frame.X, frame.Y, frame.Width, frame.Height),
            flip);
    }

    /// <summary>
    /// Wraps a plain <see cref="Texture"/> as an <see cref="ITextureSource"/>
    /// covering the whole texture. This allocates on every call, so keep the
    /// result rather than calling it each frame.
    /// </summary>
    public static ITextureSource AsSource(
        this Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new WholeTextureSource(texture);
    }

    /// <summary>
    /// Adjusts a user-supplied origin (expressed in the source's logical
    /// coordinate space) into the coordinate space of the
    /// <see cref="ITextureSource.PackedFrame"/> that is actually drawn.
    /// </summary>
    /// <remarks>
    /// A trimmed source's frame is inset into its logical
    /// <see cref="ITextureSource.Bounds"/>. When the sprite is
    /// flipped, the effective inset on each axis becomes the trim on the
    /// opposite side (logical size minus frame size minus near-side trim).
    /// Subtracting that effective inset from the origin places the pivot at
    /// the same logical pixel regardless of trimming. For a non-trimmed
    /// source (where frame equals source rectangle) this method returns
    /// <paramref name="origin"/> unchanged.
    /// </remarks>
    private static Vector2 AdjustOriginForTrim(
        RectangleInt frame,
        RectangleInt logical,
        Vector2 origin,
        SpriteFlip flip)
    {
        // The frame's offset within the logical (untrimmed) rectangle. A mirroring scale mirrors
        // the whole logical rectangle about the origin, trimmed part and all, so the offset is the
        // same either way. A flip mirrors the image inside the logical rectangle, so the frame sits
        // as far from the near side as it was from the far one.
        int nearX = ((flip & SpriteFlip.Horizontal) != 0) ? (logical.X + logical.Width) - (frame.X + frame.Width) : frame.X - logical.X;
        int nearY = ((flip & SpriteFlip.Vertical) != 0) ? (logical.Y + logical.Height) - (frame.Y + frame.Height) : frame.Y - logical.Y;

        if (nearX == 0 && nearY == 0)
        {
            return origin;
        }

        return new Vector2(origin.X - nearX, origin.Y - nearY);
    }

    private sealed class WholeTextureSource : ITextureSource
    {
        private readonly Texture _texture;
        private readonly RectangleInt _frame;

        public WholeTextureSource(
            Texture texture)
        {
            this._texture = texture;
            var size = texture.Size;
            this._frame = new RectangleInt(0, 0, size.X, size.Y);
        }

        public Texture Texture => this._texture;

        public RectangleInt PackedFrame => this._frame;

        public RectangleInt Bounds => this._frame;
    }
}

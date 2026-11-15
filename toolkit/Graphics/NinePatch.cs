// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// A nine-patch (a.k.a. 9-slice) renderer for a textured rectangle.
/// </summary>
/// <remarks>
/// <para>
/// Divides an <see cref="ITextureSource"/> into a 3&#215;3 grid of slices
/// defined by the cap (border) insets. When drawn at an arbitrary size,
/// the four corner slices are preserved at a fixed size, the four edge
/// slices stretch along one axis, and the center slice stretches along
/// both axes. This is the standard technique used by Android's
/// <c>NinePatchDrawable</c>, Godot's <c>NinePatchRect</c>, Unity's
/// sliced sprites, and iOS's resizable images.
/// </para>
/// <para>
/// Cap insets are specified in the source's <em>logical</em> (untrimmed)
/// coordinate space. Trimmed atlas regions are handled automatically:
/// the trim padding is subtracted from the effective cap insets and from
/// the drawn bounds so the visible pixels land in the same place they
/// would if the source had not been trimmed.
/// </para>
/// <para>
/// An optional <see cref="Outset"/> describes how far the texture extends
/// outside the bounds it is drawn into, such as a drop shadow or glow baked
/// into the texture. Layout uses the visual bounds (excluding the outset)
/// and the texture is drawn expanded by the outset, like CSS
/// <c>border-image-outset</c>, Godot's <c>StyleBoxTexture</c> expand margins,
/// or Android's nine-patch layout bounds.
/// </para>
/// </remarks>
public sealed class NinePatch
{
    private readonly ITextureSource _source;
    private readonly ThicknessInt _caps;
    private readonly ThicknessInt _outset;

    // Trim padding in source-local logical coordinates.
    private readonly ThicknessInt _pad;
    private readonly int _logicalWidth;
    private readonly int _logicalHeight;

    // 9 slice rectangles, in source-local logical coordinates, pre-clipped
    // to the source's Frame (so no slice samples outside the sprite).
    // Stored as framework Rectangles so the hot Draw path needs no per-call
    // RectangleInt-to-Rectangle conversion.
    private readonly Rectangle _sliceTopLeft;
    private readonly Rectangle _sliceTop;
    private readonly Rectangle _sliceTopRight;
    private readonly Rectangle _sliceLeft;
    private readonly Rectangle _sliceCenter;
    private readonly Rectangle _sliceRight;
    private readonly Rectangle _sliceBottomLeft;
    private readonly Rectangle _sliceBottom;
    private readonly Rectangle _sliceBottomRight;

    // Precomputed reciprocals of the fixed source extents so the hot Draw
    // path scales with multiplications instead of divisions. A zero
    // reciprocal reproduces the original "0 when the extent is empty" result.
    private readonly float _invCenterW;
    private readonly float _invCenterH;
    private readonly float _invCapL;
    private readonly float _invCapT;
    private readonly float _invCapR;
    private readonly float _invCapB;
    private readonly float _invLogicalCapW;
    private readonly float _invLogicalCapH;

    // Effective cap extents, in source-local logical pixels. Cached as ints
    // because the framework Rectangle exposes Width/Height as floats.
    private readonly int _capL;
    private readonly int _capR;
    private readonly int _capT;
    private readonly int _capB;

    /// <summary>
    /// Creates a <see cref="NinePatch"/> with the specified cap insets.
    /// </summary>
    /// <remarks>
    /// When <paramref name="source"/> is a <see cref="TextureRegion"/> that
    /// declares an <see cref="TextureRegion.Outset"/>, that outset is used;
    /// otherwise the outset is zero.
    /// </remarks>
    /// <param name="source">The texture source to slice.</param>
    /// <param name="caps">Cap insets in logical pixels.</param>
    public NinePatch(
        ITextureSource source,
        ThicknessInt caps)
        : this(source, caps, (source as TextureRegion)?.Outset ?? ThicknessInt.Zero)
    {
    }

    /// <summary>
    /// Creates a <see cref="NinePatch"/> with the specified cap insets and
    /// outset.
    /// </summary>
    /// <param name="source">The texture source to slice.</param>
    /// <param name="caps">Cap insets in logical pixels.</param>
    /// <param name="outset">
    /// The amount, in logical pixels, that the texture extends outside the
    /// bounds it is drawn into. Overrides any outset declared by a
    /// <see cref="TextureRegion"/> source. Must not be negative.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outset"/> has a negative side.
    /// </exception>
    public NinePatch(
        ITextureSource source,
        ThicknessInt caps,
        ThicknessInt outset)
    {
        ArgumentNullException.ThrowIfNull(source);

        if ((outset.Left < 0) || (outset.Top < 0) || (outset.Right < 0) || (outset.Bottom < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(outset), "Outset must not be negative.");
        }

        this._source = source;
        this._caps = caps;
        this._outset = outset;

        var src = source.Bounds;
        var frm = source.PackedFrame;

        // Amount of transparent border trimmed off each side of the logical
        // rectangle during packing. Zero for non-trimmed sources.
        this._pad = new ThicknessInt(
            frm.X - src.X,
            frm.Y - src.Y,
            (src.X + src.Width) - (frm.X + frm.Width),
            (src.Y + src.Height) - (frm.Y + frm.Height));

        // Frame rectangle expressed in source-local logical coordinates.
        int fx = this._pad.Left;
        int fy = this._pad.Top;
        int fw = src.Width - this._pad.Left - this._pad.Right;
        int fh = src.Height - this._pad.Top - this._pad.Bottom;

        // Caps reduced by the portion that fell within the trimmed border,
        // and clamped to the frame so no slice samples outside the sprite
        // (e.g. when the visible pixels lie entirely within one cap).
        int effCapLeft = Math.Clamp(caps.Left - this._pad.Left, 0, fw);
        int effCapTop = Math.Clamp(caps.Top - this._pad.Top, 0, fh);
        int effCapRight = Math.Clamp(caps.Right - this._pad.Right, 0, fw - effCapLeft);
        int effCapBottom = Math.Clamp(caps.Bottom - this._pad.Bottom, 0, fh - effCapTop);

        // Caps that fill an untrimmed source leave no center to stretch, so a pixel of the wider
        // cap does, rather than a larger patch having a hole. A trim that took the center away
        // is different: nothing is meant to draw there.
        if ((this._pad.Horizontal == 0) && (fw > 0) && ((effCapLeft + effCapRight) == fw))
        {
            if (effCapRight >= effCapLeft)
            {
                effCapRight--;
            }
            else
            {
                effCapLeft--;
            }
        }

        if ((this._pad.Vertical == 0) && (fh > 0) && ((effCapTop + effCapBottom) == fh))
        {
            if (effCapBottom >= effCapTop)
            {
                effCapBottom--;
            }
            else
            {
                effCapTop--;
            }
        }

        // Center rectangle.
        int cx = fx + effCapLeft;
        int cy = fy + effCapTop;
        int cw = Math.Max(0, fw - effCapLeft - effCapRight);
        int ch = Math.Max(0, fh - effCapTop - effCapBottom);

        this._sliceTopLeft = new Rectangle(fx, fy, effCapLeft, effCapTop);
        this._sliceTop = new Rectangle(cx, fy, cw, effCapTop);
        this._sliceTopRight = new Rectangle(cx + cw, fy, effCapRight, effCapTop);
        this._sliceLeft = new Rectangle(fx, cy, effCapLeft, ch);
        this._sliceCenter = new Rectangle(cx, cy, cw, ch);
        this._sliceRight = new Rectangle(cx + cw, cy, effCapRight, ch);
        this._sliceBottomLeft = new Rectangle(fx, cy + ch, effCapLeft, effCapBottom);
        this._sliceBottom = new Rectangle(cx, cy + ch, cw, effCapBottom);
        this._sliceBottomRight = new Rectangle(cx + cw, cy + ch, effCapRight, effCapBottom);

        // Precompute the reciprocals the hot Draw path multiplies by.
        this._invCapL = effCapLeft > 0 ? 1f / effCapLeft : 0f;
        this._invCapT = effCapTop > 0 ? 1f / effCapTop : 0f;
        this._invCapR = effCapRight > 0 ? 1f / effCapRight : 0f;
        this._invCapB = effCapBottom > 0 ? 1f / effCapBottom : 0f;
        this._invCenterW = cw > 0 ? 1f / cw : 0f;
        this._invCenterH = ch > 0 ? 1f / ch : 0f;
        // The too-small path scales against the visual (non-outset) part of
        // the logical caps, since the outset grows with the same scale.
        this._invLogicalCapW = 1f / Math.Max(1, this._caps.Horizontal - outset.Horizontal);
        this._invLogicalCapH = 1f / Math.Max(1, this._caps.Vertical - outset.Vertical);

        this._capL = effCapLeft;
        this._capR = effCapRight;
        this._capT = effCapTop;
        this._capB = effCapBottom;
        this._logicalWidth = src.Width;
        this._logicalHeight = src.Height;
    }

    /// <summary>
    /// Creates a <see cref="NinePatch"/> from a <see cref="TextureRegion"/>
    /// that already declares its cap insets via
    /// <see cref="TextureRegion.Caps"/> (typically loaded from atlas
    /// metadata that includes a nine-patch <c>center</c> rectangle).
    /// </summary>
    /// <remarks>
    /// The region's <see cref="TextureRegion.Outset"/>, if any, is also
    /// applied.
    /// </remarks>
    /// <param name="region">The texture region to slice.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="region"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="region"/> does not declare nine-patch caps. Pass
    /// caps explicitly via <see cref="NinePatch(ITextureSource, ThicknessInt)"/>
    /// in that case.
    /// </exception>
    public static NinePatch FromRegion(
        TextureRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        var caps = region.Caps
            ?? throw new InvalidOperationException(
                "The texture region does not declare nine-patch caps; "
                + "supply caps explicitly via the NinePatch constructor.");

        return new NinePatch(region, caps, region.Outset ?? ThicknessInt.Zero);
    }

    /// <summary>
    /// Gets the texture source backing this nine-patch.
    /// </summary>
    public ITextureSource Source => this._source;

    /// <summary>
    /// Gets the amount, in logical pixels, that the texture extends outside
    /// the bounds it is drawn into, such as a baked-in drop shadow or glow.
    /// </summary>
    public ThicknessInt Outset => this._outset;

    /// <summary>
    /// Draws the nine-patch stretched to fill the specified bounds.
    /// </summary>
    /// <param name="spriteBatch">
    /// An active <see cref="SpriteBatch"/> (within
    /// the scope from <see cref="SpriteBatch.Begin()"/>).
    /// </param>
    /// <param name="bounds">
    /// The logical destination rectangle, in pixels. The texture is drawn
    /// expanded by <see cref="Outset"/>.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, RectangleInt bounds)
        => this.Draw(spriteBatch, bounds, Color.White);

    /// <summary>
    /// Draws the nine-patch stretched to fill the specified bounds.
    /// </summary>
    /// <param name="spriteBatch">
    /// An active <see cref="SpriteBatch"/> (within
    /// the scope from <see cref="SpriteBatch.Begin()"/>).
    /// </param>
    /// <param name="bounds">
    /// The logical destination rectangle, in pixels. The texture is drawn
    /// expanded by <see cref="Outset"/>.
    /// </param>
    /// <param name="color">Tint color, multiplied with the sampled pixels.</param>
    public void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds,
        Color color)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);

        if ((bounds.Width <= 0) || (bounds.Height <= 0))
        {
            return;
        }

        // The logical destination, expanded by the outset
        int logicalW = bounds.Width + this._outset.Horizontal;
        int logicalH = bounds.Height + this._outset.Vertical;

        int bx, by, bw, bh;

        int capL = this._capL;
        int capR = this._capR;
        int capT = this._capT;
        int capB = this._capB;

        float scaleL, scaleT, scaleR, scaleB;
        int scaledL, scaledT, scaledR, scaledB;
        int centerW, centerH;

        if ((logicalW <= 0) || (logicalH <= 0) || (this._caps.Horizontal > logicalW) || (this._caps.Vertical > logicalH))
        {
            // Destination too small: scale corners uniformly.
            // Use the original logical cap sizes (less the outset, which
            // scales along with them) and the visual destination bounds to
            // compute the scale factor so that a trimmed source produces the
            // same visual result as an equivalent untrimmed one.
            float s = Math.Min(
                bounds.Width * this._invLogicalCapW,
                bounds.Height * this._invLogicalCapH);

            // Scale the outset and pad inset by the same factor so the draw
            // origin and the available destination space for the visible
            // caps remain proportional to the logical layout.
            int scaledPadL = (int)(this._pad.Left * s) - (int)(this._outset.Left * s);
            int scaledPadT = (int)(this._pad.Top * s) - (int)(this._outset.Top * s);
            int scaledPadR = (int)(this._pad.Right * s) - (int)(this._outset.Right * s);
            int scaledPadB = (int)(this._pad.Bottom * s) - (int)(this._outset.Bottom * s);

            bx = bounds.X + scaledPadL;
            by = bounds.Y + scaledPadT;
            bw = bounds.Width - scaledPadL - scaledPadR;
            bh = bounds.Height - scaledPadT - scaledPadB;

            scaledL = ScaleCap(capL, s);
            scaledT = ScaleCap(capT, s);
            scaledR = ScaleCap(capR, s);
            scaledB = ScaleCap(capB, s);

            // Each cap rounds alone, so together they may be a pixel over
            FitCaps(ref scaledL, ref scaledR, bw);
            FitCaps(ref scaledT, ref scaledB, bh);

            centerW = Math.Max(0, bw - scaledL - scaledR);
            centerH = Math.Max(0, bh - scaledT - scaledB);

            // Scale each cap to exactly its rounded extent. A single shared scale would draw
            // asymmetric caps (e.g. 55/61) fractionally short of the integer offsets the next
            // row and column start at, leaving a hairline seam.
            scaleL = scaledL * this._invCapL;
            scaleT = scaledT * this._invCapT;
            scaleR = scaledR * this._invCapR;
            scaleB = scaledB * this._invCapB;
        }
        else
        {
            // Where the frame lands in the destination: the part of the source each edge is in
            // decides how far it moves
            int left = ToDestination(this._pad.Left, this._caps.Left, this._caps.Right, this._logicalWidth, logicalW);
            int top = ToDestination(this._pad.Top, this._caps.Top, this._caps.Bottom, this._logicalHeight, logicalH);
            int right = ToDestination(this._logicalWidth - this._pad.Right, this._caps.Left, this._caps.Right, this._logicalWidth, logicalW);
            int bottom = ToDestination(this._logicalHeight - this._pad.Bottom, this._caps.Top, this._caps.Bottom, this._logicalHeight, logicalH);

            bx = bounds.X - this._outset.Left + left;
            by = bounds.Y - this._outset.Top + top;
            bw = right - left;
            bh = bottom - top;

            scaledL = capL;
            scaledT = capT;
            scaledR = capR;
            scaledB = capB;
            centerW = Math.Max(0, bw - capL - capR);
            centerH = Math.Max(0, bh - capT - capB);
            scaleL = 1f;
            scaleT = 1f;
            scaleR = 1f;
            scaleB = 1f;
        }

        var centerScale = new Vector2(
            centerW * this._invCenterW,
            centerH * this._invCenterH);

        // Top row.
        this.DrawSlice(spriteBatch, new Vector2(bx, by), this._sliceTopLeft, color, new Vector2(scaleL, scaleT));
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL, by), this._sliceTop, color, new Vector2(centerScale.X, scaleT));
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL + centerW, by), this._sliceTopRight, color, new Vector2(scaleR, scaleT));

        // Middle row.
        this.DrawSlice(spriteBatch, new Vector2(bx, by + scaledT), this._sliceLeft, color, new Vector2(scaleL, centerScale.Y));
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL, by + scaledT), this._sliceCenter, color, centerScale);
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL + centerW, by + scaledT), this._sliceRight, color, new Vector2(scaleR, centerScale.Y));

        // Bottom row.
        this.DrawSlice(spriteBatch, new Vector2(bx, by + scaledT + centerH), this._sliceBottomLeft, color, new Vector2(scaleL, scaleB));
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL, by + scaledT + centerH), this._sliceBottom, color, new Vector2(centerScale.X, scaleB));
        this.DrawSlice(spriteBatch, new Vector2(bx + scaledL + centerW, by + scaledT + centerH), this._sliceBottomRight, color, new Vector2(scaleR, scaleB));
    }

    /// <summary>
    /// Draws the nine-patch into bounds that need not be whole pixels, as for an animated panel,
    /// with its caps scaled: 2 for a source drawn at half the size of a high-density display.
    /// </summary>
    /// <param name="spriteBatch">
    /// An active <see cref="SpriteBatch"/> (within the scope from <see cref="SpriteBatch.Begin()"/>).
    /// </param>
    /// <param name="bounds">The logical destination rectangle, in pixels.</param>
    /// <param name="color">Tint color, multiplied with the sampled pixels.</param>
    /// <param name="capScale">
    /// The scale of the caps, and of the trim and outset that go with them. Caps that wouldn't fit
    /// the bounds at this scale shrink to fit.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch,
        Rectangle bounds,
        Color color,
        float capScale)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capScale);

        if ((bounds.Width <= 0) || (bounds.Height <= 0))
        {
            return;
        }

        float scale = capScale;
        int visualCapsW = this._caps.Horizontal - this._outset.Horizontal;
        int visualCapsH = this._caps.Vertical - this._outset.Vertical;
        if (visualCapsW > 0)
        {
            scale = MathF.Min(scale, bounds.Width / visualCapsW);
        }

        if (visualCapsH > 0)
        {
            scale = MathF.Min(scale, bounds.Height / visualCapsH);
        }

        // In the source's pixels: the outset grows the bounds, as in the whole-pixel path
        float logicalW = (bounds.Width / scale) + this._outset.Horizontal;
        float logicalH = (bounds.Height / scale) + this._outset.Vertical;

        float left = ToDestination((float)this._pad.Left, this._caps.Left, this._caps.Right, this._logicalWidth, logicalW);
        float top = ToDestination((float)this._pad.Top, this._caps.Top, this._caps.Bottom, this._logicalHeight, logicalH);
        float right = ToDestination((float)(this._logicalWidth - this._pad.Right), this._caps.Left, this._caps.Right, this._logicalWidth, logicalW);
        float bottom = ToDestination((float)(this._logicalHeight - this._pad.Bottom), this._caps.Top, this._caps.Bottom, this._logicalHeight, logicalH);

        float centerW = MathF.Max(0, right - left - this._capL - this._capR);
        float centerH = MathF.Max(0, bottom - top - this._capT - this._capB);
        float stretchX = centerW * this._invCenterW;
        float stretchY = centerH * this._invCenterH;

        float x0 = bounds.X + ((left - this._outset.Left) * scale);
        float y0 = bounds.Y + ((top - this._outset.Top) * scale);
        float x1 = x0 + (this._capL * scale);
        float y1 = y0 + (this._capT * scale);
        float x2 = x1 + (centerW * scale);
        float y2 = y1 + (centerH * scale);

        this.DrawSlice(spriteBatch, new Vector2(x0, y0), this._sliceTopLeft, color, new Vector2(scale, scale));
        this.DrawSlice(spriteBatch, new Vector2(x1, y0), this._sliceTop, color, new Vector2(stretchX * scale, scale));
        this.DrawSlice(spriteBatch, new Vector2(x2, y0), this._sliceTopRight, color, new Vector2(scale, scale));
        this.DrawSlice(spriteBatch, new Vector2(x0, y1), this._sliceLeft, color, new Vector2(scale, stretchY * scale));
        this.DrawSlice(spriteBatch, new Vector2(x1, y1), this._sliceCenter, color, new Vector2(stretchX * scale, stretchY * scale));
        this.DrawSlice(spriteBatch, new Vector2(x2, y1), this._sliceRight, color, new Vector2(scale, stretchY * scale));
        this.DrawSlice(spriteBatch, new Vector2(x0, y2), this._sliceBottomLeft, color, new Vector2(scale, scale));
        this.DrawSlice(spriteBatch, new Vector2(x1, y2), this._sliceBottom, color, new Vector2(stretchX * scale, scale));
        this.DrawSlice(spriteBatch, new Vector2(x2, y2), this._sliceBottomRight, color, new Vector2(scale, scale));
    }

    private static float ToDestination(
        float position,
        int capStart,
        int capEnd,
        int sourceLength,
        float destinationLength)
    {
        if (position <= capStart)
        {
            return position;
        }

        if (position >= (sourceLength - capEnd))
        {
            return destinationLength - (sourceLength - position);
        }

        float stretch = (destinationLength - capStart - capEnd) / (sourceLength - capStart - capEnd);
        return capStart + ((position - capStart) * stretch);
    }

    // Where a position in the logical source lands in the logical destination: caps keep their
    // size, and the center stretches.
    private static int ToDestination(
        int position,
        int capStart,
        int capEnd,
        int sourceLength,
        int destinationLength)
    {
        if (position <= capStart)
        {
            return position;
        }

        if (position >= (sourceLength - capEnd))
        {
            return destinationLength - (sourceLength - position);
        }

        float stretch = (destinationLength - capStart - capEnd) / (float)(sourceLength - capStart - capEnd);
        return capStart + (int)MathF.Round((position - capStart) * stretch);
    }

    private static void FitCaps(
        ref int start,
        ref int end,
        int available)
    {
        int over = start + end - Math.Max(0, available);
        for (; over > 0; over--)
        {
            if (end >= start)
            {
                end--;
            }
            else
            {
                start--;
            }
        }
    }

    // Scales a cap extent without letting a non-empty cap collapse to zero,
    // so thin (e.g. 1-pixel) trimmed caps remain visible when shrunk.
    private static int ScaleCap(
        int cap,
        float scale)
    {
        if (cap <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)MathF.Round(cap * scale));
    }

    private void DrawSlice(
        SpriteBatch spriteBatch,
        Vector2 position,
        Rectangle slice,
        Color color,
        Vector2 scale)
    {
        if ((slice.Width <= 0) || (slice.Height <= 0))
        {
            return;
        }

        // The toolkit's Draw(ITextureSource, ..., Rectangle?, ...) extension
        // translates this source-local logical rectangle into the underlying
        // GPU texture's coordinate space (adding the source's SourceRectangle
        // origin), so this draw works correctly for both plain textures and
        // atlas regions.
        spriteBatch.Draw(
            source: this._source,
            position: position,
            sourceRectangle: slice,
            color: color,
            rotation: Angle.Zero,
            origin: Vector2.Zero,
            scale: scale);
    }
}

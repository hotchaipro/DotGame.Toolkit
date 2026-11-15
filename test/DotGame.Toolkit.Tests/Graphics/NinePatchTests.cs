// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// Where each slice lands, through the sprite batch a platform receives it in.
/// </summary>
public sealed class NinePatchTests
{
    private const float Precision = 0.001f;

    private static readonly Texture Page = TestTextures.Create(4096, 4096);

    private static TextureRegion Untrimmed(
        int width = 100,
        int height = 100,
        int x = 0,
        int y = 0)
    {
        return new TextureRegion(Page.AsSource(), new RectangleInt(x, y, width, height));
    }

    // A source with its logical rectangle at (x, y) and the given trim on each side
    private static TextureRegion Trimmed(
        ThicknessInt trim,
        int width = 100,
        int height = 100,
        int x = 200,
        int y = 300)
    {
        return new TextureRegion(
            Page.AsSource(),
            new RectangleInt(x, y, width, height),
            new RectangleInt(x + trim.Left, y + trim.Top, width - trim.Horizontal, height - trim.Vertical));
    }

    private static List<DrawCall> Draw(
        NinePatch patch,
        int x,
        int y,
        int width,
        int height)
    {
        var batch = new RecordingBatch();
        patch.Draw(batch.Batch, new RectangleInt(x, y, width, height));
        return batch.Calls;
    }

    private static List<DrawCall> Visible(
        List<DrawCall> calls)
    {
        return calls.FindAll(c => (c.Right - c.Left > Precision) && (c.Bottom - c.Top > Precision));
    }

    private static DrawCall SliceAt(
        List<DrawCall> calls,
        float sourceX,
        float sourceY)
    {
        return Assert.Single(calls, c => (c.Source!.Value.X == sourceX) && (c.Source.Value.Y == sourceY));
    }

    // The slices cover the rectangle exactly: they lie inside it, and their areas add up to it,
    // so they neither overlap nor leave a gap
    private static void AssertCovers(
        List<DrawCall> calls,
        float left,
        float top,
        float right,
        float bottom)
    {
        float area = 0;
        foreach (var call in calls)
        {
            Assert.InRange(call.Left, left - Precision, right + Precision);
            Assert.InRange(call.Right, left - Precision, right + Precision);
            Assert.InRange(call.Top, top - Precision, bottom + Precision);
            Assert.InRange(call.Bottom, top - Precision, bottom + Precision);
            area += (call.Right - call.Left) * (call.Bottom - call.Top);
        }

        float expected = (right - left) * (bottom - top);
        Assert.InRange(area, expected - 1, expected + 1);
    }

    // Slices that meet in the source meet on the screen, without a hairline between them
    private static void AssertNoSeams(
        List<DrawCall> calls)
    {
        foreach (var call in calls)
        {
            var source = call.Source!.Value;
            foreach (var other in calls)
            {
                var otherSource = other.Source!.Value;
                if ((otherSource.X == source.Right) && (otherSource.Y == source.Y))
                {
                    Assert.Equal(call.Right, other.Left, Precision);
                }

                if ((otherSource.Y == source.Bottom) && (otherSource.X == source.X))
                {
                    Assert.Equal(call.Bottom, other.Top, Precision);
                }
            }
        }
    }

    // One axis of an untrimmed patch: where a logical source coordinate lands, in a destination
    // of the given extent. Caps are fixed, and the part between them stretches.
    private static float MapAxis(
        float coordinate,
        float sourceExtent,
        float capNear,
        float capFar,
        float destinationExtent)
    {
        if (coordinate <= capNear)
        {
            return coordinate;
        }

        if (coordinate >= sourceExtent - capFar)
        {
            return destinationExtent - (sourceExtent - coordinate);
        }

        float centerScale = (destinationExtent - capNear - capFar) / (sourceExtent - capNear - capFar);
        return capNear + ((coordinate - capNear) * centerScale);
    }

    // Every slice of a trimmed source lands where those pixels would land untrimmed
    private static void AssertLandsLikeUntrimmed(
        ThicknessInt trim,
        ThicknessInt caps,
        int width,
        int height,
        float tolerance = Precision)
    {
        const int SourceX = 200;
        const int SourceY = 300;
        const int SourceSize = 100;

        var calls = Visible(Draw(new NinePatch(Trimmed(trim, SourceSize, SourceSize, SourceX, SourceY), caps), 0, 0, width, height));
        Assert.NotEmpty(calls);

        foreach (var call in calls)
        {
            var source = call.Source!.Value;
            float logicalLeft = source.X - SourceX;
            float logicalTop = source.Y - SourceY;

            Assert.Equal(MapAxis(logicalLeft, SourceSize, caps.Left, caps.Right, width), call.Left, tolerance);
            Assert.Equal(MapAxis(logicalLeft + source.Width, SourceSize, caps.Left, caps.Right, width), call.Right, tolerance);
            Assert.Equal(MapAxis(logicalTop, SourceSize, caps.Top, caps.Bottom, height), call.Top, tolerance);
            Assert.Equal(MapAxis(logicalTop + source.Height, SourceSize, caps.Top, caps.Bottom, height), call.Bottom, tolerance);
        }
    }

    // Slicing

    [Fact]
    public void Slices_FollowTheCaps_InTheSourcesTextureCoordinates()
    {
        // Caps of 10, 20, 30 and 40 leave a 60 x 40 center
        var patch = new NinePatch(Untrimmed(100, 100, 200, 300), new ThicknessInt(10, 20, 30, 40));

        var calls = Draw(patch, 0, 0, 100, 100);

        Assert.Equal(9, calls.Count);
        Assert.Equal(new Rectangle(200, 300, 10, 20), SliceAt(calls, 200, 300).Source);
        Assert.Equal(new Rectangle(210, 300, 60, 20), SliceAt(calls, 210, 300).Source);
        Assert.Equal(new Rectangle(270, 300, 30, 20), SliceAt(calls, 270, 300).Source);
        Assert.Equal(new Rectangle(200, 320, 10, 40), SliceAt(calls, 200, 320).Source);
        Assert.Equal(new Rectangle(210, 320, 60, 40), SliceAt(calls, 210, 320).Source);
        Assert.Equal(new Rectangle(270, 320, 30, 40), SliceAt(calls, 270, 320).Source);
        Assert.Equal(new Rectangle(200, 360, 10, 40), SliceAt(calls, 200, 360).Source);
        Assert.Equal(new Rectangle(210, 360, 60, 40), SliceAt(calls, 210, 360).Source);
        Assert.Equal(new Rectangle(270, 360, 30, 40), SliceAt(calls, 270, 360).Source);
    }

    [Fact]
    public void AtItsOwnSize_DrawsUnscaled()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(10, 20, 30, 40));

        var calls = Draw(patch, 7, 9, 100, 100);

        Assert.All(calls, c => Assert.Equal(Vector2.One, c.Scale));
        AssertCovers(calls, 7, 9, 107, 109);
    }

    [Fact]
    public void Stretching_KeepsCornersFixed_AndStretchesEdgesAlongOneAxis()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(10, 20, 30, 40));

        var calls = Draw(patch, 5, 6, 200, 160);

        // A 160 x 100 center from a 60 x 40 one
        float scaleX = 160f / 60;
        float scaleY = 100f / 40;

        var topLeft = SliceAt(calls, 0, 0);
        Assert.Equal(new Vector2(5, 6), topLeft.Position);
        Assert.Equal(Vector2.One, topLeft.Scale);

        var top = SliceAt(calls, 10, 0);
        Assert.Equal(new Vector2(15, 6), top.Position);
        Assert.Equal(new Vector2(scaleX, 1), top.Scale);

        var topRight = SliceAt(calls, 70, 0);
        Assert.Equal(new Vector2(175, 6), topRight.Position);
        Assert.Equal(Vector2.One, topRight.Scale);

        var left = SliceAt(calls, 0, 20);
        Assert.Equal(new Vector2(5, 26), left.Position);
        Assert.Equal(new Vector2(1, scaleY), left.Scale);

        var center = SliceAt(calls, 10, 20);
        Assert.Equal(new Vector2(15, 26), center.Position);
        Assert.Equal(new Vector2(scaleX, scaleY), center.Scale);

        var bottomRight = SliceAt(calls, 70, 60);
        Assert.Equal(new Vector2(175, 126), bottomRight.Position);
        Assert.Equal(Vector2.One, bottomRight.Scale);

        AssertCovers(calls, 5, 6, 205, 166);
    }

    [Fact]
    public void Draw_PassesTheTint_AndDefaultsToWhite()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(10));
        var batch = new RecordingBatch();

        patch.Draw(batch.Batch, new RectangleInt(0, 0, 50, 50), Color.Red);
        Assert.All(batch.Calls, c => Assert.Equal(Color.Red, c.Color));

        Assert.All(Draw(patch, 0, 0, 50, 50), c => Assert.Equal(Color.White, c.Color));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    [InlineData(-5, 50)]
    [InlineData(50, -5)]
    public void Draw_EmptyBounds_DrawsNothing(
        int width,
        int height)
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(10));

        Assert.Empty(Draw(patch, 0, 0, width, height));
    }

    [Fact]
    public void Draw_RejectsANullBatch_EvenWithEmptyBounds()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(10));

        Assert.Throws<ArgumentNullException>(() => patch.Draw(null!, default));
    }

    [Fact]
    public void Draw_OverAPlainTexture()
    {
        var patch = new NinePatch(TestTextures.Create(64, 64).AsSource(), new ThicknessInt(8));

        var calls = Draw(patch, 0, 0, 128, 128);

        Assert.Equal(new Rectangle(8, 8, 48, 48), SliceAt(calls, 8, 8).Source);
        AssertCovers(calls, 0, 0, 128, 128);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(101)]
    public void Stretched_Slices_HaveNoSeams_AtAnySize(
        int extra)
    {
        var patch = new NinePatch(Untrimmed(200, 150), new ThicknessInt(55, 33, 61, 47));

        var calls = Draw(patch, 3, 4, 200 + extra, 150 + extra);

        AssertNoSeams(calls);
        AssertCovers(calls, 3, 4, 203 + extra, 154 + extra);
    }

    // Fractional bounds and a cap scale

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void CapScale_FillsFractionalBounds_WithCapsAtThatScale(
        float capScale)
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(20));
        var batch = new RecordingBatch();

        patch.Draw(batch.Batch, new Rectangle(10.5f, 20.25f, 300, 200), Color.White, capScale);

        var calls = Visible(batch.Calls);
        Assert.Equal(10.5f, calls.Min(c => c.Left), Precision);
        Assert.Equal(310.5f, calls.Max(c => c.Right), Precision);
        var corner = calls.MinBy(c => c.Left + c.Top)!;
        Assert.Equal(20 * capScale, corner.Right - corner.Left, Precision);
    }

    [Fact]
    public void CapScale_TooLargeForTheBounds_ShrinksToFit()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(20));
        var batch = new RecordingBatch();

        patch.Draw(batch.Batch, new Rectangle(0, 0, 40, 40), Color.White, 4f);

        Assert.Equal(40f, Visible(batch.Calls).Max(c => c.Right), Precision);
    }

    // The empty center (review finding T9)

    [Theory]
    [InlineData(50, 0, 50, 0)]
    [InlineData(0, 50, 0, 50)]
    [InlineData(50, 50, 50, 50)]
    public void CapsFillingAnUntrimmedSource_AreRejected_OrLeaveNoHole(
        int left,
        int top,
        int right,
        int bottom)
    {
        NinePatch patch;
        try
        {
            patch = new NinePatch(Untrimmed(), new ThicknessInt(left, top, right, bottom));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return;
        }

        AssertCovers(Visible(Draw(patch, 0, 0, 300, 300)), 0, 0, 300, 300);
    }

    // Caps the source can't hold

    [Theory]
    [InlineData(150, 0, 0, 0)]
    [InlineData(60, 60, 60, 60)]
    [InlineData(0, 0, 0, 500)]
    public void CapsLargerThanTheSource_NeverSampleOutsideIt(
        int left,
        int top,
        int right,
        int bottom)
    {
        var source = new Rectangle(200, 300, 100, 100);
        var patch = new NinePatch(Untrimmed(100, 100, 200, 300), new ThicknessInt(left, top, right, bottom));

        foreach (var size in new[] { 10, 100, 300 })
        {
            foreach (var call in Visible(Draw(patch, 0, 0, size, size)))
            {
                Assert.True(source.Contains(call.Source!.Value), $"{call.Source} is outside the source at size {size}.");
            }
        }
    }

    // Shrinking

    [Fact]
    public void TooSmallForItsCaps_ScalesTheCornersUniformly()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(20));

        var calls = Draw(patch, 3, 4, 20, 20);

        // The 40 of caps fit in 20 at half size, leaving no center
        var topLeft = SliceAt(calls, 0, 0);
        Assert.Equal(new Vector2(3, 4), topLeft.Position);
        Assert.Equal(new Vector2(0.5f, 0.5f), topLeft.Scale);

        var bottomRight = SliceAt(calls, 80, 80);
        Assert.Equal(new Vector2(13, 14), bottomRight.Position);
        Assert.Equal(new Vector2(0.5f, 0.5f), bottomRight.Scale);

        AssertCovers(Visible(calls), 3, 4, 23, 24);
    }

    [Fact]
    public void TooSmallOnOneAxis_ScalesCornersByIt_AndStretchesTheOther()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(20));

        var calls = Draw(patch, 0, 0, 200, 20);

        Assert.Equal(new Vector2(0.5f, 0.5f), SliceAt(calls, 0, 0).Scale);

        // The caps are 10 wide now, so the center runs from 10 to 190
        var top = SliceAt(calls, 20, 0);
        Assert.Equal(10, top.Left, Precision);
        Assert.Equal(190, top.Right, Precision);
        Assert.Equal(0.5f, top.Scale.Y, Precision);

        AssertCovers(Visible(calls), 0, 0, 200, 20);
    }

    [Theory]
    [InlineData(29, 29)]
    [InlineData(57, 19)]
    [InlineData(101, 13)]
    [InlineData(115, 79)]
    public void TooSmall_AsymmetricCaps_LeaveNoSeams(
        int width,
        int height)
    {
        // Caps that round differently at each scale
        var patch = new NinePatch(Untrimmed(200, 150), new ThicknessInt(55, 33, 61, 47));

        AssertNoSeams(Visible(Draw(patch, 3, 4, width, height)));
    }

    [Fact]
    public void TooSmall_AThinCap_DoesNotCollapse()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(40, 40, 1, 40));

        var calls = Visible(Draw(patch, 0, 0, 20, 100));

        Assert.Contains(calls, c => (c.Source!.Value.X == 99) && (c.Right - c.Left >= 1 - Precision));
    }

    [Theory]
    [InlineData(10, 10, 3)]
    [InlineData(6, 6, 3)]
    public void TooSmall_StaysInsideTheBounds(
        int capLeft,
        int capRight,
        int width)
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(capLeft, 0, capRight, 0));

        foreach (var call in Visible(Draw(patch, 0, 0, width, 100)))
        {
            Assert.InRange(call.Right, 0, width + Precision);
        }
    }

    // Trimmed sources

    [Theory]
    [InlineData(10, 10, 10, 10, 200, 200)]
    [InlineData(5, 0, 15, 2, 240, 130)]
    [InlineData(20, 20, 20, 20, 100, 100)]
    [InlineData(0, 20, 0, 0, 300, 90)]
    public void TrimWithinTheCaps_LandsWhereTheUntrimmedPixelsWould(
        int trimLeft,
        int trimTop,
        int trimRight,
        int trimBottom,
        int width,
        int height)
    {
        AssertLandsLikeUntrimmed(new ThicknessInt(trimLeft, trimTop, trimRight, trimBottom), new ThicknessInt(20), width, height);
    }

    [Fact]
    public void TrimOfAWholeCap_DrawsTheRestInPlace()
    {
        // All the pixels are in the right cap, in a source trimmed down to them
        var source = Trimmed(new ThicknessInt(90, 0, 0, 0));

        var calls = Visible(Draw(new NinePatch(source, new ThicknessInt(20, 0, 20, 0)), 0, 0, 200, 100));

        var call = Assert.Single(calls);
        Assert.Equal(190, call.Left, Precision);
        Assert.Equal(200, call.Right, Precision);
    }

    [Theory]
    [InlineData(30, 20, 200)] // The frame is inside the center: its 40 pixels should stretch as part of the 60
    [InlineData(10, 0, 200)] // No caps: the whole source stretches, so its trim should too
    [InlineData(10, 0, 50)]
    public void TrimPastTheCaps_LandsWhereTheUntrimmedPixelsWould(
        int trim,
        int caps,
        int size)
    {
        AssertLandsLikeUntrimmed(new ThicknessInt(trim), new ThicknessInt(caps), size, size, tolerance: 1);
    }

    [Theory]
    [InlineData(10, 15)]
    [InlineData(30, 50)]
    public void TrimPastTheCaps_StillDrawsAtSmallSizes(
        int trim,
        int size)
    {
        var patch = new NinePatch(Trimmed(new ThicknessInt(trim)), new ThicknessInt(trim - 10));

        Assert.NotEmpty(Visible(Draw(patch, 0, 0, size, size)));
    }

    [Fact]
    public void Trimmed_TooSmall_ScalesThePaddingWithTheCaps()
    {
        // 10 of each 20 cap is trimmed; at half size the visible 5 start 5 in
        var patch = new NinePatch(Trimmed(new ThicknessInt(10)), new ThicknessInt(20));

        var calls = Visible(Draw(patch, 0, 0, 20, 20));

        var topLeft = SliceAt(calls, 210, 310);
        Assert.Equal(5, topLeft.Left, Precision);
        Assert.Equal(10, topLeft.Right, Precision);

        var bottomRight = SliceAt(calls, 280, 380);
        Assert.Equal(10, bottomRight.Left, Precision);
        Assert.Equal(15, bottomRight.Right, Precision);
    }

    // Outset

    [Fact]
    public void Outset_DrawsTheTextureOutsideTheBounds()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(30), new ThicknessInt(10));

        var calls = Draw(patch, 50, 50, 100, 100);

        // Layout is 100 x 100; the texture, its shadow included, covers 120 x 120
        AssertCovers(calls, 40, 40, 160, 160);
        Assert.Equal(new Vector2(1.5f, 1.5f), SliceAt(calls, 30, 30).Scale);
        Assert.Equal(new ThicknessInt(10), patch.Outset);
    }

    [Fact]
    public void Outset_TooSmall_ShrinksWithTheCaps()
    {
        var patch = new NinePatch(Untrimmed(), new ThicknessInt(30), new ThicknessInt(10));

        // The visual part of the caps is 40, so 20 is half size, with half the outset
        var calls = Visible(Draw(patch, 50, 50, 20, 20));

        AssertCovers(calls, 45, 45, 75, 75);
    }

    [Fact]
    public void Outset_WithTrim_AddsTheirOffsets()
    {
        var region = Trimmed(new ThicknessInt(4));
        var patch = new NinePatch(region, new ThicknessInt(30), new ThicknessInt(10));

        var calls = Draw(patch, 50, 50, 100, 100);

        // Out by the outset, back in by the trim
        AssertCovers(calls, 44, 44, 156, 156);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void Outset_RejectsNegativeSides(
        int left,
        int top,
        int right,
        int bottom)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new NinePatch(Untrimmed(), new ThicknessInt(10), new ThicknessInt(left, top, right, bottom)));
    }

    // Creating

    [Fact]
    public void Constructor_RejectsANullSource()
    {
        Assert.Throws<ArgumentNullException>(() => new NinePatch(null!, new ThicknessInt(1)));
    }

    [Fact]
    public void Constructor_TakesARegionsOutset_UnlessGivenOne()
    {
        var region = new TextureRegion(
            Page.AsSource(),
            new RectangleInt(0, 0, 100, 100),
            new RectangleInt(0, 0, 100, 100),
            caps: null,
            outset: new ThicknessInt(5));

        Assert.Equal(new ThicknessInt(5), new NinePatch(region, new ThicknessInt(10)).Outset);
        Assert.Equal(new ThicknessInt(2), new NinePatch(region, new ThicknessInt(10), new ThicknessInt(2)).Outset);
        Assert.Equal(ThicknessInt.Zero, new NinePatch(Untrimmed(), new ThicknessInt(10)).Outset);
        Assert.Same(region, new NinePatch(region, new ThicknessInt(10)).Source);
    }

    [Fact]
    public void FromRegion_UsesTheRegionsCapsAndOutset()
    {
        var rectangle = new RectangleInt(0, 0, 100, 100);
        var region = new TextureRegion(Page.AsSource(), rectangle, rectangle, new ThicknessInt(10), new ThicknessInt(3));

        var patch = NinePatch.FromRegion(region);

        Assert.Equal(new ThicknessInt(3), patch.Outset);
        Assert.Equal(new Rectangle(10, 10, 80, 80), SliceAt(Draw(patch, 0, 0, 100, 100), 10, 10).Source);
    }

    [Fact]
    public void FromRegion_RejectsARegionWithoutCaps_AndNull()
    {
        Assert.Throws<InvalidOperationException>(() => NinePatch.FromRegion(Untrimmed()));
        Assert.Throws<ArgumentNullException>(() => NinePatch.FromRegion(null!));
    }
}

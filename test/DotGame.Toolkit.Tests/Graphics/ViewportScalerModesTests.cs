// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class ViewportScalerModesTests
{
    private static readonly Vector2Int Design = new(100, 200);

    private static ViewportScaler Update(
        ViewportScaleMode mode,
        int width,
        int height)
    {
        var scaler = new ViewportScaler(Design, mode);
        scaler.Update(new RectangleInt(0, 0, width, height));
        return scaler;
    }

    [Fact]
    public void Fit_ShowsTheWholeDesign_BetweenBars()
    {
        var scaler = Update(ViewportScaleMode.Fit, 400, 400);

        Assert.Equal(new Vector2(2), scaler.Scale);
        Assert.Equal(new RectangleInt(100, 0, 200, 400), scaler.Viewport);
        Assert.Equal(new Rectangle(0, 0, 100, 200), scaler.VisibleBounds);
    }

    [Fact]
    public void Fill_ShowsLessOfTheDesign_Centered()
    {
        var scaler = Update(ViewportScaleMode.Fill, 400, 400);

        Assert.Equal(new Vector2(4), scaler.Scale);
        Assert.Equal(new RectangleInt(0, 0, 400, 400), scaler.Viewport);
        Assert.Equal(new Rectangle(0, 50, 100, 100), scaler.VisibleBounds);
    }

    [Fact]
    public void Stretch_ShowsTheWholeDesign_AtItsShape()
    {
        var scaler = Update(ViewportScaleMode.Stretch, 400, 400);

        Assert.Equal(new Vector2(4, 2), scaler.Scale);
        Assert.Equal(new Rectangle(0, 0, 100, 200), scaler.VisibleBounds);
    }

    [Theory]
    [InlineData(ViewportScaleMode.Expand, 400, 400, 0, 0, 400, 400, 200, 200)]
    [InlineData(ViewportScaleMode.Expand, 200, 1000, 0, 0, 200, 1000, 100, 500)]
    [InlineData(ViewportScaleMode.ExpandVertical, 200, 1000, 0, 0, 200, 1000, 100, 500)]
    [InlineData(ViewportScaleMode.ExpandVertical, 400, 400, 100, 0, 200, 400, 100, 200)]
    [InlineData(ViewportScaleMode.ExpandHorizontal, 400, 400, 0, 0, 400, 400, 200, 200)]
    [InlineData(ViewportScaleMode.ExpandHorizontal, 200, 1000, 0, 300, 200, 400, 100, 200)]
    public void Expanding_ShowsMoreOfTheWorld_OnlyOnItsSides(
        ViewportScaleMode mode,
        int width,
        int height,
        int viewportX,
        int viewportY,
        int viewportWidth,
        int viewportHeight,
        float visibleWidth,
        float visibleHeight)
    {
        var scaler = Update(mode, width, height);

        Assert.Equal(new RectangleInt(viewportX, viewportY, viewportWidth, viewportHeight), scaler.Viewport);
        Assert.Equal(new Rectangle(0, 0, visibleWidth, visibleHeight), scaler.VisibleBounds);
    }

    [Fact]
    public void Expanding_KeepsTheWholeDesign_FromItsTopLeft()
    {
        var scaler = Update(ViewportScaleMode.Expand, 400, 400);

        Assert.Equal(new Vector2(2), scaler.Scale);
        Assert.Equal(Vector2.Zero, Vector2.Transform(Vector2.Zero, scaler.Transform));
        Assert.Equal(new Vector2(200, 400), Vector2.Transform(new Vector2(100, 200), scaler.Transform));
    }

    [Fact]
    public void Expanding_InsideASafeArea_StartsAtItsTopLeft()
    {
        var scaler = new ViewportScaler(Design, ViewportScaleMode.ExpandVertical);

        scaler.Update(new RectangleInt(0, 50, 200, 950));

        Assert.Equal(new Vector2(0, 50), Vector2.Transform(Vector2.Zero, scaler.Transform));
        Assert.Equal(new Rectangle(0, 0, 100, 475), scaler.VisibleBounds);
    }

    [Fact]
    public void AScreenWithNoSize_ShowsNothing()
    {
        var scaler = Update(ViewportScaleMode.Expand, 0, 0);

        Assert.Equal(default, scaler.VisibleBounds);
    }
}

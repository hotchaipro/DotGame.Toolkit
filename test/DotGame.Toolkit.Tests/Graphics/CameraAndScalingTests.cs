// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class CameraAndScalingTests
{
    private const float Tolerance = 1e-3f;

    private static void AssertNear(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
    }

    // ── Camera ───────────────────────────────────────────────────────

    [Fact]
    public void Camera_ByDefault_ChangesNothing()
    {
        Assert.Equal(Matrix3x2.Identity, new Camera().Transform);
    }

    [Fact]
    public void Camera_PutsItsPositionAtItsOrigin()
    {
        var camera = new Camera { Position = new Vector2(100, 50), Origin = new Vector2(400, 300), Zoom = 2, Rotation = Angle.FromDegrees(30) };

        AssertNear(new Vector2(400, 300), camera.WorldToScreen(new Vector2(100, 50)));
    }

    [Fact]
    public void Camera_Zoom_MagnifiesAroundTheOrigin()
    {
        var camera = new Camera { Origin = new Vector2(10, 10), Zoom = 2 };

        AssertNear(new Vector2(30, 10), camera.WorldToScreen(new Vector2(10, 0)));
    }

    [Fact]
    public void Camera_Rotation_TurnsTheWorldTheOtherWay()
    {
        var camera = new Camera { Rotation = Angle.QuarterCircle };

        AssertNear(new Vector2(0, -10), camera.WorldToScreen(new Vector2(10, 0)));
    }

    [Fact]
    public void Camera_ScreenToWorld_IsTheInverseOfWorldToScreen()
    {
        var camera = new Camera { Position = new Vector2(3, 7), Origin = new Vector2(50, 60), Zoom = 1.5f, Rotation = Angle.FromDegrees(70) };
        var world = new Vector2(-20, 41);

        AssertNear(world, camera.ScreenToWorld(camera.WorldToScreen(world)));
    }

    [Fact]
    public void Camera_FollowsChanges()
    {
        var camera = new Camera();
        _ = camera.Transform;

        camera.Position = new Vector2(5, 0);

        AssertNear(new Vector2(-5, 0), camera.WorldToScreen(Vector2.Zero));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Camera_RefusesAZoomThatIsNotPositive(float zoom)
    {
        var camera = new Camera();

        Assert.Throws<ArgumentOutOfRangeException>(() => camera.Zoom = zoom);
    }

    // ── ViewportScaler ─────────────────────────────────────────────────

    private static ViewportScaler Scaled(
        ViewportScaleMode mode,
        int width,
        int height)
    {
        var scaler = new ViewportScaler(new Vector2Int(400, 300), mode);
        scaler.Update(new RectangleInt(0, 0, width, height));
        return scaler;
    }

    [Fact]
    public void Fit_ShowsAllOfIt_WithBarsOnTheSidesLeftOver()
    {
        var scaler = Scaled(ViewportScaleMode.Fit, 1000, 600);

        AssertNear(new Vector2(2, 2), scaler.Scale);
        AssertNear(new Vector2(100, 0), scaler.DesignToScreen(Vector2.Zero));
        AssertNear(new Vector2(900, 600), scaler.DesignToScreen(new Vector2(400, 300)));
        Assert.Equal(new RectangleInt(100, 0, 800, 600), scaler.Viewport);
    }

    [Fact]
    public void Fill_CoversTheScreen_CutOffOnTheOtherSides()
    {
        var scaler = Scaled(ViewportScaleMode.Fill, 1000, 600);

        AssertNear(new Vector2(2.5f, 2.5f), scaler.Scale);
        Assert.Equal(new RectangleInt(0, 0, 1000, 600), scaler.Viewport);
        Assert.Equal(-75f, scaler.DesignToScreen(Vector2.Zero).Y, Tolerance);
    }

    [Fact]
    public void Stretch_FillsItExactly()
    {
        var scaler = Scaled(ViewportScaleMode.Stretch, 1000, 600);

        AssertNear(new Vector2(2.5f, 2), scaler.Scale);
        AssertNear(Vector2.Zero, scaler.DesignToScreen(Vector2.Zero));
        AssertNear(new Vector2(1000, 600), scaler.DesignToScreen(new Vector2(400, 300)));
    }

    [Fact]
    public void IntegerFit_UsesWholePixels_Centered()
    {
        var scaler = Scaled(ViewportScaleMode.IntegerFit, 1000, 700);

        AssertNear(new Vector2(2, 2), scaler.Scale);
        Assert.Equal(new RectangleInt(100, 50, 800, 600), scaler.Viewport);
    }

    [Fact]
    public void IntegerFit_OnASmallerScreen_StaysAtOne()
    {
        var scaler = Scaled(ViewportScaleMode.IntegerFit, 200, 100);

        AssertNear(Vector2.One, scaler.Scale);
        Assert.Equal(new RectangleInt(0, 0, 200, 100), scaler.Viewport);
    }

    [Fact]
    public void AScreenThatDoesNotStartAtZero_IsFittedInPlace()
    {
        var scaler = new ViewportScaler(new Vector2Int(400, 300));

        scaler.Update(new RectangleInt(20, 40, 400, 300));

        AssertNear(new Vector2(20, 40), scaler.DesignToScreen(Vector2.Zero));
        Assert.Equal(new RectangleInt(20, 40, 400, 300), scaler.Viewport);
    }

    [Theory]
    [InlineData(ViewportScaleMode.Fit)]
    [InlineData(ViewportScaleMode.Fill)]
    [InlineData(ViewportScaleMode.Stretch)]
    [InlineData(ViewportScaleMode.IntegerFit)]
    public void ScreenToDesign_IsTheInverseOfDesignToScreen(ViewportScaleMode mode)
    {
        var scaler = Scaled(mode, 1037, 613);
        var design = new Vector2(123, 45);

        AssertNear(design, scaler.ScreenToDesign(scaler.DesignToScreen(design)));
    }

    [Fact]
    public void AnEmptyScreen_LeavesAnEmptyViewport_WithoutFailing()
    {
        var scaler = Scaled(ViewportScaleMode.Fit, 0, 0);

        Assert.True(scaler.Viewport.IsEmpty);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void ADesignSizeThatIsNotPositive_IsRefused(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportScaler(new Vector2Int(width, height)));
    }

    [Fact]
    public void ACameraAndAScaler_Compose()
    {
        var camera = new Camera { Position = new Vector2(10, 0) };
        var scaler = Scaled(ViewportScaleMode.Fit, 800, 600);

        AssertNear(new Vector2(20, 0), Vector2.Transform(new Vector2(20, 0), camera.Transform * scaler.Transform));
    }
}

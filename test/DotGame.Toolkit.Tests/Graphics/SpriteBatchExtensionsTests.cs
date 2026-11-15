// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

/// <summary>
/// Trim maths: how a source's logical rectangle and origin become the texture rectangle and
/// origin the framework draws.
/// </summary>
public sealed class SpriteBatchExtensionsTests
{
    private static readonly Texture Page = TestTextures.Create(512, 512);

    // A 50 x 50 sprite at (100, 100), with 10 trimmed off its left, 20 off its top, 10 off its
    // right and 10 off its bottom
    private static readonly TextureRegion TrimmedRegion = new(
        Page.AsSource(),
        new RectangleInt(100, 100, 50, 50),
        new RectangleInt(110, 120, 30, 20));

    private static DrawCall DrawOne(
        ITextureSource source,
        Vector2 position = default,
        Vector2 origin = default,
        Vector2? scale = null,
        Rectangle? sourceRectangle = null,
        SpriteFlip flip = SpriteFlip.None)
    {
        var batch = new RecordingBatch();
        batch.Batch.Draw(source, position, Color.White, default, origin, scale, sourceRectangle, flip);
        return Assert.Single(batch.Calls);
    }

    // Whole sources

    [Fact]
    public void Draw_PassesEverythingThrough()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(10, 20, 30, 40));
        var batch = new RecordingBatch();
        var rotation = Angle.FromDegrees(30);

        batch.Batch.Draw(region, new Vector2(1, 2), Color.Red, rotation, new Vector2(3, 4), new Vector2(5, 6));

        var call = Assert.Single(batch.Calls);
        Assert.Same(Page, call.Texture);
        Assert.Equal(new Vector2(1, 2), call.Position);
        Assert.Equal(Color.Red, call.Color);
        Assert.Equal(rotation, call.Rotation);
        Assert.Equal(new Vector2(3, 4), call.Origin);
        Assert.Equal(new Vector2(5, 6), call.Scale);
        Assert.Equal(new Rectangle(10, 20, 30, 40), call.Source);
    }

    [Fact]
    public void Draw_WithoutAScale_DrawsAtOne()
    {
        Assert.Equal(Vector2.One, DrawOne(TrimmedRegion).Scale);
    }

    [Fact]
    public void Draw_RejectsNullArguments()
    {
        var batch = new RecordingBatch();

        Assert.Throws<ArgumentNullException>(() => SpriteBatchExtensions.Draw(null!, TrimmedRegion, Vector2.Zero, Color.White));
        Assert.Throws<ArgumentNullException>(() => batch.Batch.Draw((ITextureSource)null!, Vector2.Zero, Color.White));
    }

    [Fact]
    public void Draw_OutsideABatch_Throws()
    {
        var recording = new RecordingBatch();
        recording.Scope.Dispose();

        Assert.Throws<InvalidOperationException>(() => recording.Batch.Draw(TrimmedRegion, Vector2.Zero, Color.White));
    }

    [Fact]
    public void AsSource_CoversTheWholeTexture()
    {
        var texture = TestTextures.Create(64, 32);

        var source = texture.AsSource();

        Assert.Same(texture, source.Texture);
        Assert.Equal(new RectangleInt(0, 0, 64, 32), source.PackedFrame);
        Assert.Equal(new RectangleInt(0, 0, 64, 32), source.Bounds);
        Assert.Throws<ArgumentNullException>(() => ((Texture)null!).AsSource());
    }

    // Trim

    [Fact]
    public void Trimmed_DrawsItsFrame_WithThePixelsWhereTheyWereBeforeTrimming()
    {
        var call = DrawOne(TrimmedRegion, new Vector2(1000, 2000));

        Assert.Equal(new Rectangle(110, 120, 30, 20), call.Source);
        Assert.Equal(1010, call.Left);
        Assert.Equal(2020, call.Top);
    }

    [Fact]
    public void Trimmed_OriginIsInLogicalPixels()
    {
        // Pivoting on the center of the 50 x 50 sprite
        var call = DrawOne(TrimmedRegion, new Vector2(1000, 2000), origin: new Vector2(25, 25));

        Assert.Equal(new Vector2(15, 5), call.Origin);
        Assert.Equal(985, call.Left);
        Assert.Equal(1995, call.Top);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(-2, -0.5f)]
    public void Trimmed_ScaleKeepsEveryLogicalPixelInPlace(
        float scaleX,
        float scaleY)
    {
        var position = new Vector2(1000, 2000);
        var origin = new Vector2(25, 25);
        var scale = new Vector2(scaleX, scaleY);

        var call = DrawOne(TrimmedRegion, position, origin, scale);

        // The frame's corner is logical pixel (10, 20): it lands where the logical rectangle
        // scaled about the origin puts it, mirrored or not
        var expected = position + (scale * (new Vector2(10, 20) - origin));
        Assert.Equal(expected.X, call.Left);
        Assert.Equal(expected.Y, call.Top);
    }

    [Fact]
    public void Untrimmed_LeavesTheOriginAlone()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(10, 20, 30, 40));

        Assert.Equal(new Vector2(3, 4), DrawOne(region, origin: new Vector2(3, 4)).Origin);
    }

    [Fact]
    public void CustomSource_IsDrawnThroughItsFrame()
    {
        var source = new TestSource(Page, new RectangleInt(0, 0, 20, 20), new RectangleInt(5, 5, 10, 10));

        var call = DrawOne(source);

        Assert.Equal(new Rectangle(5, 5, 10, 10), call.Source);
        Assert.Equal(new Vector2(-5, -5), call.Origin);
    }

    private sealed class TestSource(
        Texture texture,
        RectangleInt sourceRectangle,
        RectangleInt frame) : ITextureSource
    {
        public Texture Texture { get; } = texture;

        public RectangleInt PackedFrame { get; } = frame;

        public RectangleInt Bounds { get; } = sourceRectangle;
    }

    // A sub-rectangle

    [Theory]
    [InlineData(5f, 5f, 0f, 0f)]
    [InlineData(5f, 5f, 10f, 0f)]
    [InlineData(5f, 5f, -10f, 10f)]
    [InlineData(5f, 5f, 10f, -10f)]
    public void EmptyRectangles_DrawNothing(
        float x,
        float y,
        float width,
        float height)
    {
        var batch = new RecordingBatch();
        var empty = new Rectangle(x, y, width, height);

        batch.Batch.FillRectangle(empty, Color.White);
        batch.Batch.DrawRectangle(empty, Color.White);

        Assert.Empty(batch.Calls);
    }

    [Fact]
    public void SubRectangle_IsInTheSourcesLogicalCoordinates()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(100, 200, 50, 50));

        var call = DrawOne(region, origin: new Vector2(1, 2), sourceRectangle: new Rectangle(5, 6, 7, 8));

        Assert.Equal(new Rectangle(105, 206, 7, 8), call.Source);
        Assert.Equal(new Vector2(1, 2), call.Origin);
    }

    [Fact]
    public void SubRectangle_InsideTheFrame_IsUnclipped()
    {
        var call = DrawOne(TrimmedRegion, sourceRectangle: new Rectangle(15, 25, 5, 5));

        Assert.Equal(new Rectangle(115, 125, 5, 5), call.Source);
        Assert.Equal(Vector2.Zero, call.Origin);
    }

    [Fact]
    public void SubRectangle_ClippedByTheFrame_KeepsThePixelsInPlace()
    {
        // From logical (5, 5) to (15, 35): the frame starts at (10, 20), so 5 and 15 are clipped
        var call = DrawOne(TrimmedRegion, new Vector2(1000, 2000), sourceRectangle: new Rectangle(5, 5, 10, 30));

        Assert.Equal(new Rectangle(110, 120, 5, 15), call.Source);
        Assert.Equal(new Vector2(-5, -15), call.Origin);
        Assert.Equal(1005, call.Left);
        Assert.Equal(2015, call.Top);
    }

    [Fact]
    public void SubRectangle_ClippedOnItsFarSides_KeepsItsOrigin()
    {
        var call = DrawOne(TrimmedRegion, sourceRectangle: new Rectangle(30, 30, 20, 20));

        Assert.Equal(new Rectangle(130, 130, 10, 10), call.Source);
        Assert.Equal(Vector2.Zero, call.Origin);
    }

    [Theory]
    [InlineData(0, 0, 5, 50)] // Left of the frame
    [InlineData(0, 0, 50, 10)] // Above it
    [InlineData(40, 0, 10, 50)] // Right of it
    [InlineData(0, 40, 50, 10)] // Below it
    [InlineData(0, 0, 10, 20)] // Touching its corner
    public void SubRectangle_InTheTrimmedAwayPart_DrawsNothing(
        float x,
        float y,
        float width,
        float height)
    {
        var batch = new RecordingBatch();

        batch.Batch.Draw(TrimmedRegion, Vector2.Zero, Color.White, sourceRectangle: new Rectangle(x, y, width, height));

        Assert.Empty(batch.Calls);
    }

    // A sprite animation player's frame

    [Fact]
    public void Player_DrawsItsFrame_PivotingOnTheLogicalCenterByDefault()
    {
        var player = new SpriteAnimationPlayer(new SpriteAnimation([TrimmedRegion], 10));
        var batch = new RecordingBatch();

        batch.Batch.Draw(player, new Vector2(1000, 2000), Color.Red, origin: (Vector2)player.Animation!.Size / 2f);

        var call = Assert.Single(batch.Calls);
        Assert.Equal(Color.Red, call.Color);
        Assert.Equal(new Rectangle(110, 120, 30, 20), call.Source);
        Assert.Equal(985, call.Left);
        Assert.Equal(1995, call.Top);
    }

    [Fact]
    public void Player_Mirrored_StaysInPlace()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(0, 0, 40, 40));
        var player = new SpriteAnimationPlayer(new SpriteAnimation([region], 10));
        var batch = new RecordingBatch();

        batch.Batch.Draw(player, new Vector2(100, 100), Color.White, origin: new Vector2(20, 20), scale: new Vector2(-1, 1));

        var call = Assert.Single(batch.Calls);
        Assert.Equal(120, call.Left);
        Assert.Equal(80, call.Right);
    }

    [Fact]
    public void Player_WithoutAnOrigin_DrawsFromTheTopLeft_AsOtherDrawsDo()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(0, 0, 40, 40));
        var player = new SpriteAnimationPlayer(new SpriteAnimation([region], 10));
        var batch = new RecordingBatch();

        batch.Batch.Draw(player, new Vector2(100, 100), Color.White);

        var call = Assert.Single(batch.Calls);
        Assert.Equal(100, call.Left);
        Assert.Equal(100, call.Top);
    }

    [Fact]
    public void Player_WithAnOrigin_UsesIt()
    {
        var region = new TextureRegion(Page.AsSource(), new RectangleInt(0, 0, 40, 40));
        var player = new SpriteAnimationPlayer(new SpriteAnimation([region], 10));
        var batch = new RecordingBatch();

        batch.Batch.Draw(player, Vector2.Zero, Color.White, origin: new Vector2(3, 4));

        Assert.Equal(new Vector2(3, 4), Assert.Single(batch.Calls).Origin);
    }

    [Fact]
    public void Player_WithoutAnAnimation_DrawsNothing()
    {
        var batch = new RecordingBatch();

        batch.Batch.Draw(new SpriteAnimationPlayer(), Vector2.Zero, Color.White);

        Assert.Empty(batch.Calls);
    }

    [Fact]
    public void Player_RejectsNullArguments()
    {
        var player = new SpriteAnimationPlayer();

        Assert.Throws<ArgumentNullException>(() => SpriteAnimationExtensions.Draw(null!, player, Vector2.Zero, Color.White));
        Assert.Throws<ArgumentNullException>(() => new RecordingBatch().Batch.Draw((SpriteAnimationPlayer)null!, Vector2.Zero, Color.White));
    }

    // Regression: the sample's "Turn" made its sprite jump sideways, because it flipped about the
    // default origin, the frame's top-left corner, not its center

    [Theory]
    [InlineData(2)]
    [InlineData(-2)]
    public void Player_FlippedAboutItsCenter_StaysInPlace(
        float scaleX)
    {
        // The trimmed frame is centered across its 50-pixel logical width (10 trimmed off each side)
        var player = new SpriteAnimationPlayer(new SpriteAnimation(new[] { TrimmedRegion }, 10));
        var position = new Vector2(1000, 2000);
        var batch = new RecordingBatch();

        batch.Batch.Draw(player, position, Color.White, origin: (Vector2)player.Frame!.Size * 0.5f, scale: new Vector2(scaleX, 2));

        var call = Assert.Single(batch.Calls);
        Assert.Equal(position.X, (call.Left + call.Right) / 2);
    }

    // Flips

    [Fact]
    public void Trimmed_Flipped_MirrorsTheFrameInsideItsLogicalRectangle()
    {
        // 20 trimmed off the top and 10 off the bottom: flipped top to bottom, the frame sits 10
        // from the top
        var call = DrawOne(TrimmedRegion, new Vector2(1000, 2000), flip: SpriteFlip.Vertical);

        Assert.Equal(SpriteFlip.Vertical, call.Flip);
        Assert.Equal(new Rectangle(110, 120, 30, 20), call.Source);
        Assert.Equal(1010, call.Left);
        Assert.Equal(2010, call.Top);
    }

    [Fact]
    public void SubRectangle_Flipped_MirrorsTheFrameInsideIt()
    {
        var call = DrawOne(TrimmedRegion, new Vector2(1000, 2000), sourceRectangle: new Rectangle(0, 0, 50, 50), flip: SpriteFlip.Vertical);

        Assert.Equal(2010, call.Top);
    }

    [Fact]
    public void Region_And_Player_PassTheFlipOn()
    {
        var batch = new RecordingBatch();
        var player = new SpriteAnimationPlayer(new SpriteAnimation(new[] { TrimmedRegion }, 10));

        batch.Batch.Draw(TrimmedRegion, new Vector2(1000, 2000), Color.White, flip: SpriteFlip.Vertical);
        batch.Batch.Draw(player, new Vector2(1000, 2000), Color.White, flip: SpriteFlip.Both);

        Assert.Equal(SpriteFlip.Vertical, batch.Calls[0].Flip);
        Assert.Equal(2010, batch.Calls[0].Top);
        Assert.Equal(SpriteFlip.Both, batch.Calls[1].Flip);
        Assert.Equal(2010, batch.Calls[1].Top);
    }
}

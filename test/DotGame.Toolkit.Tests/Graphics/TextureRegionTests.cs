// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class TextureRegionTests
{
    private static readonly Texture Page = TestTextures.Create(512, 256);

    private static readonly ITextureSource PageSource = Page.AsSource();

    // Over a plain texture

    [Fact]
    public void Region_ViewsAPartOfTheTexture()
    {
        var rectangle = new RectangleInt(10, 20, 30, 40);

        var region = new TextureRegion(PageSource, rectangle);

        Assert.Same(Page, region.Texture);
        Assert.Equal(rectangle, region.Bounds);
        Assert.Equal(rectangle, region.PackedFrame);
        Assert.Equal(new Vector2Int(30, 40), region.Size);
        Assert.Null(region.Caps);
        Assert.Null(region.Outset);
    }

    [Fact]
    public void Region_MayCoverTheWholeTexture()
    {
        var bounds = new RectangleInt(0, 0, 512, 256);

        Assert.Equal(bounds, new TextureRegion(PageSource, bounds).PackedFrame);
    }

    [Theory]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(0, 0, -1, 10)]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, -1, 10, 10)]
    [InlineData(503, 0, 10, 10)]
    [InlineData(0, 247, 10, 10)]
    public void Region_RejectsARectangleThatIsEmptyOrOutsideTheTexture(
        int x,
        int y,
        int width,
        int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureRegion(PageSource, new RectangleInt(x, y, width, height)));
    }

    [Fact]
    public void Constructors_RejectANullSource()
    {
        var rectangle = new RectangleInt(0, 0, 1, 1);

        Assert.Throws<ArgumentNullException>(() => new TextureRegion(null!, rectangle));
        Assert.Throws<ArgumentNullException>(() => new TextureRegion(null!, rectangle, rectangle));
    }

    // Trimmed

    [Fact]
    public void Trimmed_KeepsItsLogicalRectangleAndFrame()
    {
        var logical = new RectangleInt(100, 100, 50, 60);
        var frame = new RectangleInt(110, 120, 20, 30);

        var region = new TextureRegion(PageSource, logical, frame);

        Assert.Equal(logical, region.Bounds);
        Assert.Equal(frame, region.PackedFrame);
        Assert.Equal(new Vector2Int(50, 60), region.Size);
    }

    [Fact]
    public void Trimmed_LogicalRectangle_MayExtendPastTheTexture()
    {
        // A sprite packed against the page's corner has only its frame on the page
        var region = new TextureRegion(PageSource, new RectangleInt(-5, -5, 20, 20), new RectangleInt(0, 0, 10, 10));

        Assert.Equal(new Vector2Int(20, 20), region.Size);
    }

    [Theory]
    [InlineData(0, 0, 0, 10)] // Empty
    [InlineData(0, 0, 10, 0)]
    [InlineData(95, 0, 10, 10)] // Past the logical rectangle
    [InlineData(-1, 0, 10, 10)]
    public void Trimmed_RejectsAFrameThatIsEmptyOrOutsideTheLogicalRectangle(
        int x,
        int y,
        int width,
        int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextureRegion(PageSource, new RectangleInt(0, 0, 100, 100), new RectangleInt(x, y, width, height)));
    }

    [Fact]
    public void Trimmed_RejectsAnEmptyLogicalRectangle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextureRegion(PageSource, new RectangleInt(0, 0, 0, 10), new RectangleInt(0, 0, 1, 1)));
    }

    [Fact]
    public void Trimmed_RejectsAFrameOutsideTheTexture()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextureRegion(PageSource, new RectangleInt(500, 0, 100, 100), new RectangleInt(505, 0, 10, 10)));
    }

    [Fact]
    public void Metadata_IsKept()
    {
        var rectangle = new RectangleInt(0, 0, 100, 100);
        var caps = new ThicknessInt(1, 2, 3, 4);
        var outset = new ThicknessInt(5, 6, 7, 8);

        Assert.Equal(caps, new TextureRegion(PageSource, rectangle, rectangle, caps).Caps);

        var region = new TextureRegion(PageSource, rectangle, rectangle, caps, outset);
        Assert.Equal(caps, region.Caps);
        Assert.Equal(outset, region.Outset);
    }

    // Regions of regions

    [Fact]
    public void Nested_ComposesWithItsParent_AndPointsAtTheTexture()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 50, 80, 80));

        var region = new TextureRegion(parent, new RectangleInt(10, 20, 30, 40));

        Assert.Same(Page, region.Texture);
        Assert.Equal(new RectangleInt(110, 70, 30, 40), region.Bounds);
        Assert.Equal(new RectangleInt(110, 70, 30, 40), region.PackedFrame);
    }

    [Theory]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, -1, 10, 10)]
    [InlineData(71, 0, 10, 10)]
    [InlineData(0, 71, 10, 10)]
    public void Nested_RejectsARectangleOutsideItsParent(
        int x,
        int y,
        int width,
        int height)
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 50, 80, 80));

        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureRegion(parent, new RectangleInt(x, y, width, height)));
    }

    [Fact]
    public void Nested_OverATrimmedParent_ClipsItsFrameToThePixels()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 100, 50, 50), new RectangleInt(110, 120, 20, 20));

        var region = new TextureRegion(parent, new RectangleInt(0, 0, 25, 25));

        // Logically where it was asked for, with only the pixels that exist
        Assert.Equal(new RectangleInt(100, 100, 25, 25), region.Bounds);
        Assert.Equal(new RectangleInt(110, 120, 15, 5), region.PackedFrame);
    }

    [Fact]
    public void Nested_InATrimmedAwayPart_IsRejected()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 100, 50, 50), new RectangleInt(110, 120, 20, 20));

        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureRegion(parent, new RectangleInt(0, 0, 5, 5)));
    }

    [Fact]
    public void NestedTrimmed_ComposesBothRectangles()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 50, 80, 80));

        var region = new TextureRegion(parent, new RectangleInt(10, 10, 40, 40), new RectangleInt(15, 20, 10, 5));

        Assert.Equal(new RectangleInt(110, 60, 40, 40), region.Bounds);
        Assert.Equal(new RectangleInt(115, 70, 10, 5), region.PackedFrame);
    }

    [Fact]
    public void NestedTrimmed_OverATrimmedParent_ClipsItsFrame()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 100, 50, 50), new RectangleInt(110, 120, 20, 20));

        var region = new TextureRegion(parent, new RectangleInt(0, 0, 40, 40), new RectangleInt(5, 15, 30, 10));

        Assert.Equal(new RectangleInt(110, 120, 20, 5), region.PackedFrame);
    }

    [Fact]
    public void NestedTrimmed_RejectsAFrameInATrimmedAwayPart_OrOutsideTheParent()
    {
        var parent = new TextureRegion(PageSource, new RectangleInt(100, 100, 50, 50), new RectangleInt(110, 120, 20, 20));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextureRegion(parent, new RectangleInt(0, 0, 40, 40), new RectangleInt(0, 0, 5, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TextureRegion(parent, new RectangleInt(0, 0, 100, 100), new RectangleInt(45, 45, 10, 10)));
    }

    [Fact]
    public void Region_OfACustomSource_IsInTheSourcesCoordinates()
    {
        var source = new OffsetSource(Page, new RectangleInt(100, 100, 50, 50));

        var region = new TextureRegion(source, new RectangleInt(10, 20, 30, 20));

        Assert.Same(Page, region.Texture);
        Assert.Equal(new RectangleInt(110, 120, 30, 20), region.Bounds);
        Assert.Equal(new RectangleInt(110, 120, 30, 20), region.PackedFrame);
    }

    private sealed class OffsetSource(
        Texture texture,
        RectangleInt rectangle) : ITextureSource
    {
        public Texture Texture { get; } = texture;

        public RectangleInt PackedFrame { get; } = rectangle;

        public RectangleInt Bounds { get; } = rectangle;
    }

    // Outset

    [Fact]
    public void WithOutset_DeflatesTheLogicalRectangle_AndKeepsTheFrame()
    {
        var rectangle = new RectangleInt(10, 20, 100, 100);
        var region = new TextureRegion(PageSource, rectangle);

        var outset = region.WithOutset(new ThicknessInt(1, 2, 3, 4));

        Assert.Equal(new RectangleInt(11, 22, 96, 94), outset.Bounds);
        Assert.Equal(rectangle, outset.PackedFrame);
        Assert.Equal(new Vector2Int(96, 94), outset.Size);
        Assert.Same(Page, outset.Texture);
    }

    [Fact]
    public void WithOutset_DrawsThePixelsOutsideTheLogicalBounds()
    {
        var region = new TextureRegion(PageSource, new RectangleInt(10, 20, 100, 100)).WithOutset(new ThicknessInt(5));
        var batch = new RecordingBatch();

        batch.Batch.Draw(region, new System.Numerics.Vector2(50, 60), Color.White);

        var call = Assert.Single(batch.Calls);
        Assert.Equal(new Rectangle(10, 20, 100, 100), call.Source);
        Assert.Equal(45, call.Left);
        Assert.Equal(55, call.Top);
        Assert.Equal(145, call.Right);
    }

    [Fact]
    public void WithOutset_Zero_IsTheSameRegion()
    {
        var region = new TextureRegion(PageSource, new RectangleInt(10, 20, 100, 100));

        Assert.Same(region, region.WithOutset(ThicknessInt.Zero));
    }

    [Fact]
    public void WithOutset_DropsCapsAndOutset()
    {
        var rectangle = new RectangleInt(0, 0, 100, 100);
        var region = new TextureRegion(PageSource, rectangle, rectangle, new ThicknessInt(1), new ThicknessInt(1));

        var outset = region.WithOutset(new ThicknessInt(1));

        Assert.Null(outset.Caps);
        Assert.Null(outset.Outset);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(50, 0, 50, 0)] // Nothing left
    [InlineData(0, 60, 0, 60)]
    public void WithOutset_RejectsNegativeSides_AndLeavingNoSize(
        int left,
        int top,
        int right,
        int bottom)
    {
        var region = new TextureRegion(PageSource, new RectangleInt(0, 0, 100, 100));

        Assert.Throws<ArgumentOutOfRangeException>(() => region.WithOutset(new ThicknessInt(left, top, right, bottom)));
    }
}

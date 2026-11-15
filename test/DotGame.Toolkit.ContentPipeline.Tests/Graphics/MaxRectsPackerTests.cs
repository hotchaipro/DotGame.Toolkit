// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;
using Xunit;

namespace DotGame.Toolkit.ContentPipeline.Tests.Graphics;

public sealed class MaxRectsPackerTests
{
    private readonly record struct Placed(int X, int Y, int Width, int Height);

    private static void AssertInBoundsAndDisjoint(List<Placed> placed, int width, int height)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            Placed a = placed[i];
            Assert.True(a.X >= 0 && a.Y >= 0 && a.X + a.Width <= width && a.Y + a.Height <= height, $"{a} is outside {width} x {height}.");
            for (int j = i + 1; j < placed.Count; j++)
            {
                Placed b = placed[j];
                bool apart = a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y;
                Assert.True(apart, $"{a} overlaps {b}.");
            }
        }
    }

    [Fact]
    public void TryInsert_PlacesTheFirstRectangleAtTheOrigin()
    {
        var packer = new MaxRectsPacker(100, 100);

        Assert.True(packer.TryInsert(10, 20, out int x, out int y));

        Assert.Equal((0, 0), (x, y));
    }

    [Fact]
    public void TryInsert_FitsARectangleExactly()
    {
        var packer = new MaxRectsPacker(64, 32);

        Assert.True(packer.TryInsert(64, 32, out _, out _));
        Assert.False(packer.TryInsert(1, 1, out _, out _));
    }

    [Theory]
    [InlineData(101, 1)]
    [InlineData(1, 101)]
    public void TryInsert_RefusesWhatDoesntFit(int width, int height)
    {
        var packer = new MaxRectsPacker(100, 100);

        Assert.False(packer.TryInsert(width, height, out _, out _));
    }

    [Fact]
    public void TryInsert_FailureLeavesThePackerUsable()
    {
        var packer = new MaxRectsPacker(10, 10);
        Assert.False(packer.TryInsert(11, 11, out _, out _));

        Assert.True(packer.TryInsert(10, 10, out _, out _));
    }

    [Fact]
    public void TryInsert_FillsAGridExactly()
    {
        var packer = new MaxRectsPacker(40, 40);
        var placed = new List<Placed>();
        for (int i = 0; i < 16; i++)
        {
            Assert.True(packer.TryInsert(10, 10, out int x, out int y), $"Rectangle {i} didn't fit.");
            placed.Add(new Placed(x, y, 10, 10));
        }

        AssertInBoundsAndDisjoint(placed, 40, 40);
        Assert.False(packer.TryInsert(1, 1, out _, out _));
    }

    [Fact]
    public void TryInsert_UsesTheBestShortSideFit()
    {
        // After a 60 x 60 corner, the free space is a 40 wide column and a 40 high row
        var packer = new MaxRectsPacker(100, 100);
        packer.TryInsert(60, 60, out _, out _);

        // 38 wide leaves 2 in the column, its best short side
        Assert.True(packer.TryInsert(38, 10, out int x, out _));

        Assert.Equal(60, x);
    }

    [Fact]
    public void TryInsert_NeverOverlaps_OnAMixOfSizes()
    {
        // Deterministic: a fixed seed, on a page large enough that most of them fit
        var random = new Random(12345);
        var packer = new MaxRectsPacker(512, 512);
        var placed = new List<Placed>();
        int area = 0;
        for (int i = 0; i < 400; i++)
        {
            int width = random.Next(1, 64);
            int height = random.Next(1, 64);
            if (packer.TryInsert(width, height, out int x, out int y))
            {
                placed.Add(new Placed(x, y, width, height));
                area += width * height;
            }
        }

        AssertInBoundsAndDisjoint(placed, 512, 512);
        Assert.True(area > 512 * 512 * 0.8, $"Only {area} of {512 * 512} texels used.");
    }

    [Fact]
    public void TryInsert_GivesTheSameLayoutEveryTime()
    {
        static List<Placed> Pack()
        {
            var random = new Random(99);
            var packer = new MaxRectsPacker(256, 256);
            var placed = new List<Placed>();
            for (int i = 0; i < 100; i++)
            {
                int width = random.Next(1, 40);
                int height = random.Next(1, 40);
                if (packer.TryInsert(width, height, out int x, out int y))
                {
                    placed.Add(new Placed(x, y, width, height));
                }
            }

            return placed;
        }

        Assert.Equal(Pack(), Pack());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(1024, 1024)]
    [InlineData(1025, 2048)]
    public void NextPowerOfTwo_RoundsUp(int value, int expected)
    {
        Assert.Equal(expected, MaxRectsPacker.NextPowerOfTwo(value));
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class SpriteBatchShapeExtensionsTests
{
    [Theory]
    [InlineData(0f, 3)]
    [InlineData(0.5f, 3)]
    [InlineData(5f, 7)]
    [InlineData(40f, 20)]
    [InlineData(400f, 63)]
    [InlineData(100000f, 128)]
    public void Sides_FollowTheRadius(
        float radius,
        int expected)
    {
        Assert.Equal(expected, SpriteBatchShapeExtensions.ChooseSegments(radius, null));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(10f)]
    [InlineData(100f)]
    [InlineData(1000f)]
    public void Sides_StayWithinHalfAPixel_OfTheCircle(
        float radius)
    {
        int sides = SpriteBatchShapeExtensions.ChooseSegments(radius, null);

        float error = radius * (1f - MathF.Cos(MathF.PI / sides));

        Assert.True(error <= 0.5f, $"{sides} sides strays {error}");
    }

    [Fact]
    public void Sides_YouAskFor_AreUsed_AndThreeIsTheFewest()
    {
        Assert.Equal(12, SpriteBatchShapeExtensions.ChooseSegments(40f, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteBatchShapeExtensions.ChooseSegments(40f, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteBatchShapeExtensions.ChooseSegments(-1f, null));
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Xunit;

namespace DotGame.Toolkit.Tests.Graphics;

public sealed class SpriteBatchShapesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FillPolygon_RefusesTooFewPoints(
        int count)
    {
        var points = new Vector2[count];
        var error = Record.Exception(() => CheckPolygon(points));
        Assert.IsType<ArgumentException>(error);
    }

    private static void CheckPolygon(
        Vector2[] points)
    {
        default(SpriteBatch)!.FillPolygon(points, Color.White);
    }
}

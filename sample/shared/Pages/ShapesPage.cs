// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// Plain shapes from <see cref="SpriteBatchShapeExtensions"/>, and <see cref="SpriteBatch.DrawTriangles(Texture, ReadOnlySpan{Vertex})"/>
/// for what shapes don't cover: here, colors blended across a triangle.
/// </summary>
internal sealed class ShapesPage : SamplePage
{
    public ShapesPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Shapes and triangles";

    public override string Hint => "Rectangles, lines, circles, polygons and blended triangles, all in one batch.";

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var turn = Angle.FromDegrees((float)this.Game.Time.Total.TotalSeconds * 20);
        float x = bounds.X + 16;
        float y = bounds.Y + 16;

        using var batch = spriteBatch.Begin();

        // Each shape with the call that draws it. Outlines and lines are dark and saturated, to show
        // on the light page.
        spriteBatch.FillRectangle(new Rectangle(x, y, 120, 70), Color.FromRgb(0x5AB4F0));
        this.Label(spriteBatch, "FillRectangle", x, y + 78);
        spriteBatch.DrawRectangle(new Rectangle(x + 156, y, 120, 70), Color.FromRgb(0xD08A00), 4);
        this.Label(spriteBatch, "DrawRectangle", x + 156, y + 78);

        // A line turning about its middle, so it stays in its row
        var lineMiddle = new Vector2(x + 372, y + 35);
        var lineHalf = turn.ToVector() * 32;
        spriteBatch.DrawLine(lineMiddle - lineHalf, lineMiddle + lineHalf, Color.FromRgb(0x3C465A), 4);
        this.Label(spriteBatch, "DrawLine", x + 312, y + 78);

        y += 120;
        spriteBatch.FillCircle(new Vector2(x + 60, y + 60), 60, Color.FromRgb(0xF0785A));
        this.Label(spriteBatch, "FillCircle", x, y + 132);
        spriteBatch.DrawCircle(new Vector2(x + 216, y + 60), 60, Color.FromRgb(0x2E9E50), 6);
        this.Label(spriteBatch, "DrawCircle", x + 156, y + 132);

        Span<Vector2> hexagon = stackalloc Vector2[6];
        for (int i = 0; i < hexagon.Length; i++)
        {
            hexagon[i] = new Vector2(x + 372, y + 60) + ((turn + Angle.FromDegrees(i * 60)).ToVector() * 60);
        }

        spriteBatch.FillPolygon(hexagon, Color.FromRgb(0xB496F0));
        spriteBatch.DrawPolygon(hexagon, Color.FromRgb(0x4B2A8C), 4);
        this.Label(spriteBatch, "FillPolygon, DrawPolygon", x + 312, y + 132);

        y += 170;
        ReadOnlySpan<Vertex> blended =
        [
            new Vertex(new Vector2(x, y + 120), new Vector2(0.5f), Color.FromRgb(0xFF3C3C)),
            new Vertex(new Vector2(x + 120, y), new Vector2(0.5f), Color.FromRgb(0x3CFF3C)),
            new Vertex(new Vector2(x + 240, y + 120), new Vector2(0.5f), Color.FromRgb(0x3C3CFF)),
        ];
        spriteBatch.FillTriangles(blended);
        this.Label(spriteBatch, "FillTriangles, a color at each corner", x, y + 128);
    }

    private void Label(
        SpriteBatch spriteBatch,
        string text,
        float x,
        float y)
    {
        this.Game.DrawText(spriteBatch, text, new Vector2(x, y), 15, Color.FromRgb(0x5A6478));
    }
}

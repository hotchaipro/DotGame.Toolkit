// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Plain shapes in a sprite batch, in order with its sprites, for debug drawing and simple games.
/// Shapes have hard edges unless the game's <c>PreferredMultisampleCount</c> smooths them.
/// </summary>
/// <remarks>
/// A rectangle or a line costs what a sprite does, and a filled shape what a sprite does for each
/// triangle: a circle has about 20 sides at a radius of 40, and an outlined one twice that. For many circles,
/// draw a circle sprite. Shapes use a texture of their own, so drawing them between sprites from another
/// texture ends the batch's run of sprites each time: draw them together where you can.
/// </remarks>
public static class SpriteBatchShapeExtensions
{
    // Enough for a smooth circle of any size a screen shows, and small enough for the stack
    private const int MaxSegments = 128;

    // How far a side may stray from the true circle, in pixels, before the eye would see it
    private const float MaxSideError = 0.5f;

    // The points around a circle of radius 1, for each number of sides that's been drawn, the first
    // repeated at the end. Sines and cosines only once, and at most a kilobyte for each
    private static readonly Vector2[]?[] s_unitCircles = new Vector2[]?[MaxSegments + 1];

    // What shapes are drawn with, in any color, one for each graphics service, as an extension
    // method has nowhere else to keep it
    private static readonly ConditionalWeakTable<GraphicsService, Texture> s_whitePixels = [];

    private static Texture GetWhitePixel(
        GraphicsService graphics)
    {
        if (!s_whitePixels.TryGetValue(graphics, out var pixel))
        {
            pixel = graphics.CreateTexture(new Vector2Int(1, 1), [Color32.FromRgba(255, 255, 255, 255)]);
            s_whitePixels.Add(graphics, pixel);
            graphics.DeviceReset += OnDeviceReset;
        }

        return pixel;
    }

    // NOTE: A texture is gone with the device, and the next shape makes it again
    private static void OnDeviceReset(
        object? sender,
        EventArgs e)
    {
        var graphics = (GraphicsService)sender!;
        graphics.DeviceReset -= OnDeviceReset;
        s_whitePixels.Remove(graphics);
    }

    /// <summary>
    /// Fills triangles, three vertices each, with their vertices' colors, blended across each one.
    /// It's <see cref="SpriteBatch.DrawTriangles(Texture, ReadOnlySpan{Vertex})"/> without a texture,
    /// so vertices' texture coordinates don't matter.
    /// </summary>
    /// <exception cref="ArgumentException">The number of vertices isn't a multiple of three.</exception>
    public static void FillTriangles(
        this SpriteBatch spriteBatch,
        ReadOnlySpan<Vertex> vertices)
    {
        spriteBatch.DrawTriangles(GetWhitePixel(spriteBatch.Graphics), vertices);
    }

    /// <summary>
    /// Fills triangles from a list of vertices that they share, three indices each, with their
    /// vertices' colors, blended across each one. It's
    /// <see cref="SpriteBatch.DrawTriangles(Texture, ReadOnlySpan{Vertex}, ReadOnlySpan{ushort})"/>
    /// without a texture, so vertices' texture coordinates don't matter.
    /// </summary>
    /// <exception cref="ArgumentException">The number of indices isn't a multiple of three.</exception>
    public static void FillTriangles(
        this SpriteBatch spriteBatch,
        ReadOnlySpan<Vertex> vertices,
        ReadOnlySpan<ushort> indices)
    {
        spriteBatch.DrawTriangles(GetWhitePixel(spriteBatch.Graphics), vertices, indices);
    }

    /// <summary>Fills a rectangle.</summary>
    public static void FillRectangle(
        this SpriteBatch spriteBatch,
        Rectangle rectangle,
        Color color)
    {
        if (rectangle.IsEmpty)
        {
            return;
        }

        spriteBatch.Draw(
            GetWhitePixel(spriteBatch.Graphics),
            new Vector2(rectangle.X, rectangle.Y),
            color,
            scale: new Vector2(rectangle.Width, rectangle.Height));
    }

    /// <summary>
    /// Draws a rectangle's outline, inside its bounds.
    /// </summary>
    public static void DrawRectangle(
        this SpriteBatch spriteBatch,
        Rectangle rectangle,
        Color color,
        float thickness = 1)
    {
        if (rectangle.IsEmpty)
        {
            return;
        }

        float inner = rectangle.Height - (2 * thickness);
        spriteBatch.FillRectangle(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.FillRectangle(new Rectangle(rectangle.X, rectangle.Y + rectangle.Height - thickness, rectangle.Width, thickness), color);
        spriteBatch.FillRectangle(new Rectangle(rectangle.X, rectangle.Y + thickness, thickness, inner), color);
        spriteBatch.FillRectangle(new Rectangle(rectangle.X + rectangle.Width - thickness, rectangle.Y + thickness, thickness, inner), color);
    }

    /// <summary>Draws a line.</summary>
    public static void DrawLine(
        this SpriteBatch spriteBatch,
        Vector2 start,
        Vector2 end,
        Color color,
        float thickness = 1)
    {
        var delta = end - start;
        spriteBatch.Draw(
            GetWhitePixel(spriteBatch.Graphics),
            start,
            color,
            Angle.FromVector(delta),
            new Vector2(0, 0.5f),
            new Vector2(delta.Length(), thickness));
    }

    /// <summary>Fills a triangle.</summary>
    public static void FillTriangle(
        this SpriteBatch spriteBatch,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Color color)
    {
        ReadOnlySpan<Vertex> vertices =
        [
            new Vertex(a, new Vector2(0.5f), color),
            new Vertex(b, new Vector2(0.5f), color),
            new Vertex(c, new Vector2(0.5f), color),
        ];
        spriteBatch.DrawTriangles(GetWhitePixel(spriteBatch.Graphics), vertices);
    }

    /// <summary>
    /// Fills a convex polygon. The points go around it in order, either way.
    /// </summary>
    /// <exception cref="ArgumentException">There are fewer than three points.</exception>
    public static void FillPolygon(
        this SpriteBatch spriteBatch,
        ReadOnlySpan<Vector2> points,
        Color color)
    {
        if (points.Length < 3)
        {
            throw new ArgumentException("A polygon has at least three points.", nameof(points));
        }

        for (int i = 1; i < points.Length - 1; i++)
        {
            spriteBatch.FillTriangle(points[0], points[i], points[i + 1], color);
        }
    }

    /// <summary>
    /// Draws a polygon's outline, closing it back to the first point.
    /// </summary>
    public static void DrawPolygon(
        this SpriteBatch spriteBatch,
        ReadOnlySpan<Vector2> points,
        Color color,
        float thickness = 1)
    {
        for (int i = 0; i < points.Length; i++)
        {
            spriteBatch.DrawLine(points[i], points[(i + 1) % points.Length], color, thickness);
        }
    }

    /// <summary>Fills a circle.</summary>
    /// <param name="segments">How many sides it has, at most 128, or null to choose from its radius, which suits a circle drawn at that size. One magnified by a camera or a scaler may want more.</param>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="center">The circle's center.</param>
    /// <param name="radius">The circle's radius.</param>
    /// <param name="color">The circle's color.</param>
    public static void FillCircle(
        this SpriteBatch spriteBatch,
        Vector2 center,
        float radius,
        Color color,
        int? segments = null)
    {
        int count = ChooseSegments(radius, segments);
        var circle = GetUnitCircle(count);
        Span<Vertex> vertices = stackalloc Vertex[count * 3];
        var uv = new Vector2(0.5f);

        for (int i = 0; i < count; i++)
        {
            vertices[i * 3] = new Vertex(center, uv, color);
            vertices[(i * 3) + 1] = new Vertex(center + (circle[i] * radius), uv, color);
            vertices[(i * 3) + 2] = new Vertex(center + (circle[i + 1] * radius), uv, color);
        }

        spriteBatch.DrawTriangles(GetWhitePixel(spriteBatch.Graphics), vertices);
    }

    /// <summary>
    /// Draws a circle's outline, inside its radius.
    /// </summary>
    /// <param name="segments">How many sides it has, at most 128, or null to choose from its radius, which suits a circle drawn at that size. One magnified by a camera or a scaler may want more.</param>
    /// <param name="spriteBatch">The batch to draw into.</param>
    /// <param name="center">The circle's center.</param>
    /// <param name="radius">The circle's radius.</param>
    /// <param name="color">The circle's color.</param>
    /// <param name="thickness">How thick the circle's line is.</param>
    public static void DrawCircle(
        this SpriteBatch spriteBatch,
        Vector2 center,
        float radius,
        Color color,
        float thickness = 1,
        int? segments = null)
    {
        int count = ChooseSegments(radius, segments);
        var circle = GetUnitCircle(count);
        Span<Vertex> vertices = stackalloc Vertex[count * 6];
        var uv = new Vector2(0.5f);
        float inner = MathF.Max(radius - thickness, 0);

        for (int i = 0; i < count; i++)
        {
            var outerPrevious = center + (circle[i] * radius);
            var innerPrevious = center + (circle[i] * inner);
            var outerNext = center + (circle[i + 1] * radius);
            var innerNext = center + (circle[i + 1] * inner);
            int v = i * 6;
            vertices[v] = new Vertex(outerPrevious, uv, color);
            vertices[v + 1] = new Vertex(outerNext, uv, color);
            vertices[v + 2] = new Vertex(innerNext, uv, color);
            vertices[v + 3] = new Vertex(outerPrevious, uv, color);
            vertices[v + 4] = new Vertex(innerNext, uv, color);
            vertices[v + 5] = new Vertex(innerPrevious, uv, color);
        }

        spriteBatch.DrawTriangles(GetWhitePixel(spriteBatch.Graphics), vertices);
    }

    internal static int ChooseSegments(
        float radius,
        int? segments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        if (segments < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "A circle needs at least three sides, or null to choose from its radius.");
        }

        // NOTE: A side strays furthest from the circle at its middle, by radius * (1 - cos(half its angle))
        int count = segments
            ?? ((radius <= MaxSideError) ? 3 : (int)MathF.Ceiling(MathF.PI / MathF.Acos(1f - (MaxSideError / radius))));
        return Math.Clamp(count, 3, MaxSegments);
    }

    private static Vector2[] GetUnitCircle(
        int count)
    {
        var circle = s_unitCircles[count];
        if (circle is null)
        {
            circle = new Vector2[count + 1];
            for (int i = 0; i < count; i++)
            {
                float angle = MathF.Tau * i / count;
                circle[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }

            circle[count] = circle[0];
            s_unitCircles[count] = circle;
        }

        return circle;
    }
}

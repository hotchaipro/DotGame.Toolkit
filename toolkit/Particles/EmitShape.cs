// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Where around an emitter its particles start.
/// </summary>
public readonly record struct EmitShape
{
    private EmitShape(
        EmitShapeKind kind,
        Vector2 size)
    {
        this.Kind = kind;
        this.Size = size;
    }

    internal EmitShapeKind Kind { get; }

    // The circle or ring's radius in X; the rectangle's size; or the line's extent from one end to
    // the other (centered on the emitter, in any direction)
    internal Vector2 Size { get; }

    /// <summary>Particles start at the emitter's position.</summary>
    public static EmitShape Point => default;

    /// <summary>Particles start anywhere in a circle.</summary>
    public static EmitShape Circle(
        float radius)
    {
        return new EmitShape(EmitShapeKind.Circle, new Vector2(radius, radius));
    }

    /// <summary>Particles start on a ring.</summary>
    public static EmitShape Ring(
        float radius)
    {
        return new EmitShape(EmitShapeKind.Ring, new Vector2(radius, radius));
    }

    /// <summary>Particles start anywhere in a rectangle.</summary>
    public static EmitShape Rectangle(
        float width,
        float height)
    {
        return new EmitShape(EmitShapeKind.Rectangle, new Vector2(width, height));
    }

    /// <summary>Particles start anywhere along a line.</summary>
    public static EmitShape Line(
        Vector2 extent)
    {
        return new EmitShape(EmitShapeKind.Line, extent);
    }

    /// <summary>
    /// A random start offset from the emitter, with the shape scaled by
    /// <paramref name="scale"/>.
    /// </summary>
    internal Vector2 Sample(
        ParticleRandom random,
        float scale)
    {
        switch (this.Kind)
        {
            case EmitShapeKind.Circle:
                {
                    // The square root spreads the starts evenly over the area, not crowded at the center
                    float radius = this.Size.X * scale * MathF.Sqrt(random.NextSingle());
                    (float sin, float cos) = MathF.SinCos(random.NextSingle() * MathF.Tau);
                    return new Vector2(cos * radius, sin * radius);
                }

            case EmitShapeKind.Ring:
                {
                    float radius = this.Size.X * scale;
                    (float sin, float cos) = MathF.SinCos(random.NextSingle() * MathF.Tau);
                    return new Vector2(cos * radius, sin * radius);
                }

            case EmitShapeKind.Rectangle:
                return new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * this.Size * scale;

            case EmitShapeKind.Line:
                return (random.NextSingle() - 0.5f) * this.Size * scale;

            default:
                return Vector2.Zero;
        }
    }
}

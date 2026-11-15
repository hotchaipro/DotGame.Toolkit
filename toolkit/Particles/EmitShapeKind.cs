// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>The kinds of <see cref="EmitShape"/>.</summary>
internal enum EmitShapeKind
{
    /// <summary>
    /// Particles start at the emitter's position.
    /// </summary>
    Point,

    /// <summary>
    /// Particles start anywhere in a circle, evenly over its area.
    /// </summary>
    Circle,

    /// <summary>
    /// Particles start on the edge of a circle.
    /// </summary>
    Ring,

    /// <summary>
    /// Particles start anywhere in a rectangle centered on the emitter.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Particles start anywhere on a line through the emitter.
    /// </summary>
    Line,
}

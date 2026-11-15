// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// How a <see cref="ViewportScaler"/> fits a design onto a screen of another shape.
/// </summary>
public enum ViewportScaleMode
{
    /// <summary>All of the design shows, as large as fits, with bars on the sides that are left over.</summary>
    Fit,

    /// <summary>The design fills the screen, cut off on the sides that don't fit.</summary>
    Fill,

    /// <summary>The design fills the screen exactly, stretched to its shape.</summary>
    Stretch,

    /// <summary>
    /// As <see cref="Fit"/>, at a whole number of screen pixels per design pixel, for pixel art.
    /// Use it with a point sampler.
    /// </summary>
    IntegerFit,

    /// <summary>
    /// All of the design shows, as large as fits, and the sides that are left over show more of the
    /// world rather than bars: <see cref="ViewportScaler.VisibleBounds"/> says how much.
    /// </summary>
    Expand,

    /// <summary>
    /// As <see cref="Expand"/> for a screen taller than the design, showing more below it, and as
    /// <see cref="Fit"/> for a wider one, with bars: for a game played upright on phones of many shapes.
    /// </summary>
    ExpandVertical,

    /// <summary>
    /// As <see cref="Expand"/> for a screen wider than the design, showing more to its right, and as
    /// <see cref="Fit"/> for a taller one, with bars: for a game played sideways on phones of many shapes.
    /// </summary>
    ExpandHorizontal,
}

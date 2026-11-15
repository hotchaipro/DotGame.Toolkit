// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A glow around text: a soft outline with round corners, drawn behind the outline and the face.
/// </summary>
/// <param name="Color">The glow's color.</param>
/// <param name="Width">How far the glow extends beyond the face's edges, in pixels of the font's native size, to the middle of its fade.</param>
/// <param name="Softness">How much wider than a screen pixel the glow's edge fades, in pixels of the font's native size.</param>
public readonly record struct TextGlow(
    Color Color,
    float Width,
    float Softness);

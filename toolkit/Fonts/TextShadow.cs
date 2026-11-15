// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A shadow of text, drawn behind everything else.
/// </summary>
/// <param name="Color">The shadow's color.</param>
/// <param name="Offset">Where the shadow is drawn relative to the text, in pixels of the font's native size, in the text's own space.</param>
/// <param name="Spread">How far the shadow extends beyond the face's edges, in pixels of the font's native size.</param>
/// <param name="Softness">How much wider than a screen pixel the shadow's edge fades, in pixels of the font's native size.</param>
public readonly record struct TextShadow(
    Color Color,
    Vector2 Offset,
    float Spread = 0,
    float Softness = 0);

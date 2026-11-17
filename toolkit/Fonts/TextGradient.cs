// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Two colors a text's face is drawn in, one blending into the other down each line.
/// </summary>
/// <param name="Top">The color at the top of the line's capital letters, and of anything above them.</param>
/// <param name="Bottom">The color at the line's baseline, and of anything below it.</param>
/// <remarks>
/// The blend runs down the height of the font's capitals (its ascent, where the font gives no
/// cap height), the same for every character of a line, so a word is one sweep of color and
/// not a sweep for each letter. Opacity fades both colors alike.
/// </remarks>
public readonly record struct TextGradient(
    Color Top,
    Color Bottom);

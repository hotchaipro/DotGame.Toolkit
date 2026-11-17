// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A slanted band of light across a text's face: brightest along its middle, fading to nothing
/// at its edges. Move <see cref="Position"/> along the line from frame to frame and the light
/// crosses the letters.
/// </summary>
/// <param name="Color">The color of the light, which the face is blended toward.</param>
/// <param name="Position">Where the middle of the band crosses the baseline, along the line from its start, in pixels of the font's native size.</param>
/// <param name="Width">How wide the band is, along the line, in pixels of the font's native size.</param>
/// <param name="Slant">How far the band leans to the right for each pixel it rises: 0 is upright, negative leans left.</param>
/// <param name="Strength">How far the face is blended toward <paramref name="Color"/> along the middle of the band, from 0 to 1.</param>
/// <remarks>
/// The band is in the line's own space, the same on every line of the text: it lies over a
/// gradient or a character's own color, moves and turns with characters that do
/// (<see cref="ICharacterAttributes"/>), and leaves the outline, glow and shadow alone. Only the
/// characters the band touches cost more to draw; a band that is off either end of the line, or
/// of no width or strength, costs nothing.
/// </remarks>
public readonly record struct TextSheen(
    Color Color,
    float Position,
    float Width,
    float Slant = 0.35f,
    float Strength = 1);

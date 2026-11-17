// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A band of light across a text's face, upright or leaning: brightest along its middle, fading
/// to nothing at its edges. Move <see cref="Position"/> along the line from frame to frame and
/// the light crosses the letters.
/// </summary>
/// <param name="Color">The color of the light, which the face is blended toward.</param>
/// <param name="Position">Where the middle of the band crosses the baseline, along the line from its start, in pixels of the font's native size.</param>
/// <param name="Width">How wide the band is, along the line, in pixels of the font's native size.</param>
/// <param name="Angle">How far the band leans from upright: positive leans to the right as it rises, negative to the left. Upright by default; it can lean up to 80 degrees either way.</param>
/// <param name="Strength">How far the face is blended toward <paramref name="Color"/> along the middle of the band, from 0 to 1.</param>
/// <param name="Softness">How much of the band is its fading edges, from 0 to 1: at 1 it fades all the way from its middle to its edges, at 0.5 the middle half of it is at full strength, and toward 0 it is a hard-edged bar.</param>
/// <remarks>
/// The band is in the line's own space, the same on every line of the text: it lies over a
/// gradient or a character's own color, moves and turns with characters that do
/// (<see cref="ICharacterAttributes"/>), and leaves the outline, glow and shadow alone. Only the
/// characters the band touches cost more to draw; a band that is off either end of the line, or
/// of no width or strength, costs nothing.
/// <para>
/// To cross a line of text without measuring it, give the band a progress from 0 to 1 with
/// <see cref="SpriteFont.SheenAcross(ReadOnlySpan{char}, float, TextSheen, float)"/>.
/// </para>
/// </remarks>
public readonly record struct TextSheen(
    Color Color,
    float Position,
    float Width,
    Angle Angle = default,
    float Strength = 1,
    float Softness = 1)
{
    // The most it leans: beyond this it would lie along the line
    private const float MaximumLean = 80f * MathF.PI / 180f;

    /// <summary>
    /// How far the band's middle moves along the line for each pixel it rises.
    /// </summary>
    internal float Slant => MathF.Tan(Math.Clamp(this.Angle.Radians, -MaximumLean, MaximumLean));

    /// <summary>
    /// How far from its middle the band is at full strength, before it begins to fade. (An
    /// edge always fades over a little of the band: a corner of a triangle has one color, so an
    /// edge with no width at all couldn't be drawn.)
    /// </summary>
    internal float Plateau => (this.Width * 0.5f) * (1 - Math.Clamp(this.Softness, 0.04f, 1f));

    /// <summary>
    /// Places the band on a line by how far across it the band has come, without the caller
    /// working out where "just off the line" is for a band that leans: at 0 the band is just
    /// off the start of the line, touching no letter, and at 1 just off its end.
    /// </summary>
    /// <param name="progress">How far across: 0 to 1, and beyond either for further off.</param>
    /// <param name="lineWidth">The width of the line, in pixels of the font's native size.</param>
    /// <param name="ascent">How far the letters rise above the baseline.</param>
    /// <param name="descent">How far they hang below it, as a distance (not negative).</param>
    /// <returns>This band at that <see cref="Position"/>.</returns>
    public TextSheen Across(
        float progress,
        float lineWidth,
        float ascent,
        float descent)
    {
        // Where its middle is, along the line, at the top and the foot of the letters, relative
        // to where it crosses the baseline: a leaning band reaches further one way up there and
        // the other way down here
        float slant = this.Slant;
        float atTop = slant * Math.Max(ascent, 0);
        float atFoot = -slant * Math.Max(descent, 0);
        float half = Math.Max(this.Width, 0) * 0.5f;

        float start = -half - Math.Max(atTop, atFoot);
        float end = Math.Max(lineWidth, 0) + half - Math.Min(atTop, atFoot);
        return this with { Position = start + (progress * (end - start)) };
    }
}

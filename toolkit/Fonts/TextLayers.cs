// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// The layers of text with <see cref="TextEffects"/> that a <c>DrawString</c>
/// call draws.
/// </summary>
/// <remarks>
/// <para>
/// One call draws every glyph's underlays before any glyph's face, so an underlay never covers a
/// face of the same string. Text that is drawn in several calls (lines laid out and aligned one at
/// a time, or runs in different colors) needs the same order across the calls: draw every call
/// with <see cref="Underlays"/>, then every call again with <see cref="Face"/>.
/// </para>
/// <para>
/// Layers that the effects don't have draw nothing, so asking for <see cref="Underlays"/> of text
/// with none draws nothing at all.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var line in lines)
/// {
///     spriteBatch.DrawString(font, line.Text, ..., effects, layers: TextLayers.Underlays);
/// }
///
/// foreach (var line in lines)
/// {
///     spriteBatch.DrawString(font, line.Text, ..., effects, layers: TextLayers.Face);
/// }
/// </code>
/// </example>
[Flags]
public enum TextLayers
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Everything under the face (<see cref="TextEffects.Underlays"/>).</summary>
    Underlays = 1,

    /// <summary>The glyphs themselves, with the face's gradient, sheen, emboldening and softness.</summary>
    Face = 2,

    /// <summary>Everything: the underlays, then the face.</summary>
    All = Underlays | Face,
}

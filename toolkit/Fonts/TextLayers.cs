// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// The layers of text with <see cref="TextEffects"/> that a <c>DrawString</c>
/// call draws.
/// </summary>
/// <remarks>
/// <para>
/// One call draws every glyph's effects before any glyph's face, so effects never cover a face
/// of the same string. Text that is drawn in several calls (lines laid out and aligned one at a
/// time, or runs in different colors) needs the same order across the calls: draw every call
/// with <see cref="Effects"/>, then every call again with <see cref="Face"/>.
/// </para>
/// <para>
/// Layers that the effects don't have draw nothing, so asking for <see cref="Effects"/> of text
/// with no effects draws nothing at all.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var line in lines)
/// {
///     spriteBatch.DrawString(font, line.Text, ..., effects, layers: TextLayers.Effects);
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

    /// <summary>The shadow (<see cref="TextEffects.Shadow"/>).</summary>
    Shadow = 1,

    /// <summary>The glow (<see cref="TextEffects.Glow"/>).</summary>
    Glow = 2,

    /// <summary>The outline (<see cref="TextEffects.Outline"/>).</summary>
    Outline = 4,

    /// <summary>The glyphs themselves, with the face's emboldening and softness.</summary>
    Face = 8,

    /// <summary>Every effect behind the face: shadow, glow and outline.</summary>
    Effects = Shadow | Glow | Outline,

    /// <summary>Everything: the effects, then the face.</summary>
    All = Effects | Face,
}

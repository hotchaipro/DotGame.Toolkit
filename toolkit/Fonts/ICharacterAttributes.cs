// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Supplies the drawing attributes for each character of a string.
/// </summary>
/// <remarks>
/// <para>
/// An implementation behaves like a cursor: <see cref="MoveTo"/> positions it on a
/// character of the original text, and the attribute members then describe that character.
/// Positioning once and reading many attributes keeps the (potentially non-trivial) lookup
/// out of each individual attribute.
/// </para>
/// <para>
/// The cursor is absolute (see <see cref="CharacterCursor"/>), so text that is drawn in
/// several pieces shares one instance: no attribute splitting is required, characters that
/// are never drawn simply are never visited, and a drawing pass can be repeated in any
/// order. Implementations that cache their position must therefore tolerate a cursor that
/// moves backwards.
/// </para>
/// <para>
/// Implementations are consumed through a generic constraint rather than as a boxed
/// interface, so the per-character members are devirtualized and inlined. Additional
/// attributes should be added as default interface implementations that describe the
/// identity (no-op) value, which keeps existing implementations source compatible.
/// NOTE: a <c>struct</c> implementation must implement every member explicitly, including
/// the identity ones, because invoking a default interface implementation through a
/// generic constraint boxes the value on every call.
/// </para>
/// </remarks>
public interface ICharacterAttributes
{
    /// <summary>
    /// Positions the cursor on the specified character of the original text.
    /// </summary>
    /// <param name="cursor">The position of the character within the original text.</param>
    /// <remarks>
    /// This is called once for each glyph that is drawn rather than once for each character
    /// of the text, so a character that draws nothing is skipped.
    /// </remarks>
    void MoveTo(
        in CharacterCursor cursor);

    /// <summary>
    /// Gets the color that replaces the color passed to the draw method for the current
    /// character, or <c>null</c> to draw it in that color.
    /// </summary>
    /// <remarks>
    /// The color of the text belongs to the caller of the draw method, so an implementation
    /// that describes something other than color (a per-glyph translation, for example)
    /// leaves this alone and the text keeps the color it was drawn with.
    /// </remarks>
    Color? Color
    {
        get
        {
            return null;
        }
    }

    // Additional per-character attributes belong here, declared with a default
    // implementation that returns the identity value. Each one falls into one of two
    // categories, and which category it belongs to decides how a caller that draws the
    // same text more than once has to treat it:
    //
    //   Appearance (Color, and for example Opacity) describes how a glyph is painted. A
    //   caller may legitimately override it for a single pass, which is what lets a drop
    //   shadow or an outline be drawn in one solid color beneath text that is not.
    //
    //   Geometry (for example Translation, Rotation, Scale) describes where a glyph is
    //   placed. Every pass that draws the same text MUST apply it identically, or the
    //   passes come apart and a glyph separates from its own shadow or outline.
    //
    //     float Opacity => 1.0f;                 // appearance
    //     Vector2 Translation => Vector2.Zero;   // geometry
    //     Angle Rotation => Angle.Zero;          // geometry
    //
    // NOTE: Geometry attributes take no part in layout. Text is measured and wrapped from
    // the font metrics alone, so a glyph that these attributes displace may fall outside
    // the bounds that were measured for it.
}

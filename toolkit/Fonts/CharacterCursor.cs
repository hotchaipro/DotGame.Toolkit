// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Identifies the position of a character within the original text being drawn.
/// </summary>
/// <remarks>
/// <para>
/// Both indexes are absolute: they are relative to the start of the original text, not to
/// the start of the span passed to a draw method. This allows a single
/// <see cref="ICharacterAttributes"/> instance to describe text that is drawn in several
/// pieces (for example, one piece per wrapped line) without splitting the attributes.
/// </para>
/// <para>
/// <see cref="CharIndex"/> identifies the position within the text as it was written, and
/// <see cref="GlyphIndex"/> identifies the position within the text as it is drawn. Use the
/// former to describe a range of the text, and the latter to distribute an effect evenly
/// across the text.
/// </para>
/// <para>
/// Additional position information should be added here rather than as extra parameters,
/// so that the cursor can grow without breaking every implementation.
/// </para>
/// </remarks>
public readonly record struct CharacterCursor
{
    /// <summary>The start of a text.</summary>
    public static CharacterCursor Start => new CharacterCursor(0, 0);

    /// <summary>Makes a cursor at these positions.</summary>
    public CharacterCursor(
        int charIndex,
        int glyphIndex)
    {
        this.CharIndex = charIndex;
        this.GlyphIndex = glyphIndex;
    }

    /// <summary>
    /// Gets the UTF-16 index of the character within the original text.
    /// </summary>
    /// <remarks>
    /// This is the index that a caller uses to describe a range of the text, so it counts
    /// each character of a surrogate pair separately, exactly as <see cref="string"/> does.
    /// </remarks>
    public int CharIndex { get; }

    /// <summary>
    /// Gets the ordinal of the glyph within the original text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This counts Unicode code points, so a surrogate pair counts once while an emoji sequence
    /// counts each of its code points: <see cref="SpriteFont"/> does no text shaping, so it forms
    /// no ligatures or sequences. That makes this the useful index for effects that distribute
    /// an attribute evenly across the text, such as a per-glyph color ramp.
    /// </para>
    /// <para>
    /// The ordinal increases as the text is drawn. Whether characters that draw nothing, such as
    /// a line break or a character that is missing from the font, advance the ordinal is
    /// unspecified, so treat it as a position rather than as a count.
    /// </para>
    /// </remarks>
    public int GlyphIndex { get; }
}

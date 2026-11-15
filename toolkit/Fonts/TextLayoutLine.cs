// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A line of a <see cref="TextLayout"/>: which part of the text it shows, and how wide that is.
/// </summary>
/// <param name="Start">The index in the text of the line's first character.</param>
/// <param name="Length">How many characters the line shows.</param>
/// <param name="Width">How wide it is, with the layout's scale applied, and its ellipsis if any.</param>
/// <param name="HasEllipsis">Whether it ends in the layout's ellipsis, as text was cut off.</param>
public readonly record struct TextLayoutLine(
    int Start,
    int Length,
    float Width,
    bool HasEllipsis);

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// How the corners of an outline are drawn.
/// </summary>
public enum TextCorners
{
    /// <summary>
    /// Mitered corners that follow the glyph's corners, cut back once they extend more than the
    /// outline's <see cref="TextOutline.MiterLimit"/> times its width, as SVG's
    /// <c>stroke-miterlimit</c> does. Fonts in <see cref="GlyphFormat.Msdf"/> can't cut corners
    /// back, and draw them unlimited; <see cref="GlyphFormat.Sdf"/> fonts draw them round.
    /// </summary>
    Sharp,

    /// <summary>
    /// Round corners, at the outline's width from the glyph's corners. Fonts in
    /// <see cref="GlyphFormat.Msdf"/> can't draw them, and draw sharp corners instead.
    /// </summary>
    Round,
}

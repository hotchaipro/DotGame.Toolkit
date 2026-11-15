// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Describes how the glyphs of a <see cref="SpriteFont"/> are stored in its atlas pages.
/// </summary>
internal enum GlyphFormat
{
    /// <summary>
    /// Anti-aliased coverage rendered (and hinted) at the font's <see cref="SpriteFont.EmSize"/>,
    /// stored as white with premultiplied alpha. Pixel-exact when drawn at scale 1, and drawn with
    /// the default sprite shader.
    /// </summary>
    Bitmap,

    /// <summary>
    /// Single-channel signed distance field. Scales freely, with rounded corners.
    /// </summary>
    Sdf,

    /// <summary>
    /// Multi-channel signed distance field. Scales freely and keeps sharp corners.
    /// </summary>
    Msdf,

    /// <summary>
    /// Multi-channel signed distance field with a true signed distance field in alpha, for soft
    /// effects such as glows and shadows.
    /// </summary>
    Mtsdf,
}

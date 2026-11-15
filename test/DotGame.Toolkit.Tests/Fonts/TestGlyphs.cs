// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Fonts;

namespace DotGame.Toolkit.Tests.Fonts;

internal static class TestGlyphs
{
    public static SpriteFontGlyph? Of(
        SpriteFont font,
        int codePoint)
    {
        return font.TryGetGlyph(codePoint, out var glyph) ? glyph : null;
    }
}

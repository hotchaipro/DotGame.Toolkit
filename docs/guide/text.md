# Text and fonts

The framework doesn't draw text, and the toolkit does, with [`SpriteFont`](../api/SpriteFont.md).
A font is built ahead of time into an atlas of glyphs, usually stored as distance fields, so one font
stays sharp at many sizes, and outlines, shadows and glows cost little.

Text is drawn glyph by glyph, left to right, with kerning but no shaping. Scripts that need shaping or
run right to left, such as Arabic, Hebrew and the Indic scripts, won't draw correctly.

## Building a font

Add the font file to your project:

```xml
<SpriteFontContent Include="Content\Fonts\Title.ttf" Size="36" PxRange="12" />
```

| Metadata | Default | Description |
|---|---|---|
| `Name` | File name | Output name and content key |
| `Size` | `32` | Em size in pixels (4-512) |
| `GlyphFormat` | `Mtsdf` | `Mtsdf`, `Msdf`, `Sdf` or `Bitmap` (hinted). See [glyph formats](#glyph-formats) |
| `Charset` | ASCII 0x20-0x7E | Literal characters to include |
| `CharsetFile` | | `;`-separated text files whose characters are included |
| `Fallback` | | `;`-separated fonts used, in order, for characters the primary font lacks |
| `PxRange` | `Size / 4` (min 2) | Distance field range in pixels. See [sizing the range](#sizing-the-distance-range) |
| `PxRangeOffset` | `0` | Shifts the distance field's edge, for thicker or thinner glyphs |
| `Kerning` | `false` | Include kerning pairs |

**The default character set is only ASCII.** A character that isn't in the font doesn't draw, so include
everything your text can use. This is a Latin font with Chinese from a fallback font:

```xml
<SpriteFontContent Include="Content\Fonts\Body.otf" Name="basefont"
                   Size="36" CharsetFile="latin_charset.txt;zh_charset.txt"
                   Fallback="Content\Fonts\NotoSansSC-Regular.otf" />
```

Fonts are TrueType or OpenType (`.ttf`, `.otf`, `.ttc`). WOFF web fonts aren't read.

## Drawing text

Load the font by its key, and draw it in a batch. The `scale` sets the size: the font's size is
`font.EmSize`, so this draws at 24 pixels:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    this.font = await this.Content.LoadAsync<SpriteFont>("Fonts/Title");
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;

    using var batch = spriteBatch.Begin();
    spriteBatch.DrawString(this.font, "Hello", new Vector2(20, 20), Color.White, scale: new Vector2(24 / this.font.EmSize));
}
```

Text draws with the toolkit's text shader, which every font loads and shares, in every glyph format and
with every effect. It has no parameters, so text in several fonts and effects draws together, split only
where the atlas page changes. `DrawString` switches to it and back, in any batch, and sprites around the
text keep the shader they were using.

To avoid garbage in text that changes often, `DrawString` also takes a `StringBuilder` or a span.

## Measuring text

[`MeasureString`](../api/SpriteFont.md#measurestring) gives a string's size at the font's own
size, so multiply by your scale. This centers a title:

```csharp
private Vector2 CenteredPosition(string text, float size)
{
    float scale = size / this.font.EmSize;
    var measured = this.font.MeasureString(text) * scale;
    return (Vector2)this.ClientBounds.Center - (measured / 2);
}
```

The font also has `LineHeight`, `Ascender`, `Descender`, `CapHeight` and `Baseline`, for placing text
exactly.

## Wrapping and alignment

[`TextLayout`](../api/TextLayout.md) breaks text into lines, aligns them, and can cut text off
with an ellipsis. Make one and set its `Text`. It works out its lines when they're next needed after a change, and drawing it allocates nothing:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    this.font = await this.Content.LoadAsync<SpriteFont>("Fonts/Title");

    this.layout = new TextLayout(this.font)
    {
        MaxWidth = 400,
        MaxLines = 3,
        Alignment = TextAlignment.Center,
        Scale = 24 / this.font.EmSize,
    };
    this.layout.Text = "Lines break at spaces, and a word that's too long breaks between characters.";
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;

    using var batch = spriteBatch.Begin();
    this.layout.Draw(spriteBatch, new Vector2(20, 20), Color.White);
}
```

`Size` is the space the text takes, and `Lines` has each line's range of the text and its width. Text
past `MaxLines` ends in an ellipsis that fits. A font with no ellipsis glyph uses three dots.

## Text effects

Outlines, shadows and glows are properties of the text, drawn by the same `DrawString` call:

```csharp
private static readonly TextEffects Title = new()
{
    Shadow = new TextShadow(Color.Black * 0.5f, Offset: new Vector2(2, 3), Softness: 2),
    Outline = new TextOutline(Color.Black, Width: 3),
};

private void DrawTitle(SpriteBatch spriteBatch)
{
    spriteBatch.DrawString(this.font, "Level 3", new Vector2(20, 20), Color.White, scale: new Vector2(48 / this.font.EmSize), effects: Title);
}
```

| Effect | Settings |
|---|---|
| `Shadow` | Color, offset, spread, softness |
| `Glow` | Color, width, softness; round corners |
| `Outline` | Color, width, corners (`Sharp` with a miter limit, as SVG's `stroke-miterlimit`, or `Round`), softness |
| `Gradient` | Two colors for the face, one at the top of the capitals and one at the baseline |
| `Sheen` | A slanted band of light across the face: color, position along the line, width, slant, strength |
| `Embolden`, `Softness` | The face's own: positive emboldening is bolder, negative thinner |

- **Every distance is in pixels of the font's native size** (`EmSize`), so effects scale with the text.
  The shadow's offset rotates with rotated text.
- **A string's effects are drawn behind all of its faces:** every glyph's shadow, then glow, then
  outline, then face. So one glyph's outline can't cover its neighbor's face.
- **Colors:** the color passed to `DrawString`, and per-character colors, apply to the face. Each effect
  keeps its own color. A `Gradient` replaces the face's color with two, blended down the height of the
  capitals, the same for every character of a line; a character with a color of its own keeps that.
- **A sheen is a glint to animate:** a `TextSheen` is a band of light at a place along the line, lying
  over whatever color the face has. Draw the text each frame with the band a little further along
  (`effects with { Sheen = sheen with { Position = x } }`, from before the line's start to past its
  end) and the light crosses the letters. Text draws exactly as it would without one while the band is
  off the line.
- **Effects are values:** share one `TextEffects` between strings, and derive others with `with`
  (`Title with { Outline = null }`). `GetExtent` reports how far they reach beyond the glyphs, for
  layouts that make room. Measuring text ignores them.
- **Cost:** each effect draws the glyphs once more, in the same batch.

## Moving and turning characters

Each character of a string can be given a color, a move and a turn of its own, by an
[`ICharacterAttributes`](../api/ICharacterAttributes.md) passed to `DrawString`. For each character it
draws, the text calls `MoveTo` with a [`CharacterCursor`](../api/CharacterCursor.md) (the character's
index, and `X`, how far along its line the middle of its advance is), then reads the attributes:

| Attribute | |
|---|---|
| `Color` | Replaces the face's color for this character; `null` keeps the text's |
| `Translation` | Moves the character, in pixels of the font's native size, in the text's own space |
| `Rotation` | Turns it about the middle of its advance on the baseline |

A move and a turn apply to every layer, so a character's shadow, outline and face stay together. They
take no part in measuring or wrapping.

Text along an arch moves each character down by how far it is from the middle, and turns it to the
arch's slope there:

```csharp
private sealed class Arched(float middle, float radius) : ICharacterAttributes
{
    private float angle;

    public void MoveTo(in CharacterCursor cursor)
    {
        this.angle = (cursor.X - middle) / radius;
    }

    public Vector2 Translation
    {
        get
        {
            float x = middle + (radius * MathF.Sin(this.angle));
            float distanceAlong = middle + (radius * this.angle);
            return new Vector2(x - distanceAlong, radius * (1 - MathF.Cos(this.angle)));
        }
    }

    public Angle Rotation => Angle.FromRadians(this.angle);
}

private void DrawArched(SpriteBatch spriteBatch, string text, Vector2 position)
{
    float width = this.font.MeasureString(text).X;
    spriteBatch.DrawString(this.font, text, new Arched(width / 2, radius: 400), CharacterCursor.Start, position, Color.White);
}
```

A class is the simple way to write one. A `struct` draws without allocating, and has to implement
every attribute, the ones it leaves alone too (returning `null`, `Vector2.Zero` and `default`): the
interface's own would box it on every call.

## Sizing the distance range

The most common problems with text come from `PxRange`, for two reasons.

**Size it for the effects.** An effect reaches only as far from the glyphs as the atlas encodes
distances: half of `PxRange`. Set `PxRange` to at least twice the widest effect (outline width, glow
width plus half its softness, shadow spread plus half its softness). Wider effects are drawn as far as
the range reaches, and a Debug build traces it once per font. A sharp outline's corners are likewise cut
back no further out than half the range.

**Size it for the smallest size the text is drawn at.** The shader smooths each edge over one screen
pixel, which is more atlas texels the smaller the text is drawn, but over no more than a quarter of
`PxRange`. Text drawn smaller than `4 / PxRange` of the font's size gets hard, jagged edges. The default
range, a quarter of the size, holds a 32 pixel font down to half its size and leaves room for effects
reaching 4 pixels; smaller fonts, smaller text or wider effects need an explicit `PxRange`: 16 holds a
32 pixel font down to a quarter of its size. The range costs little, only a few texels of padding around
each glyph.

## Glyph formats

| | Sharp face | Sharp outlines | Miter limit | Round outlines | Softness and glow |
|---|---|---|---|---|---|
| **Mtsdf** | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Msdf** | ✓ | ✓ | Unlimited only | Falls back to sharp | Accurate for small values |
| **Sdf** | Round | Falls back to round | n/a | ✓ | ✓ |
| **Bitmap** | As rasterized | Effects aren't drawn | | | |

`Mtsdf`, the default, is the format with no fallbacks. `Msdf` is about 30% smaller (measured on a Latin
font and on 1,000 CJK characters), and suits text that needs only sharp outlines and hard shadows.

## Custom text shaders

Set a font's `Shader` to draw its text with your own. It reads each glyph's settings as `TEXCOORD1`, a
`float4` of the sprite batch's per-sprite shader data:

| | Meaning |
|---|---|
| X | Distance offset in atlas texels: the font's distance-range middle plus the layer's emboldening |
| Y | Softness in atlas texels |
| Z | Miter limit: 1 for round corners, 1024 for unlimited |
| W | The font's distance range in atlas texels: positive for `Msdf` and `Sdf` (read the median of RGB), negative for `Mtsdf` (alpha holds the true distance), zero for bitmap fonts |

The toolkit's `spritefont.slang` is the reference. It measures the screen-space derivatives at the top of
the pixel shader and returns once, at the end, for
[the reason in the shaders guide](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/shaders.md#a-trap-derivatives-on-the-web). Set `GlyphGenExe` to use a
different glyphgen binary.

## See also

- [`SpriteFont`](../api/SpriteFont.md),
  [`SpriteBatchTextExtensions`](../api/SpriteBatchTextExtensions.md),
  [`TextLayout`](../api/TextLayout.md), [`TextEffects`](../api/TextEffects.md)
- The toolkit sample's Text and Text layout pages

# Text effects

Outlines, drop shadows and glows on text, and the face's own emboldening and softness, are properties
of the text, drawn by the same `DrawString` call as the text itself. Written once ("white text, a
3-pixel black outline, a soft shadow down and to the right"), they look the same at every size and
spacing, draw in the text's place in the scene's draw order, and cost no draws beyond the text's own.

Other engines set the expectation. TextMeshPro (Unity) has an outline, an underlay (shadow) with offset
and softness, a glow, and softness and dilation on the face. Godot's labels have an outline size and
color and a shadow offset and color. Unreal's Slate font info has outline and shadow settings. In all
of them effects are fixed properties of the text, and each piece of text draws its own. The toolkit
does the same.

The code is in [toolkit/Fonts](../../toolkit/Fonts) (`DotGame.Toolkit.Fonts`) and the shader in
[pipeline/Shaders/spritefont.slang](../../pipeline/Shaders/spritefont.slang). It's built on the
framework's sprite batch and its per-sprite shader data; DotGame.Framework's
docs/architecture/sprite-batch.md describes those.

## The API

```csharp
var title = new TextEffects
{
    Shadow = new TextShadow(Color.Black * 0.5f, Offset: new Vector2(2, 3), Softness: 2),
    Outline = new TextOutline(Color.Black, Width: 3),
};

using var batch = spriteBatch.Begin();
spriteBatch.Draw(panel, panelPosition, Color.White);
spriteBatch.DrawString(titleFont, "Level 3", titlePosition, Color.White, scale: scale, effects: title);
spriteBatch.DrawString(bodyFont, "Tap to start", bodyPosition, Color.White, scale: scale);
```

The title draws its shadow, outline and face, in that order, over the panel; the body text follows with
no effects.

| Type | Role |
|---|---|
| `TextEffects` | A record: `Shadow`, `Glow`, `Outline` (each optional), and the face's `Gradient`, `Embolden` and `Softness`. `TextEffects.None` is text with none. |
| `TextGradient(Top, Bottom)` | Two colors for the face, in place of its one. |
| `TextSheen(Color, Position, Width, Angle, Strength)` | A band of light across the face, leaning by an angle from upright. |
| `TextShadow(Color, Offset, Spread = 0, Softness = 0)` | Drawn behind everything else. |
| `TextGlow(Color, Width, Softness)` | A soft outline with round corners, drawn behind the outline. |
| `TextOutline(Color, Width, Corners = Sharp, MiterLimit = 4, Softness = 0)` | Drawn behind the face. |
| `TextCorners` | `Sharp` (mitered, cut back at the miter limit) or `Round`. |
| `TextLayers` | Flags for which layers a call draws: `Shadow`, `Glow`, `Outline`, `Face`; `Effects` and `All`. |
| `SpriteBatchTextExtensions.DrawString` | Draws text in a font, with optional effects and layers, from a `ReadOnlySpan<char>` or a `StringBuilder`, with or without per-character attributes. |

- **Every distance is in pixels of the font's native size** (`SpriteFont.EmSize`), so effects scale
  with the text and keep their proportions at every size. The shadow's offset is in the text's own
  space, so it rotates with rotated text.
- **The order is fixed:** shadow, glow, outline, face. The glow is behind the outline, so an outlined,
  glowing title shows a crisp outline over the glow.
- **Colors.** The color passed to `DrawString`, and per-character colors from `ICharacterAttributes`,
  apply to the face. Each effect has its own color, so a highlighted word keeps the same outline as the
  rest of the line. The call's opacity fades the face and the effects alike.
- **A gradient** blends from its top color at the top of the line's capitals (`SpriteFont.CapHeight`
  above the baseline, or the ascent where the font gives none) to its bottom color at the baseline.
  It's measured on the line and not on each glyph, so a word is one sweep of color. A character that
  `ICharacterAttributes` gives a color of its own keeps that color.
- **A character's move and turn** (`ICharacterAttributes.Translation` and `Rotation`) are geometry:
  every layer applies them alike, so a character's shadow, glow, outline and face stay together. A
  character turns about the middle of its advance on the baseline (`CharacterCursor.X`). They're in the
  text's own space, before its scale and rotation, and take no part in measuring.
- **Effects are values.** One `TextEffects` is shared by many strings, compared, and derived with
  `with` (`title with { Outline = null }`).
- **Measuring ignores effects:** they don't change advances or line heights.
  `TextEffects.GetExtent()` returns how far the effects reach beyond the glyphs on each side, for
  layouts that make room for them.
- **"Effect" means only a visual effect** in the toolkit. Shaders are shaders, as in the framework's
  `Shader` type.

## Drawing

`DrawString` turns the effects into up to four layers (`TextLayer`, internal), in drawing order. Each
layer is a pass over every glyph of the string, and each holds a color, an offset (the shadow's) and
the per-sprite shader data every quad of the pass carries. `SpriteFont.GetLayers` builds them, and
text without effects uses a face layer the font builds once.

**Every effect of a string is drawn before its faces, across all its glyphs:** every glyph's shadow,
then every glyph's glow, then every outline, then every face. Glyph quads overlap (kerned pairs such as
"AV", tight tracking, outlines wider than the gap between glyphs), and drawing each glyph's outline and
face together would let a later glyph's outline cover an earlier glyph's face, an artifact TextMeshPro
is known for.

Text drawn in several calls, such as lines aligned one at a time or runs in different styles, needs the
same order across the calls. `TextLayers` provides it: draw every call with `TextLayers.Effects`, then
every call again with `TextLayers.Face`. `TextEffects.HasBackgroundEffects` says whether the second
pass is needed at all. `TextLayout.Draw` draws its lines this way.

**A moved or turned character** is still one sprite a layer: its pivot becomes the sprite's origin, the
sprite is drawn where the text's own rotation and scale carry the moved pivot, and its rotation is the
text's plus its own. A character that isn't moved is drawn exactly as before.

**A gradient's glyphs are triangles.** A sprite has one color, and blending between the corners of a
glyph's rectangle wouldn't do either: the rectangle is larger than the glyph by the distance range, so
the gradient would be squeezed into the middle of every letter. The face is drawn in up to three bands
down the rectangle, each two triangles (`SpriteBatch.DrawTriangles`, with the face's shader data): what's
above the gradient in the top color, the gradient itself, and what's below it in the bottom color. The
bands' corners go through the same move, turn, scale and rotation as a sprite's would, so the face
stays on its outline.

**A sheen cuts the glyphs it falls on.** The band of light is strongest along its middle and fades in
a straight line to each edge, measured along the line from where the middle crosses the baseline, the
middle leaning with height by the tangent of its angle from upright. A glyph whose rectangle the band misses is drawn as it would be without
one: a sprite, or the gradient's bands. One it falls on is cut along the band's two edges and its
middle (and across where a gradient begins and ends), and each piece is a fan of triangles whose
corners carry the face's color there blended toward the light's. Within a piece both change evenly, so
the corners' colors draw them; the blend of the two is not quite even where a gradient and the light
overlap, and the difference isn't seen. The band is in the line's own space, before a character's move
and turn, so it follows text set along an arch.

**One call, one batch.** Every layer's quads are sprites in the batch the caller already has, in order,
so the text keeps its place in the draw order. `DrawString` opens one `UseShader` scope with the font's
`SpriteFont.Shader` for the call, never per glyph, and the batch goes back to its shader afterward.

**Fonts are data; the shader is stateless.** The text shader has no parameters. Everything it needs
besides the texture travels with each quad in its shader data, so there's one text shader for every
font, format and effect. `SpriteFontLoader` loads it under the content key `_toolkit/Shaders/spritefont`
(the content pipeline adds it to every project with `SpriteFontContent` items), and every font shares
the one instance the content service keeps. Since the shader object is the same and nothing about it
changes between fonts or effects, text in many fonts and with many effects draws together, split only
where the texture, the font's atlas page, changes. A sprite drawn with another shader between two
strings splits the draw, as any change of shader does.

`SpriteFont.Shader` can be set to a custom shader, which reads the same per-quad data.

## Glyph formats

The distance-field formats differ in what distances the shader can read:

- **MSDF** keeps three channels whose median is accurate near the edge and keeps corners sharp, but is
  meaningless far from it.
- **SDF** keeps the true distance, accurate everywhere, which rounds every corner.
- **MTSDF** keeps both: the three channels, plus the true distance in alpha.

msdf-atlas-gen describes the split as "hard effects" (perpendicular distance: mitered outlines,
thickness) and "soft effects" (true distance: glows, rounded outlines, shadows), with MTSDF the only
format that supports both.

| | Sharp face | Sharp (mitered) outlines | Miter limit | Round outlines | Softness and glow |
|---|---|---|---|---|---|
| **MTSDF** | ✓ | ✓ | ✓ | ✓ | ✓ |
| **MSDF** | ✓ | ✓ | Unlimited only | Falls back to sharp | Softens the median: accurate only for small values |
| **SDF** | Round | Falls back to round | n/a | ✓ | ✓ |
| **Bitmap** | As rasterized | Effects aren't drawn | | | |

**MTSDF is the pipeline's default** (the `GlyphFormat` metadata of `SpriteFontContent`), as the only
format with no fallbacks. MSDF is the choice for the smallest download when a game needs only sharp
text, outlines and hard shadows: its constant alpha compresses to almost nothing, while MTSDF's alpha
holds real data, which makes MTSDF pages about 30% larger.

For SDF, the median of its three identical channels is the true distance. MSDF has no true distance,
and its opaque alpha would read as "far inside", so the shader uses the median alone, which is why
sharp, unlimited corners are the only kind MSDF can draw.

What a format can't do, it draws as close as it can, and a Debug build says so once per font with a
trace, rather than failing: effects on a bitmap font, where only the face is drawn.

## Per-quad settings

Each quad's shader data (four half floats, `TEXCOORD1`) holds everything the shader needs besides the
texture. `SpriteFont.GetLayerData` writes it:

| | Meaning |
|---|---|
| X | Distance offset, in atlas texels: the font's distance-range middle plus the layer's emboldening (the face's, plus the outline width, glow width or shadow spread for an effect), limited to the encoded range |
| Y | Softness, in atlas texels: how much wider than one screen pixel the edge fades |
| Z | Miter limit: 1 for round corners, the limit for sharp ones, 1024 for unlimited |
| W | The font: its distance range in atlas texels, positive to read the median of the three channels (MSDF and SDF), negative to read the true distance in alpha as well (MTSDF), zero for a bitmap font or any other sprite |

The atlas is rendered at the font's em size, so an atlas texel is a pixel of the font's native size.
The atlas page's size, which the shader needs to turn distances into screen pixels, comes from the
texture itself (`GetDimensions`, compiled to `textureSize` for GLSL ES 3.00 on Android and the web).

W = 0 draws as the default sprite shader would: the texture's color times the vertex color. So bitmap
fonts, and sprites drawn without shader data, can share the text shader's draws. The face and the
shadow use a miter limit of 4, the same as an outline's default, so they follow the glyphs' corners.

## The shader

One formula for every quad of a distance-field font: decode the distance by the font's mode (W), limit
the three-channel median by the true distance according to the miter limit (MTSDF only), apply the
distance offset, and fade the edge over one screen pixel plus the softness. The selection by W is the
same for every quad of a string, and there's one texture sample.

- **Screen pixels.** Atlas texels per screen pixel come from the texture coordinate's derivatives,
  averaging their squares so the value doesn't change with rotation. It's capped at a quarter of the
  distance range, so heavily minified text stays legible.
- **The miter limit.** The median keeps corners sharp, so moving the edges outward by `e` extends a
  corner of angle θ to `e / sin(θ/2)` from the glyph's corner, like a miter. Limiting the median to
  the true distance plus an allowance `a` cuts the corner at `e + a`, so `a = (limit − 1) · e` cuts it
  at `limit · e`, as SVG's `stroke-miterlimit` does (the cut is round where SVG's is flat). A fixed
  allowance of `max(2 − e, 0)` is added: it keeps the face's own corners sharp, and removes the thin
  stray lines (seams) that bilinear sampling between texels with disagreeing channels can draw beside
  a stem. The true distance is encoded only within half the distance range, so a finite limit's cut is
  moved in to that distance when the limit would put it further out.
- **Derivatives are taken first** and the shader returns once, at its end. An early return compiles
  into a loop for GLSL, and WebGL's derivatives can be zero inside it, which would draw distance-field
  text without anti-aliasing on the web alone.
- **Output** is premultiplied, like the default sprite shader's. Pages are linear, straight alpha, with
  no mipmaps, sampled bilinear and clamped.

## Distance range

An effect reaches only as far from the glyphs' edges as the atlas encodes distances: half the font's
distance range (`PxRange` on `SpriteFontContent`). `GetLayerData` keeps each layer's edge, its
anti-aliasing pixel and half its softness inside that range, and a Debug build traces the first clamp
per font. The rule is the one Godot gives: set the distance range to at least twice the widest effect.
A sharp outline's corners are likewise cut back no further out than half the range.

## Cost

Each effect draws the string's glyphs once more: an outline doubles the quads, an outline and a shadow
triple them. Draws don't increase, since effects are quads in the same batch with the same shader and
texture: the draw count is set by atlas page changes, however many fonts and effects are on screen.
A gradient's face costs up to six triangles a glyph, each as much as a sprite, where a plain face is one.
A sheen costs only on the glyphs under its band (up to some thirty triangles each, with a gradient), and
nothing on the rest of the text or on text without one.
Drawing allocates nothing per frame: layers live on the stack, and a `StringBuilder`'s text is copied to
the stack, or to a pooled array when it's long.

## Not supported

- **Bevels, lighting and textured fills** (TextMeshPro has them): a custom shader reading the
  documented per-quad data covers them. A gradient is two colors, top to bottom; one at an angle, or
  of more colors, isn't.
- **Arbitrary effect stacks** (two outlines, several shadows): none of the engines above offer them as
  properties. Drawing the string twice with different effects covers the rare case.
- **Inner glows and inner shadows**, which need the face as a mask.
- **Glyphs generated at run time**, for user-typed text and large CJK character sets. Fonts are built
  ahead of time by the content pipeline.

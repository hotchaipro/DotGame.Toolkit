---
name: dotgame-toolkit
description: DotGame.Toolkit - sprite fonts and text effects, texture atlases, nine-patches, sprite animation, particles, easing and tweens, sound banks and pools, cameras and shapes, and their content items (SpriteFontContent, TextureAtlasContent, SoundBankContent). Use when adding or fixing text, sprites, animation, particles, or sounds in a DotGame game, or when toolkit content fails to build or load.
---

# DotGame toolkit

The toolkit is an optional layer over the DotGame framework (see the `dotgame-framework` skill for the game loop, threading, and the framework's content items). It adds sprite fonts and text layout, texture atlases, nine-patches, sprite animation, particles, easing and tweens, sound banks and pools, cameras, viewport scaling, and shapes, plus a content pipeline for fonts, atlases, and sound banks.

For animation, particles, easing, and tweens, read `references/animation-and-particles.md`. The repository's `docs/guide/*.md` and `docs/api/` (a page per public type) are the full documentation.

## Adding it to a game

Add the package beside the platform package, in each head or once in the shared `.props` the heads import:

```xml
<PackageReference Include="DotGame.Toolkit" Version="$(DotGameVersion)" />
```

From source, import the toolkit's include instead of the framework's (it imports the framework's, from the `DotGame.Framework` clone that `DotGameFrameworkDir` names: it's required):

```xml
<Import Project="$(DotGameToolkitDir)packaging\build\DotGame.Toolkit.targets" />
```

Then register the toolkit's content loaders (`SpriteFont`, `TextureAtlas`, `SoundBank`) before loading any of them:

```csharp
using DotGame.Toolkit;

protected override async Task OnStart()
{
    this.InitializeToolkit();
    this.font = await this.Content.LoadAsync<SpriteFont>("Fonts/title");
}
```

Without it, loads fail with "No content loader is registered for ...".

| Namespace | Has |
|---|---|
| `DotGame.Toolkit.Fonts` | `SpriteFont`, `DrawString`, `TextEffects`, `TextLayout` |
| `DotGame.Toolkit.Graphics` | `TextureAtlas`, `TextureRegion`, `NinePatch`, `Camera`, `ViewportScaler`, shape drawing, `CreateTextureFromPng`/`Jpeg` |
| `DotGame.Toolkit.Animation` | `Easing`, `Tween`, `SpriteAnimation`, `SpriteAnimationPlayer`, `PlaybackMode` |
| `DotGame.Toolkit.Particles` | `ParticleEffect`, `ParticleSystem`, renderers, curves |
| `DotGame.Toolkit.Audio` | `SoundBank`, `SoundSegment`, `SoundPool` |
| `DotGame.Toolkit` | `InitializeToolkit`, `Thickness`, `ThicknessInt` |

## Content items

| Item | Source | Output | Load as |
|---|---|---|---|
| `SpriteFontContent` | `.ttf`, `.otf`, `.ttc` (not WOFF) | `.spritefont` and atlas pages | `SpriteFont` |
| `TextureAtlasContent` | `.png` files | Packed pages and a descriptor, one atlas per item (`Name`, default `spritesheet`) | `TextureAtlas` |
| `SoundBankContent` | `.wav` files | One sound and a descriptor, one bank per item (`Name`, default `soundbank`) | `SoundBank` |

- The key is the output folder plus `Name`: `<TextureAtlasContent Include="Content\Sprites\*.png" Name="sprites" />` loads as `"Sprites/sprites"`.
- Textures and sounds are encoded by the framework's processors, so they suit each platform. Atlases and banks take `Compression` and `LossyQuality` (default 8); banks take `LosslessFormat` (`FLAC`, the default, or `WAV`) and `LossyFormat` (`Opus`). On the web, `Preload="true"` works on all three (a preloaded font preloads the text shader too).
- In a web head, `Link`s start with `wwwroot\`, as for framework items.
- Atlas packing properties: `TextureAtlasPadding` (default 0; raise it if neighbors bleed, which lossy atlases make likelier), `TextureAtlasExtrude`, `TextureAtlasMaxWidth`/`MaxHeight` (default 4096), `TextureAtlasPowerOfTwo`, `TextureAtlasSquare`. Font atlases: `SpriteFontAtlasMaxWidth`/`MaxHeight` (default 2048).
- The packing, concatenation, and font rendering run in the toolkit's build tool (from the package, prebuilt; from source, built on demand). `ToolkitContentPipelineBuildTool` points at another build; `GlyphGenExe` at another glyph renderer.
- Errors: `SND0002` (bad `LosslessFormat`) and `SND0003` (bad `LossyFormat`) as for framework sounds; an invalid `Compression` names the item.

## Fonts and text

```xml
<SpriteFontContent Include="Content\Fonts\Title.ttf" Size="36" PxRange="12" />
```

| Metadata | Default | Meaning |
|---|---|---|
| `Name` | File name | Output name and key |
| `Size` | `32` | Em size in pixels (4-512) |
| `GlyphFormat` | `Mtsdf` | `Mtsdf`, `Msdf`, `Sdf`, or `Bitmap` (hinted) |
| `Charset` | ASCII 0x20-0x7E | Literal characters to include |
| `CharsetFile` | | `;`-separated text files whose characters are included |
| `Fallback` | | `;`-separated fonts used, in order, for characters the primary lacks |
| `PxRange` | `Size / 4` (min 2) | Distance-field range in pixels |
| `PxRangeOffset` | `0` | Shifts the edge, for thicker or thinner glyphs |
| `Kerning` | `false` | Include kerning pairs |

```csharp
private static readonly TextEffects Title = new()
{
    Underlays =
    [
        TextUnderlay.Outline(Color.Black, 3),
        TextUnderlay.Shadow(Color.Black * 0.5f, new Vector2(2, 3), softness: 2),
    ],
};

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;
    using var batch = spriteBatch.Begin();
    spriteBatch.DrawString(this.font, "Level 3", new Vector2(20, 20), Color.White, scale: new Vector2(48 / this.font.EmSize), effects: Title);
}
```

- **Size is the scale.** Draw at `n` pixels with `scale: new Vector2(n / font.EmSize)`. `MeasureString` measures at the font's own size, so multiply by the scale. `LineHeight`, `Ascender`, `Descender`, and `CapHeight` place text exactly.
- **Text needs no setup.** `DrawString` switches to the toolkit's shared text shader and back inside any batch; text in several fonts and effects batches together. It takes a `string`, `StringBuilder`, or span (no garbage for changing text).
- **Only the included characters draw.** The default charset is ASCII; add a language's characters with `CharsetFile` or a `Fallback` font. No shaping or right-to-left: Arabic, Hebrew, and Indic scripts don't draw correctly.
- **Effects** are values: share one `TextEffects`, derive with `with`. Everything under the face is a `TextUnderlay` (the glyphs grown by `Width`, moved by `Offset`, softened by `Softness`, painted in `Color` or a top-to-bottom `Gradient`; `Corners`, `MiterLimit`), in `Underlays`, nearest the face first, each measured from the face's edges: `TextUnderlay.Outline(color, width, corners)`, `.Shadow(color, offset, softness, spread)`, `.Glow(color, width, softness)`. Stack them for a banner (a line, a wider band after it, each with a depth: the same underlay moved down, after it). The face has `Gradient`, `Sheen`, `Embolden` and `Softness`. Distances are in pixels of the font's `EmSize`. Every glyph's underlays draw before any face, so an outline never covers a neighbor's face. The draw color applies to the face; underlays keep their own colors. Text drawn in several calls draws each call's `TextLayers.Underlays`, then each call's `TextLayers.Face`.
- **Size `PxRange` for effects:** at least twice the widest underlay (its width plus half its softness, plus the face's emboldening). Wider ones are clipped, and a Debug build traces it once per font.
- **Size `PxRange` for the smallest draw size:** text drawn smaller than `4 / PxRange` of the font's size gets hard, jagged edges. The default range gives a 24 px font hard edges below 1.33x, so a font drawn at or below its size needs an explicit `PxRange`: 8 holds to half size, 16 to a quarter. It costs only a few texels of padding per glyph.
- **Glyph formats:** `Mtsdf` draws every effect (miter limits, round outlines, glows, soft shadows). `Msdf` is about 30% smaller, with only sharp, unlimited outline corners. `Sdf` rounds everything. `Bitmap` draws no effects.
- **`TextLayout`** wraps, aligns (`TextAlignment`), and truncates with an ellipsis (`MaxWidth`, `MaxLines`, `Scale`); set `Text`, then `layout.Draw(spriteBatch, position, color)`, which allocates nothing.
- **Custom text shaders:** set `font.Shader`. Each glyph's settings arrive as `TEXCOORD1` (X distance offset, Y softness, Z miter limit, W signed distance range in atlas texels; see `docs/guide/text.md`). The toolkit's `spritefont.slang` is the reference. Take `ddx`/`ddy` at the top of the pixel shader and return once at the end, or edges turn hard on the web.

## Atlases and nine-patches

```csharp
this.atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/sprites");
this.heart = this.atlas.GetRegion("heart");          // by the source file's name
spriteBatch.Draw(this.heart, new Vector2(20, 20), Color.White);
```

- `GetRegion` throws for an unknown name; `TryGetRegion` doesn't. A `TextureRegion` is an `ITextureSource` and draws like a texture.
- Transparent borders are trimmed when packing, but a region keeps its full logical size, so it draws where you expect.
- Lossy atlases suit painted art more than flat icons: premultiplied colors can speckle along edges. Check them on their real backgrounds.
- `new NinePatch(region, new ThicknessInt(16, 16, 16, 16))` stretches to any size and keeps its caps; `panel.Draw(spriteBatch, new RectangleInt(20, 20, 300, 120), Color.White)`. `NinePatch.FromRegion(region)` uses caps recorded in the atlas (`TextureRegion.Caps`, from tools such as TexturePacker); an `Outset` lets a baked shadow extend past the bounds.

## Sound banks and pools

```xml
<SoundBankContent Include="Content\Sounds\*.wav" Name="sounds" />
```

```csharp
var bank = await this.Content.LoadAsync<SoundBank>("Sounds/sounds");
this.coins = new SoundPool(this.Audio, bank.GetSegment("coin"), voiceLimit: 3);

this.coins.Play();                                  // or Play(volume: 0.5f)
this.coins.FadeOutAll(TimeSpan.FromSeconds(0.5), this.Time);
```

- A bank loads in one request and decodes once; each file becomes a `SoundSegment` named after it.
- A `SoundPool` plays overlapping copies up to `voiceLimit` (default `SoundPool.DefaultVoiceLimit`, 8), restarting the oldest when all are busy. `StopAll()` stops them; the optional `volumeControl` argument is the volume group they play through. A pool also takes a plain `Sound`, which stays yours to dispose.
- Part of a sound: `sound.AsSegment(offset, duration)` and `this.Audio.CreateVoice(segment)`.

## Cameras, scaling, and shapes

- `new ViewportScaler(new Vector2Int(400, 300), ViewportScaleMode.Fit)` draws in a design size on any screen: call `Update(this.ClientBounds)`, draw with `spriteBatch.Begin(scaler.Transform)`, and clip Fit's bars with `Graphics.UseScissor(scaler.Viewport)`. Modes: `Fit`, `Fill`, `Stretch`, `Expand`, `ExpandVertical`, `ExpandHorizontal`, `IntegerFit` (pixel art, with `Sampler.PointClamp`).
- `Camera` (`Position`, `Origin`, `Transform`) moves and zooms a world view; combine as `camera.Transform * scaler.Transform`. Pointer to world: `camera.ScreenToWorld(scaler.ScreenToDesign(e.Position))`.
- Keep controls inside `SafeAreaBounds`; a scaled design can reach under a notch.
- Shapes on a sprite batch: `DrawLine`, `DrawRectangle`, `FillRectangle`, `DrawCircle`, `FillCircle`, `DrawPolygon`, `FillPolygon`, `FillTriangle`, `FillTriangles`.

## Pitfalls

1. Forgetting `InitializeToolkit()` leaves no loader for fonts, atlases, or sound banks.
2. Text missing characters: they aren't in the charset. Add a `CharsetFile` or `Fallback`.
3. Jagged small text, or clipped outlines and glows: raise `PxRange`.
4. An atlas or bank key is folder plus `Name`, not the source file names; regions and segments are looked up by file name.
5. Toolkit content from a project with no platform (a class library) doesn't build: put content items in the heads.

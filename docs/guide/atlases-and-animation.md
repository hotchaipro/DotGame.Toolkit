# Atlases and animation

## Texture atlases

An atlas packs many images into one texture. Sprites from one texture
[draw together](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/graphics.md#keeping-draws-cheap), and load in one request. The images of a folder make one atlas, named `spritesheet` unless `Name` says
otherwise:

```xml
<TextureAtlasContent Include="Content\Sprites\*.png">
  <Name>sprites</Name>
</TextureAtlasContent>
```

Load the atlas, and get each image as a [`TextureRegion`](../api/TextureRegion.md) by its file's
name. Draw a region as you'd draw a texture:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    this.atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/sprites");
    this.heart = this.atlas.GetRegion("heart");
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;

    using var batch = spriteBatch.Begin();
    spriteBatch.Draw(this.heart, new Vector2(20, 20), Color.White);
}
```

Transparent borders are trimmed when packing, to fit more in, but a region still has its full logical
size, so the sprite draws where you expect.

### Compression

Atlases are lossless unless you set `Compression="Lossy"`, which suits painted art better than flat
icons. Atlas colors are premultiplied, and lossy WebP can push a color above its alpha along an edge,
which shows as speckle around light shapes on a light background. On a real icon atlas, quality 8 cut the
file to less than half with slight speckle, and quality 5 made it clearly visible. If you go lossy, check
the sprites on the backgrounds they'll be drawn over, and raise the `TextureAtlasPadding` property if
neighboring sprites bleed into each other.

A lower `LosslessQuality` is the safe middle: the atlas stays lossless, but its colors may move a
little where that saves bytes (`LosslessQuality="6"`: by 2 levels at most, of 255). It has no speckle
and keeps flat icons sharp, for a smaller saving than lossy. Atlases take their quality as textures do:
see Textures in the framework's content pipeline guide.

In Visual Studio, the [DotGame Toolkit extension](../../vs/README.md) shows an atlas's settings in its
folder's Properties window, and writes them to the item that includes the folder's images.

## Nine-patches

A [`NinePatch`](../api/NinePatch.md) stretches to any size while keeping its borders, for panels
and buttons. Its caps are the borders that don't stretch:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    this.atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/sprites");
    this.panel = new NinePatch(this.atlas.GetRegion("panel"), new ThicknessInt(16, 16, 16, 16));
}

private void DrawPanel(SpriteBatch spriteBatch)
{
    this.panel.Draw(spriteBatch, new RectangleInt(20, 20, 300, 120), Color.White);
}
```

`NinePatch.FromRegion` uses the caps of a region from an atlas exported by a tool that records them, such
as TexturePacker.

## Sprite animation

Frame-by-frame animation plays atlas regions in sequence. Name the frame images
`<animation>_<number>.png` (`run_0.png`, `run_1.png`, ...), add them to a `TextureAtlasContent` item, and
build animations from the loaded atlas:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    var atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/hero");
    this.idle = atlas.CreateAnimation("idle", framesPerSecond: 6);
    this.run = atlas.CreateAnimation("run", framesPerSecond: 12);
    this.hero = new SpriteAnimationPlayer(this.idle);
}

protected override void OnUpdate()
{
    this.hero.Play(this.isMoving ? this.run : this.idle);   // Only restarts when the animation changes
    this.hero.Update(this.Time.Delta);
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;

    using var batch = spriteBatch.Begin();
    spriteBatch.Draw(this.hero, this.position, Color.White, flip: this.facingLeft ? SpriteFlip.Horizontal : SpriteFlip.None);   // Mirrored in place
}
```

- A [`SpriteAnimation`](../api/SpriteAnimation.md) is shared and immutable. Each animated thing
  has its own [`SpriteAnimationPlayer`](../api/SpriteAnimationPlayer.md).
- Frames are ordered by number, so `run_2` comes before `run_10`. Every frame must have the same logical
  size, so export them at a fixed canvas size, and the atlas trims them.
- Playback is `Loop`, `Once`, which holds the last frame, or `PingPong`.
- To hold a frame longer, build the animation from `GetAnimationFrames` with a per-frame `Hold`, a
  multiple of one frame at the animation's frame rate: `new SpriteAnimationFrame(region, Hold: 4)`.
- `animation.GetFrame(time)` needs no player, for things that animate in lockstep.

### Acting on frames

`Play` returns a handle to that play. `When(frame, action)` runs an action whenever the play enters a
frame, for footsteps and hit frames. `Then(action)` runs when a `Once` animation finishes, so one can
chain into the next at the exact frame:

```csharp
private void Attack()
{
    this.hero.Play(this.swing)
        .When(2, () => this.HitEnemies())
        .Then(() => this.hero.Play(this.idle));
}
```

A play has one slot per frame and one for `Then`. Setting one again replaces it, and another `Play`, a
restart or `Stop` empties them, so there's nothing to unsubscribe.

The design and its reasoning are in
[sprite-animation.md](../architecture/sprite-animation.md).

## See also

- [`TextureAtlas`](../api/TextureAtlas.md), [`NinePatch`](../api/NinePatch.md),
  [`SpriteAnimationPlayer`](../api/SpriteAnimationPlayer.md)
- The toolkit sample's Atlases and Animation pages

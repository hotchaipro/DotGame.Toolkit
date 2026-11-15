# Sprite animation, particles, easing, and tweens

## Contents

- Sprite animation
- Acting on frames
- Particles
- Easing
- Tweens

## Sprite animation

Frame-by-frame animation from atlas regions. There's no separate content item: frames are ordinary images in a `TextureAtlasContent` item, named `<animation>_<number>.png`.

```xml
<TextureAtlasContent Include="Content\Sprites\hero\*.png" Name="hero" />   <!-- idle_0.png, run_00.png ... run_11.png -->
```

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    var atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/hero/hero");
    this.idle = atlas.CreateAnimation("idle", framesPerSecond: 6);
    this.run = atlas.CreateAnimation("run", framesPerSecond: 12);
    this.die = atlas.CreateAnimation("die", framesPerSecond: 10, PlaybackMode.Once);
    this.hero = new SpriteAnimationPlayer(this.idle);   // one player per animated thing
}

protected override void OnUpdate()
{
    this.hero.Play(this.isMoving ? this.run : this.idle);   // only restarts when the animation changes
    this.hero.Update(this.Time.Delta);
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;
    using var batch = spriteBatch.Begin();
    spriteBatch.Draw(this.hero, this.position, Color.White, scale: new Vector2(this.facingLeft ? -1 : 1, 1));   // a negative scale mirrors
}
```

| Type | Role |
|---|---|
| `SpriteAnimation` | Shared and immutable: `Frames`, `FramesPerSecond`, `Playback`, `Duration`, logical `Size`. `GetFrame(time)` needs no player, for things animating in lockstep. |
| `SpriteAnimationFrame` | A `TextureRegion` and its `Hold`: how many frames' worth of time it shows (1 by default). |
| `PlaybackMode` | `Loop`, `Once` (holds the last frame and finishes), `PingPong`. |
| `SpriteAnimationPlayer` | Per-instance state: `Play(animation, restart:)`, `Pause`, `Resume`, `Stop`, `Update(elapsed)`, `Speed`, `Time` (setting it seeks), `Frame`, `FrameIndex`, `IsPlaying`, `IsFinished`. |
| `SpriteAnimationHandle` | One play, returned by `Play`: `When(frame, action)` and `Then(action)`. |

- **Frame names:** the animation name is everything before the last `_`, so `idle_left_0` belongs to `idle_left`. Frames are ordered by number (`run_2` before `run_10`) and needn't start at 0 or be contiguous. Two frames with the same number (`run_1`, `run_01`) throw `InvalidDataException`; no frames throws `KeyNotFoundException`.
- **One canvas size:** every frame must have the same logical size, or construction throws. Export at a fixed canvas size and let the atlas trim.
- **Held frames:** copy the frame file (the atlas stores identical images once), or build from `atlas.GetAnimationFrames(name)` with per-frame holds: `new SpriteAnimationFrame(region, Hold: 4)`. In collection expressions, name the type (`[new SpriteAnimationFrame(r, 4)]`); target-typed `new` is ambiguous between its constructors.
- **`Play` is idempotent:** with the current animation it keeps going, a paused player resumes, and a finished `Once` stays finished. `restart: true` starts over.

## Acting on frames

```csharp
private void Attack()
{
    this.hero.Play(this.swing, restart: true)
        .When(2, () => this.HitEnemies())
        .Then(() => this.hero.Play(this.idle));
}
```

- `When(frame, action)` runs whenever the play enters that frame: every loop, both ways through a ping-pong, and even for frames a slow update skips (in order). `Then(action)` runs when a `Once` play finishes; looping plays never finish.
- A play has one slot per frame and one `Then`; setting one again replaces it, so calling `Play(x).When(...)` every frame never piles up. Another `Play` of a different animation, a restart, or `Stop` empties the slots without running them, so there's nothing to unsubscribe.
- An animation played from `Then` starts with the leftover time, so chains land on the exact frame. Replaying the same finished `Once` animation from its own `Then` needs `restart: true`.
- Actions run synchronously inside `Update`. To await a play, complete a `TaskCompletionSource` from `Then`.

## Particles

An effect is immutable data, a system runs it, and the game drives and draws it with its own sprite batch:

```csharp
var sparkle = new ParticleEffect
{
    Renderer = ParticleRenderer.Sprite(atlas.GetRegion("sparkle")),
    Blend = ParticleBlend.Additive,
    LifetimeSeconds = 0.25f,
    Rate = 36,
    Shape = EmitShape.Circle(100),
    OpacityOverLife = ParticleCurve.Between(1, 0),
};

this.particles = new ParticleSystem(sparkle);
this.particles.Burst(position, count: 20);                 // once
this.trail = this.particles.CreateEmitter(coinPosition);   // continuously

// Every frame
this.trail.Position = coinPosition;      // before Update, so emission spreads along the path
this.particles.Update(this.Time.Delta);
using (spriteBatch.Begin())
{
    this.particles.Draw(spriteBatch);    // or Draw(spriteBatch, Color.White * opacity)
}

// Done
this.trail.Stop();                       // emitted particles live on
await this.particles.WhenEmptyAsync();
```

| Type | Role |
|---|---|
| `ParticleEffect` | Immutable record. Emission: `Rate`, `RatePerDistance`, `RateOverDuration`, `Bursts`, `DurationSeconds`, `DelaySeconds`, `Capacity`, `Overflow`. Start: `Shape`, `ShapeScalesWithEmitter`, `Speed`, `DirectionDegrees`, `Scale`/`ScaleX`/`ScaleY`, `RotationDegrees`, `SpinDegrees`, `AlignToVelocity`, `Color`, `EndColor`, `ColorEase`, `Opacity`. Motion: `Gravity`, `Drag`, `Sway`, `Flip`. Over life: `ScaleOverLife`, `OpacityOverLife`. Plus `Renderer`, `Blend`, `Behavior`. Share it; make variants with `with`. |
| `ParticleSystem` | Runs one or more effects, drawn in order: `Burst(position or area, count, overrides)`, `Emit(spawn)`, `CreateEmitter(position)`, `Update(elapsed)`, `Draw`, `Clear`, `WhenEmptyAsync`, `Bounds`, `Count`, `IsActive`. |
| `ParticleEmitter` | A struct handle to a pooled emission point: `Position`, `Scale`, `TimeSeconds`, `IsActive`, `Stop()`. |
| `ParticleRenderer` | `Sprite(texture)`, `AnyOf(textures)`, `Animation(spriteAnimation, timing)`, `Stretched(texture, lengthPerSpeed)`, `Composite(renderers)`. Derive from it (a record) for your own. |
| `IParticleBehavior` | Custom per-particle code: `Start(ref Particle)` and `bool Update(ref Particle, elapsedSeconds)` (false removes it). `Particle.Origin` is where it was emitted; `Particle.Custom` reaches the renderer. |

- **Units:** angles in degrees, clockwise from the right (screen Y points down, so 90 is down); times in seconds. `FloatRange` values are written as a number or a tuple, `Speed = (100, 300)`, and sampled per particle.
- **Memory:** nothing allocates while running; buffers come from a pool. `ParticleOverrides`, `ParticleSpawn`, and `ParticleColor.Fixed`/`Between` are values (free per burst); `ParticleColor.Palette`, effects, curves, and renderers allocate, so build them once. Make one system per place particles appear, not per burst.
- **Motion is exact at any frame rate**, and a moving emitter spreads emission along its path.
- **Spaces:** particles live in whatever space the system is drawn in. For particles that move with something, draw inside `spriteBatch.Begin(transform)` and put the emitter in local space; to follow something, set the emitter's `Position` before `Update`.
- **Solid particles** (confetti, sparks): a small, padded white region tinted by the particle's color, with a `Size` on the sprite renderer.
- **Blending** is applied before the renderer sees the color (an additive particle has alpha 0, premultiplied), so every renderer blends right.
- A renderer that needs a shader (text, for example) goes in a system drawn inside its own `UseShader` scope.
- There's no ribbon or connected-trail renderer yet.

## Easing

`Easing` (in `DotGame.Toolkit.Animation`) maps progress from 0 to 1 to how far along a movement should be, with `Evaluate(t)`:

```csharp
float t = MathF.Min(this.elapsed / 0.5f, 1);
this.position = Vector2.Lerp(this.start, this.end, Easing.BackOut.Evaluate(t));
```

- Ready-made, each in `In`, `Out`, and `InOut` forms: `Quad`, `Cubic`, `Quart`, `Quint`, `Sine`, `Expo`, `Circ`, `Back`, `Elastic`, `Bounce`; and `Linear`.
- Your own settings: `Easing.Back(mode, overshoot)`, `Elastic(mode, amplitude, period)`, `Exponential(mode, exponent)`, `CubicBezier(x1, y1, x2, y2)` (as CSS), `Steps(count)`, `Spring(damping)`, `Custom(easeIn, mode)`. `Reversed()` plays one backward.
- Easings are immutable: share them. Particle curves take them too (`ParticleCurve.Between(from, to, ease)`).

## Tweens

`Tween` moves a value along an easing, driven by elapsed time, so it follows slow motion and pauses:

```csharp
private readonly Tween<Vector2> slide = Tween.Create(Vector2.Zero, new Vector2(400, 0), TimeSpan.FromSeconds(0.5), Easing.BackOut);

protected override void OnUpdate()
{
    this.slide.Update(this.Time.Delta);
    this.position = this.slide.Value;
}
```

- `Tween.Create` takes a `float`, `Vector2`, `Color`, `Angle`, or any type with a lerp function; `delay` and `playback` (`PlaybackMode`) are optional.
- `Restart()` reuses it without allocating, `Finish()` jumps to the end, `Progress` and `IsFinished` report where it is, and `await WhenFinishedAsync()` sequences tweens.

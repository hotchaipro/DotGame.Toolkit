# Particles

2D particle effects for games built on the framework: sparkles behind a flying coin, confetti at the
end of a level, smoke, snow, sparks, a trail of sprites behind a dash. An effect is described as data,
burst or attached to an emitter in one line, and drawn with the `SpriteBatch` the game already uses.

The system guarantees that motion is exact at any frame rate, that a moving emitter spreads its
particles evenly along its path, and that a running game allocates nothing in it: particles live in
pooled buffers of a fixed capacity. It depends only on the framework and on the toolkit's own types, and
knows nothing about scenes, sprites, or controls, so a game or a library built on the framework hosts it
in its own objects. The types are in `DotGame.Toolkit.Particles`, under
[`toolkit/Particles`](../../toolkit/Particles).

## Principles

1. **The caller drives.** As with `SpriteAnimationPlayer`, the game calls `Update` and `Draw` and sets
   positions. The system never reaches out to anything else (no anchors, no scene graph, no callbacks
   into game objects), so it needs no interfaces to the outside.
2. **Data is shared, state is per instance.** `ParticleEffect` is to `ParticleSystem` what
   `SpriteAnimation` is to `SpriteAnimationPlayer`.
3. **Reuse what's there.** Particles draw `ITextureSource`s (whole textures or atlas regions) and play
   `SpriteAnimation`s. Spaces and transforms come from `SpriteBatch.Begin(transform)`.
4. **No global state.** Behaviors and easings are referenced as objects, not looked up by name.
5. **Open where games differ, shared where they don't.** A game customizes how particles move (an
   `IParticleBehavior`) and how they look (a `ParticleRenderer` subclass). The system does everything in
   between: emission, motion, curves over life, color changes, sway, flip, and blending.

## The types

```csharp
// Describe, once (in a theme, for example)
var atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/effects");

var sparkle = new ParticleEffect
{
    Renderer = ParticleRenderer.Sprite(atlas.GetRegion("sparkle")),
    Blend = ParticleBlend.Additive,
    LifetimeSeconds = 0.25f,
    Rate = 36,
    Shape = EmitShape.Circle(100),
    OpacityOverLife = ParticleCurve.Between(1, 0),
};

var particles = new ParticleSystem(sparkle);

// Burst at a point, or attach an emitter and move it
particles.Burst(position, count: 20);
var trail = particles.CreateEmitter(coin.Position);

// Every frame
trail.Position = coin.Position;          // particles spread along the path it moved
particles.Update(this.Time.Delta);

using (spriteBatch.Begin())
{
    particles.Draw(spriteBatch);
}

// When the coin lands
trail.Stop();                            // emitted particles live on
await particles.WhenEmptyAsync();
```

| Type | Role | Lifetime |
|---|---|---|
| [`ParticleEffect`](../../toolkit/Particles/ParticleEffect.cs) | Immutable description: renderer, blend, emission, start values, motion, changes over life | Defined once, shared; variants with `with` |
| [`ParticleRenderer`](../../toolkit/Particles/ParticleRenderer.cs) | Abstract record that draws an effect's particles from their final state; built-in subclasses, and a game's own | Part of an effect; shared |
| [`ParticleSystem`](../../toolkit/Particles/ParticleSystem.cs) | Runs one or more effects: pools, simulates, and draws their particles | One per place particles appear |
| [`ParticleEmitter`](../../toolkit/Particles/ParticleEmitter.cs) | Handle to a running emission point: `Position`, `Teleport`, `Scale`, `Stop` | Created by a system; ends when stopped or its effects' durations end |
| [`IParticleBehavior`](../../toolkit/Particles/IParticleBehavior.cs) | Custom per-particle code for what the effect's values don't cover | Shared; referenced from an effect |

Supporting value types: `FloatRange` (a range sampled per particle; a `float` or a `(min, max)` tuple
converts to one), `EmitShape` (`Point`, `Circle`, `Ring`, `Rectangle`, `Line`), `ParticleColor`
(`Constant`, `Between`, `Palette`; a `Color` converts to one), `ParticleCurve` and `ParticleCurveKey`,
`ParticleBurst`, `ParticleSway`, `ParticleFlip`, `ParticleOverrides` (per-burst changes),
`ParticleSpawn` (one placed particle), `Particle` (simulation state, for behaviors), and
`ParticleDrawState` (final drawn state, for renderers).

## Effects

A `ParticleEffect` is a sealed record with `init` properties, in groups:

- **Appearance and pooling:** `Renderer` (required), `Blend` (`Alpha` or `Additive`), `Capacity`
  (default 100), and `Overflow` (`DropNew` or `ReplaceOldest`) for a particle emitted at capacity.
- **Life:** `LifetimeSeconds`.
- **Emission by an emitter:** `Rate` (per second), `RatePerDistance` (per pixel the emitter moves),
  `RateOverDuration` (a multiplier curve over `DurationSeconds`), `Bursts` (counts at times in the
  emitter's run), `DurationSeconds` (null runs until stopped), and `DelaySeconds`.
- **Start:** `Shape`, `ShapeScalesWithEmitter`, `Speed`, `DirectionDegrees`, `Scale`, `ScaleX`, `ScaleY`,
  `RotationDegrees`, `SpinDegrees`, `AlignToVelocity`, `Color`, `EndColor`, `ColorEase`, and `Opacity`.
- **Motion:** `Gravity`, `Drag` (per axis, the fraction of speed lost per second), `Sway`, and `Flip` (a
  flutter, as of paper turning over).
- **Over life:** `ScaleOverLife` and `OpacityOverLife`, multiplier curves from 0 to 1 of a particle's
  life.
- **Custom code:** `Behavior`.

Angles are in degrees, clockwise from the right (screen Y points down), times in seconds, distances in
pixels. Times are float seconds rather than `TimeSpan`s, because effects are authored by hand and
`LifetimeSeconds = (0.5f, 1)` reads better than two `TimeSpan`s. `Update` itself takes a `TimeSpan`, so
`this.Time.Delta` passes straight in.

Curves and eases are objects. `ParticleCurveKey.Ease` and `ColorEase` take the toolkit's `Easing`
(`Easing.CubicOut` and the rest, in `DotGame.Toolkit.Animation`), which keeps an effect pure data that can
be compared and shared, and games use the same easings for their own tweens.

## The system

A `ParticleSystem` is built from one or more effects, kept as layers drawn in order. Every emission
emits each effect's particles: a burst of 20 in a system of a glow and a spark effect emits 20 of each.

```csharp
public ParticleSystem(params ParticleEffect[] effects);
public ParticleSystem(int seed, params ParticleEffect[] effects);

public IReadOnlyList<ParticleEffect> Effects { get; }
public Rectangle? Bounds { get; set; }
public int Count { get; }
public bool IsActive { get; }                   // particles or running emitters

public void Burst(Vector2 position, int count, in ParticleOverrides overrides = default);
public void Burst(Rectangle area, int count, in ParticleOverrides overrides = default);
public void Emit(in ParticleSpawn spawn);
public ParticleEmitter CreateEmitter(Vector2 position);
public void Clear();
public Task WhenEmptyAsync(CancellationToken cancellationToken = default);
public void Reserve();
public void ReleaseReserved();

public void Update(TimeSpan elapsed);
public void Draw(SpriteBatch spriteBatch);
public void Draw(SpriteBatch spriteBatch, Color color);
```

- `Burst(area, ...)` emits anywhere in a rectangle, unless the overrides give a shape.
  `ParticleOverrides` can change a burst's `Shape`, `DirectionDegrees`, `Color`, and `Variant`.
- `Emit` places one particle exactly, in the effect its `ParticleSpawn.Layer` names, with optional
  velocity, lifetime, scale, rotation, color, opacity, and variant in place of the effect's.
- `Bounds` removes a particle that leaves it, such as confetti falling below the screen. It and the
  burst area are the framework's float `Rectangle`, since particle space is continuous.
- `WhenEmptyAsync` completes when there are no particles and no running emitters. It completes only
  through `Update` or `Clear`, so a system that's no longer updated never completes it; the token covers
  that case, as when a scene that stops updating the system leaves. Its continuation runs after the
  update that emptied the system, not in the middle of it.
- `Draw(spriteBatch, color)` tints every particle: `Color.White` leaves them unchanged, and
  `Color.White * 0.5f` fades the whole system to half opacity. There are no `Opacity` or `IsVisible`
  properties, because both would be state the caller already owns: pass a tint, or don't call `Draw`.

### An update

`Update(elapsed)` does nothing for an idle system or a non-positive time. Otherwise, in order:

1. **Existing particles move.** Each effect's particles age; those past their lifetime are removed. The
   rest move along their exact path under `Gravity` and `Drag` for the whole frame, then the effect's
   behavior runs (and can remove the particle), then `Bounds` is checked. Survivors keep their order.
2. **Emitters emit.** Each running emitter emits each effect's particles for the frame, then the
   emitters that ended are released.
3. **Waiters complete** if the system became empty.

New particles are emitted after the move, and each is aged and moved by the part of the frame since it
was emitted, so no particle moves twice and none is a frame behind.

**Exact motion.** Along each axis, velocity under constant acceleration `g` and drag `k` follows
`v(t) = g/k + (v0 - g/k)·e^(-kt)`, and the position its integral. The system applies that closed form
for the frame's time rather than stepping it, so two steps give the same result as one step of their
total, and the same effect looks the same at 30, 60 or 144 frames per second. With drag, particles
approach a top speed of gravity divided by drag (a falling leaf's).

**Values over life are computed from age at draw time.** A particle's simulation state
([`Particle`](../../toolkit/Particles/Particle.cs)) holds its starting scale, rotation, spin, colors,
opacity, sway, and flip. The curves, spin, sway, flip, and color change are evaluated from its age when
it's drawn, so they're exact at any frame rate too and cost nothing for particles that aren't drawn.

### Emission

For each effect, an emitter works out the part of the frame that falls in the effect's run (after
`DelaySeconds`, before `DelaySeconds + DurationSeconds`), then:

- **Continuous emission is a budget.** The frame adds `Rate × time` (times `RateOverDuration` at the
  middle of the frame's part of the duration) plus `RatePerDistance × distance moved`. The whole part is
  emitted, and the fraction carries over to the next frame, so a rate of 36 emits 36 a second whatever
  the frame rate. A new emitter starts with a random fraction, so emitters started together don't emit in
  step.
- **Emission is spread along the path.** The frame's particles are spaced evenly in time across it, at
  the positions the emitter passed through between its last position and this one, each aged by the
  rest of the frame. A fast-moving emitter leaves an even trail, not clumps at each frame's position.
- **Bursts fire once each**, in time order, at the point on the path for their time.
- An effect's run ends at its duration; an emitter ends when every effect's run has ended, or when it's
  stopped. Its particles live out their lives.

### Capacity

Each effect's particles are a ring buffer of its `Capacity`, oldest first. At capacity, `DropNew`
discards the new particle and `ReplaceOldest` makes the oldest one make way. Removing expired particles
closes the gaps in order, so drawing is always oldest first, which is right for 2D bursts and trails of
sprites.

## Emitters

```csharp
public readonly struct ParticleEmitter : IEquatable<ParticleEmitter>
{
    public Vector2 Position { get; set; }   // set before Update; emission spreads from the last position to this one
    public void Teleport(Vector2 position); // moves it without emitting along the way
    public float Scale { get; set; }        // 1 to start; scales Shape for effects with ShapeScalesWithEmitter
    public float TimeSeconds { get; }       // how long it has run
    public bool IsActive { get; }           // false once stopped or ended
    public void Stop();
}
```

An emitter is a handle (system, slot, generation) to one of the system's pooled emitter slots, so
creating one allocates nothing once the pool has grown to the most that run at once (it starts at 4
slots and doubles). A handle to an emitter that has ended stays ended after its slot is reused: it reads
as inactive, and setting it or stopping it does nothing, so a stale handle can't move another emitter. A
stopped emitter stays in the running list until the next update, so a slot is never reused while an
update is walking the list.

There's no `Follow`. To follow something, set `Position` (and `Scale`, if the shape should scale with
it) each frame before `Update`, and call `Stop()` when it should end. The path interpolation that makes
trails smooth needs only consecutive positions, so nothing has to implement an anchor interface.
`Teleport` covers a respawn or a jump across the screen, which shouldn't leave a streak.

## Spaces

A system has no parent object. Particle positions, emitter positions, burst positions, and `Bounds` are
all in the system's space, which is whatever space the caller draws it in:

- **Particles that trail behind a moving thing**, in screen or world space, need nothing special: draw
  with the batch's usual transform.
- **Particles that move with something** (a ship's exhaust that turns with the ship) are drawn inside
  `spriteBatch.Begin(transform)` with that thing's transform, and their emitter's position is in its
  local space.

This is the framework's existing mechanism for spaces, so the system has no transform math of its own.

## Drawing and renderers

`Draw` works out each effect's particles' final state and hands them to the effect's renderer, effect by
effect in order, into the batch the caller has begun:

```csharp
public abstract record ParticleRenderer
{
    public abstract void Draw(SpriteBatch spriteBatch, ReadOnlySpan<ParticleDrawState> particles);

    public static SpriteParticleRenderer Sprite(ITextureSource texture);
    public static SpriteParticleRenderer AnyOf(params ITextureSource[] textures);
    public static AnimatedSpriteParticleRenderer Animation(SpriteAnimation animation,
        ParticleAnimationTiming timing = ParticleAnimationTiming.OverLife);
    public static CompositeParticleRenderer Composite(params ParticleRenderer[] renderers);
    public static StretchedSpriteParticleRenderer Stretched(ITextureSource texture, float lengthPerSpeed);
}
```

[`ParticleDrawState`](../../toolkit/Particles/ParticleDrawState.cs) is everything the system resolves
for a particle:

| Member | Meaning |
|---|---|
| `Position` | In the system's space, after sway |
| `Scale` | After `ScaleOverLife` and flip |
| `Rotation` | After spin, and from the velocity's direction with `AlignToVelocity` |
| `Color` | Premultiplied, after the color change, opacity, the `Draw` tint, and blend |
| `Velocity` | Pixels per second |
| `AgeSeconds`, `NormalizedAge` | Seconds lived, and 0 to 1 over its life |
| `Variant` | A random number fixed for the particle's life |
| `Custom` | What the effect's behavior left in `Particle.Custom` |

The contract:

- **All of an effect's particles come at once**, oldest first, in one span. That's one virtual call per
  effect per frame rather than per particle, and a renderer can do per-draw work once. The span is a
  buffer of the effect's capacity, pooled with the particles, so drawing allocates nothing.
- **Invisible particles are left out**: those whose opacity is 0, and those the tint makes fully
  transparent.
- **Blending is applied before the renderer sees the color.** For `ParticleBlend.Additive`, the color's
  alpha is 0 (premultiplied additive), so every renderer blends correctly without knowing about blend
  modes, and an effect's blend works with any renderer.
- **`Variant`, not a texture index.** A stored frame index would mean something only to texture
  renderers. A random number fixed for each particle's life lets any renderer make a stable choice:
  `AnyOf` draws `textures[Variant % count]`, and a text renderer could pick a word.
  `ParticleOverrides.Variant` and `ParticleSpawn.Variant` let a burst or a placed particle choose, so with
  `AnyOf` a variant of 2 draws the third texture.
- **`Custom` carries data from behavior to renderer**: heat, charge, a letter index.
- **A renderer draws into the batch it's given.** The caller owns `Begin`, so a renderer can't change
  the transform or the batch's settings. A renderer whose drawing needs settings of its own, such as a
  custom shader, goes in a system drawn in its own `Begin` with those settings. Text needs nothing
  special: `DrawString` draws with its font's shader whatever the batch is using.

Renderers are records, not an interface: immutable data like the rest of the effect, so `with` makes
variants (`Sprite(white) with { Size = new(15, 25) }`) and one renderer can be shared by many effects.
An abstract base also leaves room for virtual members without breaking subclasses.

The renderer is part of the effect because an effect describes how particles look as well as how they
move, and values like `ScaleOverLife`, `OpacityOverLife`, and `EndColor` connect the two. An effect has
one renderer; `CompositeParticleRenderer` draws the same particles with several.

### Built-in renderers

- **[`SpriteParticleRenderer`](../../toolkit/Particles/SpriteParticleRenderer.cs)**, from `Sprite(texture)`
  or `AnyOf(textures)`, draws `ITextureSource`s through the toolkit's trim-aware `SpriteBatch.Draw`, so
  atlas regions work like any other sprite. `Size` (`Vector2?`) is the size a particle draws at before
  its scale, defaulting to the texture's logical size. `Pivot` (0 to 1, default `(0.5, 0.5)`) is where
  the texture is placed and what it scales and rotates around. It's relative because one renderer can
  draw textures of different sizes, which is also why it's `Pivot` and not `Origin`, which is in pixels on
  `SpriteBatch.Draw`.
- **[`AnimatedSpriteParticleRenderer`](../../toolkit/Particles/AnimatedSpriteParticleRenderer.cs)**, from
  `Animation(animation, timing)`, plays a `SpriteAnimation` (its `SpriteAnimation` property), so frames,
  holds, and playback modes are authored like any sprite animation. It has the same `Size` and `Pivot`.
  `OverLife` stretches one pass over each particle's life (smoke that puffs out and dissipates), ending
  on the last frame even for a looping animation. `Loop` plays at the animation's own frame rate from the
  particle's age (a spinning coin), with the animation's `Playback` applied as usual.
- **[`StretchedSpriteParticleRenderer`](../../toolkit/Particles/StretchedSpriteParticleRenderer.cs)**, from
  `Stretched(texture, lengthPerSpeed)`, draws a texture, drawn pointing right, centered on the particle,
  rotated along its velocity and lengthened by `LengthPerSpeed` per pixel per second of speed beyond its
  scaled width: sparks, rain, and speed lines. `MaxLength` (null for no limit) keeps a fast particle from
  stretching across the screen. A particle that isn't moving draws at its own rotation and length.
- **[`CompositeParticleRenderer`](../../toolkit/Particles/CompositeParticleRenderer.cs)**, from
  `Composite(renderers)`, draws the same particles with each renderer in turn, such as a soft glow under
  a crisp core. Every renderer draws into the same batch, so the effect has one blend mode: a glow that
  needs additive blending under a normal core is two effects.

**Solid particles are tinted textures.** There's no rectangle renderer: confetti, sparks, and rain are a
small white texture tinted by the particle's color, from the game's atlas or created at run time.
`Sprite(white) with { Size = new(15, 25) }` draws 15×25 confetti, and games can shape solid particles
(rounded confetti, soft-edged sparks) without code. A tiny atlas region stretched large samples its
neighbors unless the atlas pads or extrudes it, so use a white region of a few pixels with padding.

The frame of an animated particle is worked out by the renderer from the particle's age, where some
engines compute a flipbook index during simulation and pass it to the renderer. In 2D the frame depends
only on age, and working it out in the renderer reuses `SpriteAnimation` as it is. A behavior that wants
to choose frames itself can write `Custom` for a custom renderer to read.

### A custom renderer

Score pop-ups ("+10") drawn with a sprite font:

```csharp
public sealed record TextParticleRenderer(SpriteFont Font, string Text, float Size) : ParticleRenderer
{
    public override void Draw(SpriteBatch spriteBatch, ReadOnlySpan<ParticleDrawState> particles)
    {
        var center = this.Font.MeasureString(this.Text) * 0.5f;
        float fontScale = this.Size / this.Font.EmSize;

        foreach (ref readonly var particle in particles)
        {
            spriteBatch.DrawString(this.Font, this.Text, particle.Position, particle.Color,
                rotation: particle.Rotation, origin: center, scale: particle.Scale * fontScale);
        }
    }
}

var popup = new ParticleEffect
{
    Renderer = new TextParticleRenderer(font, "+10", Size: 32),
    LifetimeSeconds = 0.8f,
    Speed = 120,
    DirectionDegrees = 270,                                  // straight up
    Drag = new Vector2(0, 3),
    ScaleOverLife = ParticleCurve.Between(0.6f, 1.2f, Easing.BackOut),
    OpacityOverLife = ParticleCurve.FromKeys(new(0, 1), new(0.6f, 1), new(1, 0)),
};
```

A nine-patch shard, or a texture whose color cycles by `Variant`, fits the same shape: a few lines in the
game, and no change to the toolkit.

## Behaviors

```csharp
public interface IParticleBehavior
{
    void Start(ref Particle particle) { }                    // after the effect's values are applied
    bool Update(ref Particle particle, float elapsedSeconds); // after the move; false removes it
}
```

`Particle` is a struct with public fields (`Position`, `Velocity`, `Origin`, `AgeSeconds`,
`LifetimeSeconds`, `Scale`, `Rotation`, `Spin`, `Color`, `EndColor`, `Opacity`, `Variant`, `Custom`),
because behaviors update thousands of particles a frame in place. `Origin` is where it was emitted from,
before the shape spread it out, for behaviors that pull particles toward or around that point. One
behavior instance serves every particle of every effect that uses it, so per-particle state goes in the
particle (`Custom`).

A behavior runs in the middle of an update, so it must not create emitters, burst, or clear the system
it belongs to. To stay correct at any frame rate, it changes things by rates times `elapsedSeconds`, or
computes them from `AgeSeconds`.

Behaviors and renderers are the two extension points, and they split cleanly: a behavior changes a
particle's simulation state while updating, and a renderer reads its final drawn state while drawing.

Behaviors are not registered by name. A process-wide registry is global state in a library, and turns a
typo into a run-time error. A registry would make effects serializable, but nothing serializes them; a
loader for effect files would be the place for a name-to-behavior map, rather than every effect.

## Memory

A game's steady state allocates nothing in the particle system:

- **Particle buffers are pooled.** Each effect's particles and draw states are buffers of its
  `Capacity`, rented from `ArrayPool<T>.Shared` when its first particle is emitted and returned when its
  last one ends (or on `Clear`). An idle system holds no particle memory, so a system for a view that
  seldom emits costs only the small system object, and busy systems reuse the same buffers. Particles
  hold no references, so buffers aren't cleared on return.
- **`Reserve` takes the buffers early** and keeps them while the system is idle, until `ReleaseReserved`.
  Without it they arrive on the first burst, the frame of the first explosion. Call it on a loading
  screen.
- **Emitters are pooled slots** (see Emitters).
- **Per-burst values are values.** `ParticleOverrides`, `ParticleSpawn`, `EmitShape`, `FloatRange`, and
  `ParticleColor` are structs, so
  `Burst(position, n, new ParticleOverrides { Color = ParticleColor.Between(c, Color.White) })` allocates
  nothing. `ParticleColor.Palette` copies its array, so make palettes once.
- **Effects, curves, and renderers are built once** and shared. `ParticleCurve` is a class, and
  `FromKeys`, `AnyOf`, and `Composite` copy their arrays.
- **What does allocate:** constructing a system (the system, one small object per effect, and a copy of
  the effects array), growing the emitter pool past its peak, and `WhenEmptyAsync` while the system is
  active (a task, and a registration for a token that can be canceled).

## Randomness

Sampling uses a small internal generator, so no random number generator appears in the public API; the
`Sample` methods of `FloatRange`, `EmitShape`, and `ParticleColor` are internal. The
`ParticleSystem(int seed, ...)` constructor makes a system's random choices follow from the seed, so the
same emissions and updates give the same particles, for replays and for effects that must look the same
every time.

## Threading

A system is a plain object with no locks: call it from one thread, the one that updates and draws it.
Behaviors and renderers run synchronously inside `Update` and `Draw`.

## How it compares

| System | Name of the drawing part | Extensible |
|---|---|---|
| Unity (built-in particle system) | `ParticleSystemRenderer` | No (custom shaders and vertex streams) |
| Unreal Niagara | Renderers (`UNiagaraRendererProperties`) | Yes, in C++ |
| PopcornFX | Renderers | Partly |
| Cocos Creator | `ParticleSystemRenderer` | No |
| libGDX (3D particles) | `ParticleControllerRenderer` | Yes |
| tsParticles (web) | Shape drawers (`IShapeDrawer`) | Yes, registered by name |
| Godot, Phaser, MonoGame.Extended, libGDX 2D | No abstraction: a texture or frame on the emitter | No |

The large systems call it a **renderer**, so this design uses `ParticleRenderer`, with subclasses named
like Niagara's "Sprite Renderer". The simple 2D libraries have only a texture property, which is what a
closed set of visuals amounts to: each new kind of drawing becomes a new case inside the library.
tsParticles is closest to this design, an open drawing type with text as a built-in kind, but its name,
"shape", would clash with `EmitShape`. Unity's and Cocos's renderers also only draw what the system gives
them, so the name means what developers coming from them expect.

- **Simulation and drawing are separate**, as in Unity and Niagara: the effect's values and a behavior
  write per-particle values, and the renderer only reads them.
- **The values a renderer reads** correspond to Niagara's renderer bindings: position, color, sprite
  size (`Scale`), sprite rotation (`Rotation`), velocity, sub-image index (`Variant`), and dynamic
  material parameters (`Custom`, Unity's custom data).
- **One renderer per effect**, where Niagara allows several renderers to draw the same particles (a
  sprite plus a light). Two effects can't do that, because each simulates its own particles; a composite
  renderer does.
- **Blend mode stays on the effect.** Unity and Niagara put it on the renderer's material, while Phaser,
  Defold, and Godot put it on the emitter. Here additive blending works through the color's alpha, so it
  applies to any renderer without the renderer knowing about it.

## Limits

- Ribbons (a strip connecting an emitter's particles) and trails per particle are not supported. A
  trail is a stream of sprites, or a game's own strip drawn with the framework's
  `SpriteBatch.DrawTriangles`.
- Particles always draw oldest first; there's no sort mode.
- Each effect has one blend mode, and a renderer can't change the batch's shader or transform.
- Effects are built in code; there's no effect file format.

## Tests

`test/DotGame.Toolkit.Tests/Particles/` runs without a graphics device. The system fills the draw-state
buffer in an internal method, which tests call directly (through `InternalsVisibleTo`) and which `Draw`
calls before handing the span to the renderer; the built-in renderers' frame, origin, and size choices
are internal `Resolve` helpers tested the same way, with regions that have a size and no texture. The
tests cover frame-rate independence (the same positions under gravity and drag, and the same emitted
counts, at several frame rates), moving emitters and `RatePerDistance`, emission budgets, bursts, delay and duration, capacity
and overflow with draw order after wraparound, `Bounds`, `Clear` and `WhenEmptyAsync`, the draw states
(sway, scale, flip, rotation, color, additive alpha, a stable `Variant`, `Custom`), the built-in
renderers including trim parity for pivots, curve and argument validation, and
`ParticleSystemTests.SteadyState_AllocatesNothing`, which checks that after a warm-up, cycles of
emitters, bursts with per-burst colors, placed spawns, and the system emptying allocate 0 bytes.

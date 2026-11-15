# Sprite animation

Frame-by-frame ("flipbook") sprite animation for 2D games: a walk cycle, a spinning coin, an
explosion. The frames are ordinary regions of a texture atlas, so going from a folder of PNGs to an
animated character takes a few lines and no new file format or tool. Animations are shared, immutable
data; each animated thing has its own player, which the game updates and draws itself. Playback is
time-based and exact: the frame shown and the actions run depend only on the game time passed in, not
on the display's frame rate.

Like the rest of the toolkit, it depends only on the framework (`SpriteBatch`, `Texture`) and on the
toolkit's atlas types. The types are in `DotGame.Toolkit.Animation`.

## The types

```csharp
// Load: frames are atlas regions named run_0.png, run_1.png, ...
var atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/hero");
var idle  = atlas.CreateAnimation("idle", framesPerSecond: 6);
var run   = atlas.CreateAnimation("run",  framesPerSecond: 12);
var die   = atlas.CreateAnimation("die",  framesPerSecond: 10, PlaybackMode.Once);

// Each character gets its own player; animations are shared.
var hero = new SpriteAnimationPlayer(idle);

// Update: say which animation the state calls for; it only restarts when that changes.
hero.Play(isMoving ? run : idle);
hero.Update(this.Time.Delta);

// Draw: like any other sprite.
var center = new Vector2(idle.Size.X, idle.Size.Y) / 2;
spriteBatch.Draw(hero, position, Color.White, origin: center,
    flip: facingLeft ? SpriteFlip.Horizontal : SpriteFlip.None);
```

| Type | Role | Lifetime |
|---|---|---|
| [`SpriteAnimation`](../../toolkit/Animation/SpriteAnimation.cs) | An immutable sequence of frames, a frame rate, and a playback mode | Built once at load, shared like a texture |
| [`SpriteAnimationFrame`](../../toolkit/Animation/SpriteAnimationFrame.cs) | One frame: a `TextureRegion` and its `Hold` | Part of an animation |
| [`PlaybackMode`](../../toolkit/Animation/PlaybackMode.cs) | `Loop`, `Once`, or `PingPong` (shared with `Tween<T>`) | Part of an animation |
| [`SpriteAnimationPlayer`](../../toolkit/Animation/SpriteAnimationPlayer.cs) | Per-instance playback state: which animation, what time, playing or not | One per animated thing |
| [`SpriteAnimationHandle`](../../toolkit/Animation/SpriteAnimationHandle.cs) | One play of an animation, for setting frame actions (`When`) and a finish action (`Then`) | Returned by `Play`; valid until the play is interrupted |
| [`SpriteAnimationExtensions`](../../toolkit/Animation/SpriteAnimationExtensions.cs) | `CreateAnimation` and `GetAnimationFrames` on `TextureAtlas`, and `SpriteBatch.Draw` for a player | Static |

Data is shared and cheap to hold; state is per instance and cheap to create. Particles use the same
split (`ParticleEffect` and `ParticleSystem`), and play `SpriteAnimation`s too.

## Authoring: frames come from the atlas

There is no animation content item. Frames are regions of a `TextureAtlasContent` item, found by name:

```
Sprites/hero/
  idle_0.png  idle_1.png  idle_2.png  idle_3.png
  run_00.png  run_01.png  ...  run_11.png
  die_0.png   ...
```

`atlas.CreateAnimation("run", ...)` collects every region named `run_<number>` and orders them
**numerically**, so `run_2` comes before `run_10` however they're padded. This gets everything the atlas
pipeline already does:

- **Trimming.** Transparent borders are trimmed from each frame, but every `TextureRegion` keeps its
  untrimmed logical size, and the toolkit's trim-aware `SpriteBatch.Draw` keeps the origin on the same
  logical pixel. A character doesn't jitter as frames trimmed differently play.
- **Duplicate frames are free.** The packer stores identical images once, so a pose held by copying its
  file (`run_03` copied as `run_04`) costs no texture memory. `SpriteAnimationFrame.Hold` does the same in
  code.
- **Packing and encoding** per platform, with no extra work.

Naming rules, from [`GetAnimationFrames`](../../toolkit/Animation/SpriteAnimationExtensions.cs):

- A frame is named `<animation name>_<digits>`, such as `run_0` or `explosion_017`. The last `_`
  separates the number, so `idle_left_0` is frame 0 of `idle_left`. A region whose suffix isn't a plain
  number (parsed with `NumberStyles.None`) is ignored.
- Numbers needn't start at 0 or be contiguous. Two regions with the same number (`run_1` and `run_01`)
  throw `InvalidDataException`; no regions at all throw `KeyNotFoundException`.
- Names are compared without case, like all atlas lookups.
- Several animations and plain sprites can share one atlas. One atlas per character or per effect is
  the usual setup.

## `SpriteAnimation`

An animation holds its frames, `FramesPerSecond` and `Playback`. A frame shows for its `Hold` divided by
the frame rate: a `Hold` of 1 is one frame, 3 holds it three times as long, 0.5 shows it for half a
frame. `GetFrameDuration(index)` gives that time, and `Duration` is one pass (forward and back for
`PingPong`, whose end frames aren't repeated: 0 1 2 3 2 1 0 1 ...).

The constructor checks its invariants, so an animation that exists is valid:

- At least one frame, and every frame has a region and a positive, finite `Hold`.
- `framesPerSecond` is positive and finite.
- **Every frame has the same logical size** (`TextureRegion.Size`), exposed as `Size`. Frame-by-frame
  animation is one canvas, redrawn; with one size, an origin such as `Size / 2` means the same point on
  every frame. Export frames at a fixed canvas size; the atlas trims them anyway.
- No frame is so short that it rounds to no time at the frame rate.

There are two constructors, one from `TextureRegion`s (every `Hold` 1) and one from
`SpriteAnimationFrame`s. In a collection expression, target-typed `new` is ambiguous between them
(`[new(region, 4)]`), so name the type: `[new SpriteAnimationFrame(region, 4)]`.

`GetFrameIndex(time)` and `GetFrame(time)` are pure: which frame shows at a time since the animation
started. Things that animate in lockstep, such as a field of spinning coins, need no players:

```csharp
var frame = coinSpin.GetFrame(this.Time.Total);
var origin = new Vector2(coinSpin.Size.X, coinSpin.Size.Y) / 2;
foreach (var coin in coins)
{
    spriteBatch.Draw(frame, coin.Position, Color.White, origin: origin);
}
```

Negative times show the first frame; times past the end of a `Once` animation show its last.

**Lookup.** The constructor builds the time each frame ends, in ticks, from the running sum of holds
divided by the frame rate, so rounding doesn't accumulate across frames. Lookup is a binary search over
those ends. For `Loop` the time wraps; for `PingPong` a time on the way back is mirrored into the
forward pass. The constructor also builds the list of frames entered during one pass, with their start
times (0 to n-1, then n-2 down to 1 for `PingPong`), which the player uses to find the frames an update
passes through.

An animation holds no textures. Its regions belong to the atlas, and disposing the atlas invalidates its
animations, as it does its regions.

## Building animations from an atlas

`TextureAtlas` knows nothing about animation. It exposes `RegionNames`, every region's name in no
particular order, and the extensions in `SpriteAnimationExtensions` do the rest:

```csharp
public static SpriteAnimation CreateAnimation(this TextureAtlas atlas, string name,
    float framesPerSecond, PlaybackMode playback = PlaybackMode.Loop);

public static IReadOnlyList<TextureRegion> GetAnimationFrames(this TextureAtlas atlas, string name);
```

Keeping the convention outside the atlas leaves the atlas a plain name-to-region store, and lets a game
apply its own naming convention to `RegionNames` the same way. `GetAnimationFrames` scans the names once
per call, which is fine for a load-time call over at most a few hundred regions. Each `CreateAnimation`
builds a new animation, so call it once at load and keep the result.

Per-frame holds are set by building the animation from `GetAnimationFrames`:

```csharp
// Wind-up, then hold the strike pose (frame 2) for four frames.
var regions = atlas.GetAnimationFrames("attack");
var frames = new SpriteAnimationFrame[regions.Count];
for (int i = 0; i < frames.Length; i++)
{
    frames[i] = new SpriteAnimationFrame(regions[i], Hold: (i == 2) ? 4 : 1);
}

var attack = new SpriteAnimation(frames, framesPerSecond: 15, PlaybackMode.Once);
```

## `SpriteAnimationPlayer`

A player has an `Animation` (null draws nothing), a `Time` since the animation started (scaled by
`Speed`), and `IsPlaying` and `IsFinished`. `FrameIndex` and `Frame` are the frame showing at the player's
time (-1 and null with no animation).

- `Play(animation, restart: false)` switches to an animation and plays it from the start. If it's
  already the current animation, it keeps going: a paused player resumes, a finished one stays finished,
  and the returned handle is the current play's. `restart: true` starts it over.
- `Pause()` holds the current frame and keeps the play and its actions. `Resume()` continues, or plays a
  finished animation again as a new play, without the finished play's actions.
- `Stop()` returns to the first frame and interrupts the play.
- `Update(elapsed)` advances by the game time passed in, running the frame actions of the frames it
  enters and the finish action of a play that finishes. Call it once per frame.
- `Speed` is zero or positive and finite. At 0 the frame holds but `IsPlaying` stays true; `Pause` is the
  way to stop. Reversed playback is an animation built from the frames in reverse order, which keeps
  `Speed` non-negative.
- Setting `Time` seeks. It wraps for `Loop` and `PingPong` and is clamped to `Duration` for `Once`, so
  it never grows without bound in a long-running game. Seeking runs no frame actions. A playing `Once`
  animation sought to its end finishes, and runs its `Then`, on the next `Update`; a finished one sought
  back is left paused there. Seeking with no animation throws.

### Plays, frame actions, and finishing

`Play` starts a *play*, one run of an animation, and returns a `SpriteAnimationHandle` to it. A play has
one action slot per frame, set with `When(frameIndex, action)`, and one for when it finishes, set with
`Then(action)`:

```csharp
// Intro, then loop
hero.Play(jumpStart).Then(() => hero.Play(jumpLoop));

// A one-shot with a hit frame, then back to idle
hero.Play(attack, restart: true)
    .When(3, this.DealDamage)
    .Then(() => hero.Play(idle));

// Footsteps on every loop of the run
hero.Play(run).When(2, footsteps.Play).When(8, footsteps.Play);
```

The contract:

- **Interrupting empties the slots.** Another `Play` of a different animation, a restart, `Stop`, or
  `Resume` of a finished animation starts a new play; the previous play's actions are dropped without
  running. A handle from an earlier play does nothing, so a chain built on an interrupted play stops by
  itself, and nothing is ever unsubscribed.
- **Slots, not lists.** A second `When` for the same frame, or a second `Then`, on the same play
  replaces the first. Because `Play` with the current animation returns the current play's handle,
  `Play(run).When(2, ...)` called every frame doesn't pile up delegates. To avoid creating a delegate
  every frame at all, set the actions only when `handle.IsNewPlay` is true.
- **`Then` runs only for a `Once` play that finishes.** It never runs for `Loop` or `PingPong`, which
  never finish (that's allowed, not an error), nor for a play that's interrupted first. A `Then` or
  `When` set after the play has finished is ignored, so a state machine calling `Play(die).Then(...)`
  every frame doesn't run it every frame.
- **`When` runs on entering a frame, not on showing it.** It runs every time playback enters the frame:
  each loop, and twice per pass for a middle frame of a ping-pong. An update that jumps over frames on
  screen still runs their actions, in order, so a hit frame isn't lost to a slow frame. An update runs at
  most one pass's worth of actions, so a very long one (resuming after the game was in the background)
  can't run a loop's footsteps hundreds of times.
- **Ordering inside an update.** A play's first frame is entered when it starts, so its action runs on
  the next `Update`. Frame actions run before the time moves, so an action that plays another animation
  interrupts the current play where it is, and one that pauses the play keeps its frame counted as
  entered, so it doesn't run again on resume. A `Once` animation's last frame runs its action before
  `Then`.
- **Hand-offs land on the exact frame.** An animation played from `Then` starts with the time left over
  from the update that finished the previous play, so `Play(intro).Then(() => Play(loop))` neither loses
  nor repeats time. One update can chain through up to 64 plays this way; past that the rest of the time
  is dropped, so a chain of tiny animations can't spin.
- **Actions run synchronously** inside `Update`, on whatever thread calls it.

The per-frame slots are an array the length of the animation, allocated on the first `When` and cleared,
not reallocated, by later plays of an animation with as many frames. `Update` and drawing allocate
nothing; a `When` or `Then` allocates only its own delegate.

## Drawing

```csharp
public static void Draw(this SpriteBatch spriteBatch, SpriteAnimationPlayer player,
    Vector2 position, Color color, Angle rotation = default, Vector2 origin = default,
    Vector2? scale = null, SpriteFlip flip = SpriteFlip.None);
```

It draws the player's current frame through the trim-aware `Draw(TextureRegion, ...)`, and does nothing
when the player has no animation. The parameters mean what they mean for any atlas sprite:

- `origin` is in the frame's logical (untrimmed) pixels from its top-left corner, and defaults to the
  top-left corner. A character centered on its position passes
  `new Vector2(animation.Size.X, animation.Size.Y) / 2`; one anchored at its feet passes `new Vector2(animation.Size.X / 2f, animation.Size.Y)`.
- A negative `scale` component mirrors the frame about the origin.
- `flip` mirrors the image inside its logical rectangle, which doesn't move, so it flips in place
  whatever the origin, and a trimmed frame's pixels land mirrored where they belong.

For anything else, draw `player.Frame` directly.

## Recipes

**Character state machine**

```csharp
hero.Play(state switch
{
    HeroState.Run => run,
    HeroState.Jump => jump,
    _ => idle,
});
hero.Update(this.Time.Delta);
```

**One-shot effect that removes itself**

```csharp
var boom = new SpriteAnimationPlayer();
boom.Play(explosion).Then(() => effects.Remove(boom));   // a PlaybackMode.Once animation
```

**Two-part idle, repeating**

```csharp
void PlayIdle() => hero.Play(idle0).Then(() => hero.Play(idle1).Then(PlayIdle));
```

**Waiting for an animation from async code**

```csharp
var finished = new TaskCompletionSource();
hero.Play(victory).Then(() => finished.TrySetResult());
await finished.Task;   // never completes if the play is interrupted, like a chain
```

**Random start, so a crowd doesn't move in unison**

```csharp
var player = new SpriteAnimationPlayer(idle) { Time = idle.Duration * Random.Shared.NextSingle() };
```

## Why it's shaped this way

- **Timing is relative.** A frame rate plus a per-frame `Hold`, rather than a duration per frame, makes
  `FramesPerSecond` the one setting for an animation's tempo: changing 12 to 15 speeds up the whole
  animation and keeps its rhythm, with no durations to recalculate. It's also how animators think ("hold
  this pose for three frames").
- **`Play` is idempotent.** Game code usually decides the animation every frame from state
  (`Play(isMoving ? run : idle)`). Since playing the current animation does nothing, there's no separate
  "current animation" variable or "has it changed?" check. A deliberate restart, like a second attack,
  passes `restart: true`.
- **Actions belong to a play, not to the player.** C# events need unsubscribing and outlive the code that
  cares about them: a player kept somewhere long-lived can hold a view alive through a forgotten
  handler. Slots that empty when the play is interrupted can't outlive it.
- **Plain delegates, not tasks.** A `Task` per play costs an allocation, and an `await` resumes through
  the synchronization context, a frame late. `When` and `Then` run synchronously inside `Update`, so a
  hand-off lands on the exact frame. Async code can still wait through a `TaskCompletionSource`, as in
  the recipe above.
- **Time-based, not frame-counted.** `Update` takes elapsed time, so animation speed doesn't depend on
  the display's frame rate, and a long frame skips frames instead of slowing the animation.
- **An explicit `Update` on a plain object.** There's no hidden registration in a global animation
  system, so a player is easy to pause per object (don't call `Update`), to drive from a custom clock
  (slow motion, a paused game), and to unit test.
- **A player, not a sprite.** The toolkit has no scene graph or sprite object, so the player only tracks
  the frame and the game draws it where it likes. A game or library with a scene graph can wrap a player
  in its own sprite.
- **One `PlaybackMode` enum, not flags.** Separate looping, reversed, and ping-pong flags allow
  meaningless combinations; one enum doesn't, and reversing is a reversed animation.
- **Animations are objects, not names.** Looking animations up by string on a sheet finds typos only at
  run time; holding the `SpriteAnimation` doesn't, and needs no sheet type in between.

## How it compares

| Library | Clip (shared data) | Player (per instance) | Frames come from |
|---|---|---|---|
| This toolkit | `SpriteAnimation` | `SpriteAnimationPlayer` | atlas regions named `name_N` |
| MonoGame.Extended | `SpriteSheetAnimation` | `AnimatedSprite` with an `IAnimationController` | `Texture2DAtlas` regions, added frame by frame |
| Godot 4 | `SpriteFrames` animation | `AnimatedSprite2D` | editor |
| PixiJS | array of textures (spritesheet `animations`) | `AnimatedSprite` | spritesheet JSON |
| Phaser | Animation (by key) | the sprite's `anims` | atlas, `generateFrameNames` |
| Excalibur | `Animation` | the actor's graphics | `Animation.fromSpriteSheet` |
| libGDX | `Animation<TextureRegion>` | none: the caller keeps `stateTime` | atlas, `name_N` (`findRegions`) |
| Defold | flipbook animation | sprite component (`play_flipbook`) | atlas animation groups |
| Unreal Paper2D | `UPaperFlipbook` | `UPaperFlipbookComponent` | sprite assets |

The design follows common practice in several places: frame rate plus relative per-frame timing
(Godot's `SpriteFrames` durations are multipliers of the animation's speed); the `name_N` convention and
numeric order (libGDX's packer); a stateless time lookup (libGDX's `getKeyFrame`, Unreal's
`GetSpriteAtTime`); a speed multiplier that can't go negative; Loop, Once, and PingPong (closest to
Excalibur's strategies); `Play` not restarting the current clip (Godot, Unreal, Phaser's
`ignoreIfPlaying`); frame cues (PixiJS's `onFrameChange`); and no finished signal for looping clips.

It differs in keeping the player a separate object rather than a scene node or sprite, like libGDX's
split of shared `Animation` and caller-kept time, and in action slots per play where most others use
events or callbacks on the sprite (Phaser's `chain` is the closest).

### The names

The names are the ones 2D developers know and search for: "animation" (Godot, PixiJS, Phaser,
Excalibur, libGDX, MonoGame.Extended's `SpriteSheetAnimation`), with "sprite" to make it specific, since
a bare `Animation` suggests tweens or skeletal animation too. Alternatives:

| Clip | Player | Why not |
|---|---|---|
| `Flipbook` | `FlipbookPlayer` | Short and exact, but mainly an effects and Unreal/Defold term, less familiar to developers coming from Godot, PixiJS, Phaser, or MonoGame.Extended |
| `SpriteAnimation` | `AnimatedSprite` | The player name in Godot, PixiJS, and MonoGame.Extended, but there it's a drawable sprite with a position; this player only tracks the frame |
| `Animation` | `AnimationPlayer` | Too general; Godot's `AnimationPlayer` animates any property |

Code that also uses tweens calls them *tweens* (`Tween<T>` in the same namespace), so "animation" keeps
meaning frames.

## Limits

- Frame rates, holds, and pivots are set in code. There's no animation sidecar file or import from an
  animation tool.
- Animations are discrete: there's no blending or tweening between frames.
- There's no sprite that owns a player and updates itself; the game calls `Update` and draws.

## Tests

`test/DotGame.Toolkit.Tests/Graphics/SpriteAnimation*Tests.cs` cover frame lookup at boundaries for each
playback mode, with and without fractional holds; idempotent `Play`; `Then` running once, being
replaced, and never running after finishing, interruption, or for loops; `When` on every frame entered
(each loop, both legs of a ping-pong, frames skipped on screen, the first frame), at most one pass per
update, not after seeking, before `Then`, and interrupting from an action; slots released when
interrupted; hand-offs carrying leftover time, including several in one update; numeric ordering; and
the duplicate-number and mixed-size errors. They build regions without a texture
(`TestRegions`), so drawing isn't covered; the toolkit sample's Animation page shows it.

The atlas extensions are tested in `SpriteAnimationExtensionsTests.cs`.

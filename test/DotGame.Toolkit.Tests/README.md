# DotGame.Toolkit.Tests

Regression tests for the toolkit's own logic. They run without a graphics device, audio device or
content on disk, and take a couple of seconds.

## Running

From this folder:

```bash
dotnet test --project test/DotGame.Toolkit.Tests -c Debug
```

```bash
dotnet test --project test/DotGame.Toolkit.Tests -c Release
```

Run both before relying on a change: Debug-only code in the toolkit (`[Conditional("DEBUG")]`
reporting) and the JIT's optimizations differ between them, and the allocation tests see the
difference.

One area at a time:

```bash
dotnet test --project test/DotGame.Toolkit.Tests --filter-class "*NinePatchTests"
```

## What's covered

| Tests | What they guard |
|---|---|
| `Graphics/NinePatchTests` | Slices, stretching, shrinking, seams, trim and outset, checked against where the untrimmed pixels would land |
| `Graphics/TextureRegionTests` | Rectangles over textures and over other regions, trim, `WithOutset`, validation |
| `Graphics/SpriteBatchExtensionsTests` | Trim maths: frames, origins, mirroring, clipped sub-rectangles, a player's frame |
| `Graphics/TextureAtlasTests` | The atlas manifest's format, malformed manifests, the loader, and the atlas's lifetime |
| `Graphics/SpriteAnimation*Tests` | Playback and timing. `SpriteAnimationEdgeTests` checks every playback and hold against the frames it enters, plus long updates and actions that change the play |
| `Audio/SoundBankTests` | The bank manifest's format, the loader, segments |
| `Audio/SoundPoolTests` | Which voice plays, stealing, volume, fades, and use after `Dispose` |
| `Fonts/SpriteFontTests` | Measuring, kerning, default characters, metrics |
| `Fonts/SpriteFontDrawTests` | What drawing queues: glyph placement, pages, layers, attributes, `StringBuilder` chunks, surrogates, and the edges of the control and ignorable character ranges |
| `Fonts/SpriteFontFileTests` | The `.spritefont` format, written with the pipeline's own `PbonWriter`, truncated and malformed files, the loader, PBON |
| `Fonts/TextEffectsTests` | The settings each text layer carries to the shader |
| `Particles/*Tests` | Motion, emission, capacity, draw states, renderers. `ParticleLifecycleTests` checks the particle ring against a list model, and motion against the exact formulas |
| `AllocationTests` | Text, sprites, nine-patches, animation, sound pools and particle drawing allocate nothing per frame |
| `SupportTypesTests` | Thicknesses, code point decoding, reading content streams |
| `EasingTests` | The easing functions |

The sibling suites cover the rest: `test/DotGame.Framework.Tests` for the framework and its
platforms. Behavior the toolkit only passes through (such as what a platform's voice does when
stopped) is not tested here.

## How the tests see the platform

`TestPlatform.cs` implements the framework's internal platform strategies, which the framework
exposes to this project through `InternalsVisibleTo`. The tests use the real `Texture`,
`SpriteBatch`, `SoundVoice` and `ContentService` over them:

- `TestTextures.Create` makes a texture of a size.
- `RecordingBatch` is a begun `SpriteBatch` that keeps each `DrawCall`, with where its source
  rectangle lands on the screen.
- `FakeAudio` makes voices that play until a test calls `Finish`, and logs what's done to them.
- `FakeContent` serves files from memory and fake textures and sounds, and records what's loaded.

`Fonts/TestFonts` builds fonts, `Graphics/TestRegions` builds regions and atlases, and
`Particles/ParticleTestHelpers` builds seeded particle systems, so a test sees the same particles
every run.

## Conventions

- Tests assert correct behavior, from the docs and the design, not whatever the code does today.
- No sleeps, wall clocks or unseeded randomness: time is what a test passes to `Update`.
- Allocation tests read `GC.GetAllocatedBytesForCurrentThread`, so they're safe beside other
  tests. They run their body twice first, for what only happens once. The project turns background
  GC off: one can retire a thread's allocation buffer and count what's left of it as allocated, so
  a test that allocated nothing would fail with up to 8 KB.
- Theory data can't carry an unpaired surrogate through xUnit's serialization; loop in a `Fact`.
- This project is outside DotGame's build policy (it isn't game code), so LINQ is fine here.
  Reflection isn't needed: use the fakes.

## Known issues

A test for a bug that isn't fixed yet is written as it should pass, and skipped:

```csharp
[Fact(Skip = "Known issue T2: ...")]
```

`T` numbers are findings in the toolkit code review; `TK` numbers were found by this suite. When
fixing one, delete its `Skip` and the test guards the fix. To list them, search this folder for
`Skip = "Known issue`.

| Issue | What's wrong | Where |
|---|---|---|
| T1 | A `SoundPool` plays on after its sound is disposed, reading freed memory on desktop | `Audio/SoundPool.cs`, `Play` |
| T2 | A surrogate pair split across `StringBuilder` chunks draws as two default characters; `GetChunks` allocates past 8 chunks | `Fonts/SpriteFont.cs`, `MeasureString`, `DrawString` |
| T3 | `WhenEmptyAsync`'s continuations run inside `Update` | `Particles/ParticleSystem.cs`, `WhenEmptyAsync` |
| T9 | Untrimmed caps that fill the source leave a hole in a larger patch | `Graphics/NinePatch.cs`, constructor |
| T14i | A region of a third-party `ITextureSource` ignores the source's offset | `Graphics/TextureRegion.cs`, `Resolve` |
| TK1 | A trim that reaches past the caps stretches the pixels over the wrong part of the bounds, or draws nothing at small sizes | `Graphics/NinePatch.cs`, `Draw` |
| TK2 | Shrunk caps that both round up draw a pixel outside the bounds | `Graphics/NinePatch.cs`, `Draw` |
| TK3 | DEL and the C1 controls draw the default character, though the docs say control characters draw nothing | `Fonts/SpriteFont.cs`, `TryGetGlyph` |
| TK4 | A `When` action that pauses runs again on resume, so the play can't get past that frame | `Graphics/SpriteAnimationPlayer.cs`, `Update` |

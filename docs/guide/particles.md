# Particles and easing

## Particle effects

Sparkles, confetti, smoke, snow and trails. A [`ParticleEffect`](../api/ParticleEffect.md) is
immutable data, a [`ParticleSystem`](../api/ParticleSystem.md) runs it, and your game drives it
and draws it with its own sprite batch:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();
    var atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/sprites");

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
    this.trail = this.particles.CreateEmitter(this.position);
}

private void CollectCoin()
{
    this.particles.Burst(this.position, count: 20);
}

protected override void OnUpdate()
{
    this.trail.Position = this.position;
    this.particles.Update(this.Time.Delta);
}

protected override void OnDraw()
{
    var spriteBatch = this.Graphics.SharedSpriteBatch;

    using var batch = spriteBatch.Begin();
    this.particles.Draw(spriteBatch);
}
```

A burst emits once, and an emitter keeps emitting from wherever you move it.

- Motion under gravity and drag is exact at any frame rate, and a moving emitter spreads its particles
  along its path.
- Renderers draw textures (`Sprite`, `AnyOf`), sprite animations (`Animation`), stretched sprites
  (`Stretched`), or several at once (`Composite`). A game can add its own by deriving from
  [`ParticleRenderer`](../api/ParticleRenderer.md), and add custom simulation with an
  [`IParticleBehavior`](../api/IParticleBehavior.md).
- Particles live in whatever space the system is drawn in. Draw inside `spriteBatch.Begin(transform)`
  for particles that move with something.

The design and its reasoning are in [particles.md](../architecture/particles.md).

## Easing functions

An [`Easing`](../api/Easing.md), in `DotGame.Toolkit.Animation`, shapes how something moves over time. `Ease`
takes how far through the movement you are, from 0 to 1, and returns how far along it should be:

```csharp
protected override void OnUpdate()
{
    this.elapsed += this.Time.DeltaSeconds;

    float t = MathF.Min(this.elapsed / 0.5f, 1);
    this.position = Vector2.Lerp(this.start, this.end, Easing.BackOut.Evaluate(t));
}
```

There's a ready-made instance of each kind, in `In`, `Out` and `InOut` versions: `Quad`, `Cubic`, `Quart`,
`Quint`, `Sine`, `Expo`, `Circ`, `Back`, `Elastic` and `Bounce`, as well as `Linear`. For your own settings,
use a method: [`Easing.Elastic`](../api/Easing.md#elastic), `Back` and `Exponential` tune those curves,
`CubicBezier` follows CSS's `cubic-bezier`, `Steps` jumps, `Spring` bounces like a spring, and `Custom` is your own.
`Reversed` plays a curve backward. Particle curves take the
same functions.

## Moving a value over time

[`Tween`](../api/Tween.md) moves a value from one place to another along an easing. You drive it
with the time that's passed, so it follows slow motion and pauses, and you read where it's got to:

```csharp
private readonly Tween<Vector2> slide = Tween.Create(
    Vector2.Zero,
    new Vector2(400, 0),
    TimeSpan.FromSeconds(0.5),
    Easing.BackOut);

protected override void OnUpdate()
{
    this.slide.Update(this.Time.Delta);
    this.position = this.slide.Value;
}
```

`Tween.Create` takes a number, a position, a color, an angle or, with a function to mix them, a value of
your own. A delay and a [`PlaybackMode`](../api/PlaybackMode.md) are optional. `Restart` uses it again
without allocating, `Finish` jumps to the end, and `await WhenFinishedAsync()` waits for it, which is how to
run tweens one after another.

## See also

- [`ParticleEffect`](../api/ParticleEffect.md), [`ParticleSystem`](../api/ParticleSystem.md),
  [`ParticleCurve`](../api/ParticleCurve.md), [`EmitShape`](../api/EmitShape.md)

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using System.Text;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Audio;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.Graphics;
using DotGame.Toolkit.Particles;
using DotGame.Toolkit.Tests.Fonts;
using Xunit;

namespace DotGame.Toolkit.Tests;

/// <summary>
/// The toolkit's per-frame paths allocate nothing once they're warmed up. See also
/// <c>ParticleSystemTests.SteadyState_AllocatesNothing</c>.
/// </summary>
/// <remarks>
/// The counter is per thread, so tests running in parallel don't disturb each other, and this
/// project turns background GC off, which can inflate it (see the project file). Each test's body
/// runs twice first, for what happens once: JIT-time type loading, rented buffers, and the pools
/// and tables a path fills on first use.
/// </remarks>
public sealed class AllocationTests
{
    private static long Allocated(
        Action body)
    {
        body();
        body();

        long before = GC.GetAllocatedBytesForCurrentThread();
        body();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Text

    private static SpriteFont TextFont()
    {
        return new TestFonts()
            .DistanceRange(16)
            .Glyphs("Helo wrd!")
            .Glyph(0x1F600)
            .Glyph('?')
            .Kerning('H', 'e', -1)
            .Kerning('o', ' ', -1)
            .Build();
    }

    private const string Text = "Hello world!\nHello \U0001F600 é️";

    private static readonly TextEffects Effects = new()
    {
        Shadow = new TextShadow(Color.Black, new Vector2(1, 1)),
        Glow = new TextGlow(Color.Yellow, Width: 1, Softness: 1),
        Outline = new TextOutline(Color.Blue, Width: 1),
    };

    [Fact]
    public void DrawString_Plain_AllocatesNothing()
    {
        var font = TextFont();
        var batch = new RecordingBatch();

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.DrawString(font, Text, new Vector2(1, 2), Color.White);
        });

        Assert.Equal(0, allocated);
        Assert.NotEmpty(batch.Calls);
    }

    [Fact]
    public void DrawString_WithEveryEffect_AllocatesNothing()
    {
        var font = TextFont();
        var batch = new RecordingBatch();

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.DrawString(font, Text, new Vector2(1, 2), Color.White, opacity: 0.5f, rotation: Angle.FromDegrees(10), origin: Vector2.One, scale: Vector2.One, tracking: 1, effects: Effects);
        });

        Assert.Equal(0, allocated);
        Assert.NotEmpty(batch.Calls);
    }

    [Fact]
    public void DrawString_WithStructAttributes_AllocatesNothing()
    {
        var font = TextFont();
        var batch = new RecordingBatch();

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.DrawString(font, Text, new RedOddGlyphs(), new CharacterCursor(3, 3), Vector2.Zero, Color.White);
        });

        Assert.Equal(0, allocated);
        Assert.Contains(batch.Calls, c => c.Color == Color.Red);
    }

    private struct RedOddGlyphs : ICharacterAttributes
    {
        private int _glyph;

        public void MoveTo(
            in CharacterCursor cursor)
        {
            this._glyph = cursor.GlyphIndex;
        }

        public readonly Color? Color => ((this._glyph & 1) != 0) ? DotGame.Framework.Color.Red : null;

        // (Spelled out, as a struct's must be: the interface's own would box it)
        public readonly Vector2 Translation => Vector2.Zero;

        public readonly Angle Rotation => default;
    }

    [Fact]
    public void DrawString_StringBuilder_InAFewChunks_AllocatesNothing()
    {
        var font = TextFont();
        var batch = new RecordingBatch();
        var builder = new StringBuilder(4).Append("Hello").Append(" world!").Append(" Hello");

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.DrawString(font, builder, Vector2.Zero, Color.White, effects: Effects);
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void DrawString_StringBuilder_InManyChunks_AllocatesNothing()
    {
        var font = TextFont();
        var batch = new RecordingBatch();
        var builder = new StringBuilder(1);
        for (int i = 0; i < 256; i++)
        {
            builder.Append("Hello ");
        }

        int chunks = 0;
        foreach (var chunk in builder.GetChunks())
        {
            chunks += 1;
        }

        Assert.True(chunks > 8, $"The builder has {chunks} chunks.");

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.DrawString(font, builder, Vector2.Zero, Color.White);
        });

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void MeasureString_AllocatesNothing()
    {
        var font = TextFont();
        var builder = new StringBuilder(4).Append("Hello").Append(" world!");
        float width = 0;

        long allocated = Allocated(() =>
        {
            width = font.MeasureString(Text).X
                + font.MeasureString(Text.AsSpan(), tracking: 1).X
                + font.MeasureString(builder).X
                + font.MeasureString('H').X;
        });

        Assert.Equal(0, allocated);
        Assert.True(width > 0);
    }

    // Sprites

    private static readonly Texture Page = TestTextures.Create(1024, 1024);

    private static readonly TextureRegion Trimmed = new(
        Page.AsSource(),
        new RectangleInt(100, 100, 100, 100),
        new RectangleInt(110, 110, 80, 80));

    [Fact]
    public void Draw_ATrimmedRegion_AllocatesNothing()
    {
        var batch = new RecordingBatch();

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            batch.Batch.Draw(Trimmed, Vector2.One, Color.White);
            batch.Batch.Draw(Trimmed, Vector2.One, Color.White, Angle.FromDegrees(1), Vector2.One, new Vector2(-1, 1));
            batch.Batch.Draw(Trimmed, Vector2.One, Color.White, sourceRectangle: new Rectangle(0, 0, 50, 50));
        });

        Assert.Equal(0, allocated);
        Assert.Equal(3, batch.Calls.Count);
    }

    [Fact]
    public void NinePatch_Draw_AllocatesNothing()
    {
        var patch = new NinePatch(Trimmed, new ThicknessInt(20), new ThicknessInt(5));
        var batch = new RecordingBatch();

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            patch.Draw(batch.Batch, new RectangleInt(0, 0, 300, 200));
            patch.Draw(batch.Batch, new RectangleInt(0, 0, 10, 10), Color.Red);
        });

        Assert.Equal(0, allocated);
        Assert.Equal(18, batch.Calls.Count);
    }

    // Animation

    [Fact]
    public void Animation_UpdateAndDraw_AllocateNothing()
    {
        var frames = new TextureRegion[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = new TextureRegion(Page.AsSource(), new RectangleInt(i * 32, 0, 32, 32));
        }

        var run = new SpriteAnimation(frames, 10);
        var idle = new SpriteAnimation(frames, 5, PlaybackMode.PingPong);
        var player = new SpriteAnimationPlayer();
        var batch = new RecordingBatch();
        var frame = TimeSpan.FromSeconds(1 / 60.0);
        int steps = 0;

        // Kept delegates, as the docs say a per-frame Play must
        Action step = () => steps += 1;
        Action next = () => { };

        long allocated = Allocated(() =>
        {
            for (int i = 0; i < 120; i++)
            {
                batch.Calls.Clear();
                player.Play((i / 60) % 2 == 0 ? run : idle).When(1, step).Then(next);
                player.Update(frame);
                batch.Batch.Draw(player, Vector2.Zero, Color.White);
            }
        });

        Assert.Equal(0, allocated);
        Assert.True(steps > 0);
    }

    // Sound

    [Fact]
    public void SoundPool_Replays_AllocateNothing()
    {
        var audio = new FakeAudio();
        var pool = new SoundPool(audio.Service, FakeAudio.CreateSound(), voiceLimit: 3);
        var fade = TimeSpan.FromSeconds(0.5);

        // Overlapping, stealing, quiet, and fading out, on every voice there will be
        long allocated = Allocated(() =>
        {
            for (int i = 0; i < 8; i++)
            {
                pool.Play();
                pool.Play(0.5f);
            }

            pool.StopFadedOut(pool.FadeOutVoices(fade));
            pool.StopAll();
            audio.Log.Clear();
        });

        Assert.Equal(0, allocated);
        Assert.Equal(3, audio.Voices.Count);
    }

    // Particles

    [Fact]
    public void Particles_Draw_ThroughEveryRenderer_AllocatesNothing()
    {
        var sprite = ParticleRenderer.Sprite(Trimmed);
        var anyOf = ParticleRenderer.AnyOf(Trimmed, Trimmed);
        var stretched = ParticleRenderer.Stretched(Trimmed, lengthPerSpeed: 0.1f);
        var animation = ParticleRenderer.Animation(new SpriteAnimation([Trimmed, Trimmed], 10));
        var composite = ParticleRenderer.Composite(sprite, stretched);

        var effects = new ParticleRenderer[] { sprite, anyOf, stretched, animation, composite };
        var system = new ParticleSystem(new ParticleRandom(1), Array.ConvertAll(effects, ToEffect));
        system.Burst(Vector2.Zero, 32);
        var batch = new RecordingBatch();
        var frame = TimeSpan.FromSeconds(1 / 60.0);

        long allocated = Allocated(() =>
        {
            batch.Calls.Clear();
            system.Update(frame);
            system.Draw(batch.Batch, Color.White * 0.5f);
        });

        Assert.Equal(0, allocated);
        Assert.Equal(32 * 6, batch.Calls.Count);
    }

    private static ParticleEffect ToEffect(
        ParticleRenderer renderer)
    {
        return new ParticleEffect
        {
            Renderer = renderer,
            LifetimeSeconds = 100,
            Speed = 10,
            SpinDegrees = 90,
            Sway = new ParticleSway(1, 1),
            Flip = new ParticleFlip(1),
            ScaleOverLife = ParticleCurve.Between(1, 0.5f),
            OpacityOverLife = ParticleCurve.Between(1, 0.5f),
            EndColor = ParticleColor.Constant(Color.Red),
        };
    }

    [Fact]
    public void Particles_Emit_AllocatesNothing_OnceTheBuffersAreRented()
    {
        var system = new ParticleSystem(
            new ParticleRandom(1),
            new ParticleEffect { Renderer = ParticleRenderer.Sprite(Trimmed), LifetimeSeconds = 100, Capacity = 16, Overflow = ParticleOverflow.ReplaceOldest, Shape = EmitShape.Circle(5) });
        system.Burst(Vector2.Zero, 1);

        long allocated = Allocated(() =>
        {
            system.Burst(Vector2.Zero, 8);
            system.Burst(new Rectangle(0, 0, 10, 10), 8);
            system.Emit(new ParticleSpawn { Position = Vector2.One });
        });

        Assert.Equal(0, allocated);
    }
}

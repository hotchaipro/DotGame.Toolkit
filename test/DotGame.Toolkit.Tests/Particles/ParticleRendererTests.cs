// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Graphics;
using DotGame.Toolkit.Particles;
using Xunit;
using static DotGame.Toolkit.Tests.Graphics.TestRegions;
using static DotGame.Toolkit.Tests.Particles.ParticleTestHelpers;

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// The built-in renderers' choices: which texture or frame, and the origin, scale and rotation
/// it draws with.
/// </summary>
public sealed class ParticleRendererTests
{
    [Fact]
    public void AnyOf_PicksByVariant()
    {
        var textures = new ITextureSource[] { new TestSource(8, 8), new TestSource(8, 8), new TestSource(8, 8) };
        var renderer = ParticleRenderer.AnyOf(textures);

        Assert.Same(textures[2], renderer.Resolve(State(variant: 2), out _, out _));
        Assert.Same(textures[1], renderer.Resolve(State(variant: 4), out _, out _));
        Assert.Same(textures[0], renderer.Resolve(State(variant: 3), out _, out _));
    }

    [Fact]
    public void Sprite_TrimmedRegion_PivotsOnItsLogicalSize()
    {
        // A 64 x 32 region trimmed to a 40 x 20 frame: the pivot is the caller's, in logical
        // pixels, and the trim-aware draw moves it into the frame
        var region = new TestSource(new RectangleInt(0, 0, 64, 32), new RectangleInt(10, 4, 40, 20));
        var renderer = ParticleRenderer.Sprite(region) with { Pivot = new Vector2(0.25f, 1f) };

        renderer.Resolve(State(), out var origin, out var scale);

        Assert.Equal(new Vector2(16, 32), origin);
        Assert.Equal(Vector2.One, scale);
    }

    [Fact]
    public void Sprite_Size_ScalesTheLogicalSize()
    {
        var region = new TestSource(new RectangleInt(0, 0, 64, 32), new RectangleInt(10, 4, 40, 20));
        var renderer = ParticleRenderer.Sprite(region) with { Size = new Vector2(16, 16) };

        renderer.Resolve(State(scale: new Vector2(2, 3)), out var origin, out var scale);

        Assert.Equal(new Vector2(32, 16), origin);
        Assert.Equal(new Vector2(0.5f, 1.5f), scale);
    }

    [Fact]
    public void Animation_OverLife_PlaysOncePerLife()
    {
        var frames = Regions(4);
        var renderer = ParticleRenderer.Animation(new SpriteAnimation(frames, 10));

        Assert.Same(frames[0], renderer.Resolve(State(normalizedAge: 0), out _, out _));
        Assert.Same(frames[2], renderer.Resolve(State(normalizedAge: 0.5f), out _, out _));
        Assert.Same(frames[3], renderer.Resolve(State(normalizedAge: 1), out _, out _));
    }

    [Fact]
    public void Animation_Loop_PlaysAtItsOwnFrameRateFromTheAge()
    {
        var frames = Regions(4);
        var renderer = ParticleRenderer.Animation(new SpriteAnimation(frames, 10), ParticleAnimationTiming.Loop);

        Assert.Same(frames[1], renderer.Resolve(State(age: 0.15f), out _, out _));
        Assert.Same(frames[0], renderer.Resolve(State(age: 0.45f), out _, out _));
    }

    [Fact]
    public void Animation_PivotsOnTheAnimationsSize()
    {
        var renderer = ParticleRenderer.Animation(new SpriteAnimation(Regions(2), 10));

        renderer.Resolve(State(), out var origin, out _);

        Assert.Equal(new Vector2(16, 16), origin);
    }

    [Fact]
    public void Stretched_PointsAlongTheVelocity_AndLengthensWithSpeed()
    {
        var renderer = ParticleRenderer.Stretched(new TestSource(10, 4), lengthPerSpeed: 0.1f);

        renderer.Resolve(State(velocity: new Vector2(0, 200)), out var rotation, out var origin, out var scale);

        Assert.Equal(MathF.PI / 2, rotation.Radians, 0.0001f);
        Assert.Equal(new Vector2(5, 2), origin);
        Assert.Equal(3, scale.X, 0.0001f);      // (10 + 0.1 * 200) / 10
        Assert.Equal(1, scale.Y, 0.0001f);
    }

    [Fact]
    public void Stretched_LengthIsCapped()
    {
        var renderer = ParticleRenderer.Stretched(new TestSource(10, 4), lengthPerSpeed: 1) with { MaxLength = 25 };

        renderer.Resolve(State(velocity: new Vector2(1000, 0)), out _, out _, out var scale);

        Assert.Equal(2.5f, scale.X, 0.0001f);
    }

    [Fact]
    public void Stretched_StillParticle_KeepsItsRotationAndScale()
    {
        var renderer = ParticleRenderer.Stretched(new TestSource(10, 4), lengthPerSpeed: 1);

        renderer.Resolve(State(rotationRadians: 1, scale: new Vector2(2, 2)), out var rotation, out _, out var scale);

        Assert.Equal(1, rotation.Radians, 0.0001f);
        Assert.Equal(new Vector2(2, 2), scale);
    }

    [Fact]
    public void Composite_PassesTheSameParticlesToEachRenderer_InOrder()
    {
        var first = new RecordingRenderer();
        var second = new RecordingRenderer();
        var composite = ParticleRenderer.Composite(first, second);
        var system = NewSystem(Effect() with { Renderer = composite });
        system.Burst(Vector2.Zero, 3);

        system.DrawCore(null!, Color.White);

        Assert.Single(first.Calls);
        Assert.Single(second.Calls);
        Assert.Equal(3, first.Calls[0].Length);
        Assert.Equal(first.Calls[0].Select(p => p.Variant), second.Calls[0].Select(p => p.Variant));
    }

    [Fact]
    public void Draw_SkipsEffectsWithNoParticles()
    {
        var renderer = new RecordingRenderer();
        var system = NewSystem(Effect() with { Renderer = renderer });

        system.DrawCore(null!, Color.White);

        Assert.Empty(renderer.Calls);
    }
}

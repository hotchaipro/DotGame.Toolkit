// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Particles;
using Xunit;
using static DotGame.Toolkit.Tests.Particles.ParticleTestHelpers;

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// Curves, ranges, colors, and the argument checks on the values and renderers.
/// </summary>
public sealed class ParticleValueTests
{
    [Fact]
    public void Curve_InterpolatesLinearly_BetweenKeys()
    {
        var curve = ParticleCurve.FromKeys(new(0, 1), new(0.5f, 3), new(1, 0));

        Assert.Equal(1, curve.Evaluate(-1));
        Assert.Equal(2, curve.Evaluate(0.25f), 0.0001f);
        Assert.Equal(1.5f, curve.Evaluate(0.75f), 0.0001f);
        Assert.Equal(0, curve.Evaluate(2));
        Assert.False(curve.IsConstant);
    }

    [Fact]
    public void Curve_FollowsTheKeysEasing()
    {
        var curve = ParticleCurve.Between(0, 1, Easing.CubicIn);

        Assert.Equal(0.125f, curve.Evaluate(0.5f), 0.0001f);
    }

    [Fact]
    public void Curve_Constant_IsConstant()
    {
        Assert.True(ParticleCurve.Constant(3).IsConstant);
        Assert.Equal(3, ParticleCurve.Constant(3).Evaluate(0.7f));
        Assert.Equal(1, ParticleCurve.One.Evaluate(0.3f));
    }

    [Fact]
    public void Curve_RejectsNoKeys_AndKeysOutOfOrder()
    {
        Assert.Throws<ArgumentException>(() => ParticleCurve.FromKeys());
        Assert.Throws<ArgumentException>(() => ParticleCurve.FromKeys(new(0.5f, 1), new(0.2f, 0)));
    }

    [Fact]
    public void Range_ConvertsFromAValueAndATuple()
    {
        FloatRange single = 5;
        FloatRange pair = (1, 3);

        Assert.True(single.IsConstant);
        Assert.Equal(new FloatRange(1, 3), pair);
        Assert.Equal(new FloatRange(8, 12), FloatRange.Around(10, 2));
        Assert.Equal(2, pair.Lerp(0.5f));
    }

    [Fact]
    public void Range_SamplesWithinItself()
    {
        var random = new ParticleRandom(7);
        var range = new FloatRange(-2, 4);

        for (int i = 0; i < 1000; i++)
        {
            Assert.InRange(range.Sample(random), -2, 4);
        }
    }

    [Fact]
    public void Palette_RejectsNoColors_AndSamplesItsColors()
    {
        Assert.Throws<ArgumentException>(() => ParticleColor.Palette());

        var palette = ParticleColor.Palette(Color.Red, Color.Blue);
        var random = new ParticleRandom(3);
        for (int i = 0; i < 100; i++)
        {
            var color = palette.Sample(random);
            Assert.True((color == Color.Red) || (color == Color.Blue));
        }
    }

    [Fact]
    public void Renderers_RejectMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ParticleRenderer.Sprite(null!));
        Assert.Throws<ArgumentException>(() => ParticleRenderer.AnyOf());
        Assert.Throws<ArgumentNullException>(() => ParticleRenderer.AnyOf(new TestSource(1, 1), null!));
        Assert.Throws<ArgumentNullException>(() => ParticleRenderer.Animation(null!));
        Assert.Throws<ArgumentException>(() => ParticleRenderer.Composite());
        Assert.Throws<ArgumentOutOfRangeException>(() => ParticleRenderer.Stretched(new TestSource(1, 1), -1));
    }

    [Fact]
    public void Renderers_AreRecords_SoWithMakesVariants()
    {
        var white = new TestSource(4, 4);
        var renderer = ParticleRenderer.Sprite(white);
        var confetti = renderer with { Size = new System.Numerics.Vector2(15, 25) };

        Assert.Null(renderer.Size);
        Assert.Equal(new System.Numerics.Vector2(15, 25), confetti.Size);
        Assert.Same(white, confetti.Textures[0]);
    }

    [Fact]
    public void System_RejectsNoEffects_AndAnEffectWithNoCapacity()
    {
        Assert.Throws<ArgumentException>(() => new ParticleSystem());
        Assert.Throws<ArgumentException>(() => new ParticleSystem(Effect() with { Capacity = 0 }));
    }

    [Fact]
    public void Emit_RejectsALayerThatIsntAnEffect()
    {
        var system = NewSystem(Effect());

        Assert.Throws<ArgumentOutOfRangeException>(() => system.Emit(new ParticleSpawn { Position = default, Layer = 1 }));
    }
}

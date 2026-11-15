// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Particles;
using Xunit;
using static DotGame.Toolkit.Tests.Particles.ParticleTestHelpers;

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// The states a renderer is handed: everything the system resolves for each particle.
/// </summary>
public sealed class ParticleDrawStateTests
{
    [Fact]
    public void Sway_StartsFromNoOffset_ThenMovesAlongX()
    {
        var system = NewSystem(Effect() with { Sway = new ParticleSway(Amplitude: 20, Frequency: 1) });
        system.Emit(new ParticleSpawn { Position = new Vector2(50, 50) });

        Assert.Equal(50, Drawn(system)[0].Position.X, 0.001f);

        system.Update(TimeSpan.FromSeconds(0.2));
        var swayed = Drawn(system)[0].Position;
        Assert.NotEqual(50, swayed.X, 0.01f);
        Assert.InRange(swayed.X, 10, 90);
        Assert.Equal(50, swayed.Y, 0.001f);
    }

    [Fact]
    public void Scale_FollowsScaleOverLife()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 1, Scale = 2, ScaleOverLife = ParticleCurve.Between(1, 0) });
        system.Burst(Vector2.Zero, 1);

        system.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(new Vector2(1, 1), Drawn(system)[0].Scale);
    }

    [Fact]
    public void Flip_NarrowsTheWidth_ToNoLessThanItsMinimum()
    {
        var system = NewSystem(Effect() with { Flip = new ParticleFlip(Frequency: 3, MinWidth: 0.25f) });
        system.Burst(Vector2.Zero, 20);

        for (int frame = 0; frame < 30; frame++)
        {
            system.Update(TimeSpan.FromSeconds(1 / 30.0));
            Assert.All(Drawn(system), p =>
            {
                Assert.InRange(p.Scale.X, 0.25f, 1f);
                Assert.Equal(1, p.Scale.Y, 0.0001f);
            });
        }
    }

    [Fact]
    public void Rotation_AddsTheSpinOverTime()
    {
        var system = NewSystem(Effect() with { RotationDegrees = 90, SpinDegrees = 180 });
        system.Burst(Vector2.Zero, 1);

        system.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(MathF.PI, Drawn(system)[0].Rotation.Radians, 0.001f);
    }

    [Fact]
    public void AlignToVelocity_PointsTheWayItMoves()
    {
        var system = NewSystem(Effect() with { Speed = 100, DirectionDegrees = 90, AlignToVelocity = true });
        system.Burst(Vector2.Zero, 1);

        Assert.Equal(MathF.PI / 2, Drawn(system)[0].Rotation.Radians, 0.001f);
    }

    [Fact]
    public void AlignToVelocity_TurnsByTheRotationAndSpin()
    {
        var system = NewSystem(Effect() with { Speed = 100, DirectionDegrees = 90, RotationDegrees = 45, SpinDegrees = 90, AlignToVelocity = true });
        system.Burst(Vector2.Zero, 1);
        system.Update(TimeSpan.FromSeconds(0.5));

        // 90 to point the way it moves, 45 more, and half a second of spin
        Assert.Equal(MathF.PI * 1.0f, Drawn(system)[0].Rotation.Radians, 0.001f);
    }

    [Fact]
    public void Color_FadesToTheEndColor_TimesOpacityAndTint()
    {
        var system = NewSystem(Effect() with
        {
            LifetimeSeconds = 1,
            Color = Color.FromPremultiplied(1f, 0f, 0f, 1f),
            EndColor = Color.FromPremultiplied(0f, 0f, 1f, 1f),
            Opacity = 0.5f,
        });
        system.Burst(Vector2.Zero, 1);
        system.Update(TimeSpan.FromSeconds(0.5));

        var color = system.GetDrawStates(0, Color.White * 0.5f)[0].Color;

        // Halfway between red and blue, at half opacity, tinted to half
        Assert.Equal(0.125f, color.R, 0.001f);
        Assert.Equal(0f, color.G, 0.001f);
        Assert.Equal(0.125f, color.B, 0.001f);
        Assert.Equal(0.25f, color.A, 0.001f);
    }

    [Fact]
    public void Additive_HasNoAlpha()
    {
        var system = NewSystem(Effect() with { Blend = ParticleBlend.Additive });
        system.Burst(Vector2.Zero, 3);

        Assert.All(Drawn(system), p =>
        {
            Assert.Equal(0, p.Color.A);
            Assert.Equal(1, p.Color.R, 0.001f);
        });
    }

    [Fact]
    public void Variant_IsFixedForEachParticlesLife()
    {
        var system = NewSystem(Effect());
        system.Burst(Vector2.Zero, 10);
        var before = Drawn(system).Select(p => p.Variant).ToArray();

        Run(system, 1);

        Assert.Equal(before, Drawn(system).Select(p => p.Variant));
        Assert.True(before.Distinct().Count() > 1);
    }

    [Fact]
    public void Variant_FromOverrides_IsTheSameForTheBurst()
    {
        var system = NewSystem(Effect());

        system.Burst(Vector2.Zero, 5, new ParticleOverrides { Variant = 2 });

        Assert.All(Drawn(system), p => Assert.Equal(2u, p.Variant));
    }

    [Fact]
    public void FullyTransparentParticles_AreLeftOut()
    {
        var system = NewSystem(Effect());
        system.Emit(new ParticleSpawn { Position = Vector2.Zero, Opacity = 0 });
        system.Emit(new ParticleSpawn { Position = new Vector2(1, 0) });

        Assert.Equal([1f], Drawn(system).Select(p => p.Position.X));
        Assert.Empty(system.GetDrawStates(0, Color.Transparent).ToArray());
    }

    [Fact]
    public void Custom_PassesThroughFromTheBehavior()
    {
        var system = NewSystem(Effect() with { Behavior = new HeatBehavior() });
        system.Burst(Vector2.Zero, 1);

        system.Update(TimeSpan.FromSeconds(0.25));

        Assert.Equal(new Vector4(0.25f, 7, 0, 0), Drawn(system)[0].Custom);
    }

    [Fact]
    public void Behavior_ReturningFalse_RemovesTheParticle()
    {
        var system = NewSystem(Effect() with { Behavior = new HeatBehavior { RemoveAfter = 0.5f } });
        system.Burst(Vector2.Zero, 3);

        Run(system, 0.6);

        Assert.Equal(0, system.Count);
    }

    [Fact]
    public void AgeAndNormalizedAge_AreReported()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 2 });
        system.Burst(Vector2.Zero, 1);

        system.Update(TimeSpan.FromSeconds(0.5));

        var drawn = Drawn(system)[0];
        Assert.Equal(0.5f, drawn.AgeSeconds, 0.0001f);
        Assert.Equal(0.25f, drawn.NormalizedAge, 0.0001f);
    }

    private sealed class HeatBehavior : IParticleBehavior
    {
        public float RemoveAfter { get; init; } = float.MaxValue;

        public void Start(
            ref Particle particle)
        {
            particle.Custom.Y = 7;
        }

        public bool Update(
            ref Particle particle,
            float elapsedSeconds)
        {
            particle.Custom.X += elapsedSeconds;
            return particle.AgeSeconds < this.RemoveAfter;
        }
    }
}

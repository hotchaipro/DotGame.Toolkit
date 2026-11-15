// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Particles;
using Xunit;
using static DotGame.Toolkit.Tests.Particles.ParticleTestHelpers;

// NOTE: These test the plain wait. One with a token takes another path, and says so
#pragma warning disable xUnit1051

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// The simulation: motion, emission, capacity, and the system's lifecycle.
/// </summary>
public sealed class ParticleSystemTests
{
    // ── Frame-rate independence ────────────────────────────────────────

    [Theory]
    [InlineData(30)]
    [InlineData(144)]
    public void Motion_UnderGravityAndDrag_IsTheSameAtAnyFrameRate(
        double fps)
    {
        var effect = Effect() with
        {
            Speed = 100,
            DirectionDegrees = -45,
            Gravity = new Vector2(0, 200),
            Drag = new Vector2(0.5f, 1.5f),
        };

        var reference = NewSystem(effect);
        reference.Burst(Vector2.Zero, 1);
        Run(reference, 1, fps: 60);

        var system = NewSystem(effect);
        system.Burst(Vector2.Zero, 1);
        Run(system, 1, fps);

        var expected = Drawn(reference)[0];
        var actual = Drawn(system)[0];
        Assert.Equal(expected.Position.X, actual.Position.X, 0.01f);
        Assert.Equal(expected.Position.Y, actual.Position.Y, 0.01f);
        Assert.Equal(expected.Velocity.Y, actual.Velocity.Y, 0.01f);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void ContinuousEmission_EmitsTheSameCountAtAnyFrameRate(
        double fps)
    {
        var system = NewSystem(Effect() with { Rate = 60 });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 1, fps);

        // The emitter starts with a random fraction owed, so it can be one more
        Assert.InRange(system.Count, 60, 61);
    }

    // ── Moving emitters ────────────────────────────────────────────────

    [Fact]
    public void MovingEmitter_SpreadsParticlesEvenlyAlongItsPath()
    {
        var system = NewSystem(Effect() with { RatePerDistance = 0.1f });
        var emitter = system.CreateEmitter(Vector2.Zero);
        system.Update(TimeSpan.FromSeconds(1 / 60.0));

        emitter.Position = new Vector2(100, 0);
        system.Update(TimeSpan.FromSeconds(1 / 60.0));

        var drawn = Drawn(system);
        Assert.InRange(drawn.Length, 10, 11);
        for (int i = 1; i < drawn.Length; i++)
        {
            Assert.Equal(100f / drawn.Length, drawn[i].Position.X - drawn[i - 1].Position.X, 0.01f);
            Assert.Equal(0, drawn[i].Position.Y, 0.001f);
        }
    }

    [Fact]
    public void RatePerDistance_IsEvenAtAnySpeed()
    {
        var fast = NewSystem(Effect() with { RatePerDistance = 0.5f });
        var fastEmitter = fast.CreateEmitter(Vector2.Zero);
        fast.Update(TimeSpan.FromSeconds(0.01));
        fastEmitter.Position = new Vector2(200, 0);
        fast.Update(TimeSpan.FromSeconds(0.01));

        var slow = NewSystem(Effect() with { RatePerDistance = 0.5f });
        var slowEmitter = slow.CreateEmitter(Vector2.Zero);
        slow.Update(TimeSpan.FromSeconds(0.01));
        for (int i = 1; i <= 20; i++)
        {
            slowEmitter.Position = new Vector2(i * 10, 0);
            slow.Update(TimeSpan.FromSeconds(0.01));
        }

        Assert.InRange(fast.Count, 100, 101);
        Assert.InRange(slow.Count, 100, 101);
    }

    [Fact]
    public void Stop_EndsEmission_AndTheParticlesLiveOn()
    {
        var system = NewSystem(Effect() with { Rate = 60 });
        var emitter = system.CreateEmitter(Vector2.Zero);
        Run(system, 0.5);
        int emitted = system.Count;

        emitter.Stop();
        Run(system, 0.5);

        Assert.False(emitter.IsActive);
        Assert.Equal(emitted, system.Count);
        Assert.True(system.IsActive);
    }

    // ── Emission ───────────────────────────────────────────────────────

    [Fact]
    public void FractionalBudgets_CarryAcrossFrames()
    {
        var system = NewSystem(Effect() with { Rate = 1 });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 1, fps: 4);
        Assert.Equal(1, system.Count);

        Run(system, 1, fps: 4);
        Assert.Equal(2, system.Count);
    }

    [Fact]
    public void Bursts_FireOnceAtTheirTimes()
    {
        var system = NewSystem(Effect() with { Bursts = [new ParticleBurst(0.5f, 5)] });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 0.4, fps: 10);
        Assert.Equal(0, system.Count);

        Run(system, 0.2, fps: 10);
        Assert.Equal(5, system.Count);

        Run(system, 1, fps: 10);
        Assert.Equal(5, system.Count);
    }

    [Fact]
    public void DelayAndDuration_StartAndStopTheEmitter()
    {
        var system = NewSystem(Effect() with { Rate = 10, DelaySeconds = 1, DurationSeconds = 1 });
        var emitter = system.CreateEmitter(Vector2.Zero);

        Run(system, 0.9, fps: 10);
        Assert.Equal(0, system.Count);

        Run(system, 1.6, fps: 10);
        Assert.InRange(system.Count, 10, 11);
        Assert.False(emitter.IsActive);
    }

    [Fact]
    public void RateOverDuration_ScalesTheRate()
    {
        var system = NewSystem(Effect() with { Rate = 100, DurationSeconds = 1, RateOverDuration = ParticleCurve.Between(1, 0) });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 1.5);

        // The average of a rate falling from 100 to 0 over a second
        Assert.InRange(system.Count, 48, 52);
    }

    // ── Capacity ───────────────────────────────────────────────────────

    [Fact]
    public void DropNew_AtCapacity_KeepsTheOldest()
    {
        var system = NewSystem(Effect() with { Capacity = 3, Overflow = ParticleOverflow.DropNew });

        for (int i = 0; i < 5; i++)
        {
            system.Emit(new ParticleSpawn { Position = new Vector2(i, 0) });
        }

        Assert.Equal([0f, 1f, 2f], Drawn(system).Select(p => p.Position.X));
    }

    [Fact]
    public void ReplaceOldest_AtCapacity_KeepsTheNewest_InOrder()
    {
        var system = NewSystem(Effect() with { Capacity = 3, Overflow = ParticleOverflow.ReplaceOldest });

        for (int i = 0; i < 5; i++)
        {
            system.Emit(new ParticleSpawn { Position = new Vector2(i, 0) });
        }

        Assert.Equal([2f, 3f, 4f], Drawn(system).Select(p => p.Position.X));

        // Still oldest first once the ring has wrapped and particles expire
        system.Emit(new ParticleSpawn { Position = new Vector2(5, 0), LifetimeSeconds = 0.05f });
        system.Emit(new ParticleSpawn { Position = new Vector2(6, 0) });
        system.Update(TimeSpan.FromSeconds(0.1));
        Assert.Equal([4f, 6f], Drawn(system).Select(p => p.Position.X));
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public void Bounds_RemoveParticlesThatLeave()
    {
        var system = NewSystem(Effect() with { Speed = 100 });
        system.Bounds = new Rectangle(0, 0, 100, 100);
        system.Emit(new ParticleSpawn { Position = new Vector2(95, 50) });
        system.Emit(new ParticleSpawn { Position = new Vector2(10, 50) });

        system.Update(TimeSpan.FromSeconds(0.1));

        Assert.Equal([20f], Drawn(system).Select(p => MathF.Round(p.Position.X)));
    }

    [Fact]
    public void Clear_RemovesEverything_AndCompletesWaiters()
    {
        var system = NewSystem(Effect() with { Rate = 60 });
        var emitter = system.CreateEmitter(Vector2.Zero);
        Run(system, 0.5);
        var empty = system.WhenEmptyAsync();
        Assert.False(empty.IsCompleted);

        system.Clear();

        Assert.Equal(0, system.Count);
        Assert.False(emitter.IsActive);
        Assert.False(system.IsActive);
        Assert.True(empty.IsCompletedSuccessfully);
    }

    [Fact]
    public void WhenEmptyAsync_CompletesWhenTheParticlesExpire()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.5f });
        Assert.True(system.WhenEmptyAsync().IsCompleted);

        system.Burst(Vector2.Zero, 10);
        var empty = system.WhenEmptyAsync();
        Run(system, 0.4);
        Assert.False(empty.IsCompleted);

        Run(system, 0.2);
        Assert.True(empty.IsCompletedSuccessfully);
    }

    [Fact]
    public void Burst_EmitsEachEffect()
    {
        var system = NewSystem(Effect(), Effect());

        system.Burst(new Vector2(10, 10), 4);

        Assert.Equal(8, system.Count);
        Assert.Equal(4, Drawn(system, 1).Length);
    }

    [Fact]
    public void BurstInArea_StartsParticlesInsideIt()
    {
        var system = NewSystem(Effect());
        var area = new Rectangle(100, 200, 50, 20);

        system.Burst(area, 50);

        Assert.All(Drawn(system), p => Assert.True(area.Contains(p.Position)));
    }

    [Fact]
    public void Origin_IsWhereTheParticleWasEmitted_BeforeTheShape()
    {
        var origins = new List<Vector2>();
        var system = NewSystem(Effect() with { Shape = EmitShape.Rectangle(100, 100), Behavior = new OriginProbe(origins) });

        system.Burst(new Vector2(40, 60), 1);
        system.Emit(new ParticleSpawn { Position = new Vector2(5, 7) });

        Assert.Equal([new Vector2(40, 60), new Vector2(5, 7)], origins);
    }

    private sealed class OriginProbe(List<Vector2> origins) : IParticleBehavior
    {
        public void Start(
            ref Particle particle)
        {
            origins.Add(particle.Origin);
        }

        public bool Update(
            ref Particle particle,
            float elapsedSeconds)
        {
            return true;
        }
    }

    // ── Pooling ────────────────────────────────────────────────────────

    [Fact]
    public void EndedEmitter_DoesNotControlTheEmitterReusingItsSlot()
    {
        var system = NewSystem(Effect() with { Rate = 60 });
        var first = system.CreateEmitter(Vector2.Zero);
        first.Stop();
        system.Update(TimeSpan.FromSeconds(0.1));

        var second = system.CreateEmitter(new Vector2(50, 0));
        first.Position = new Vector2(-100, 0);
        first.Stop();

        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
        Assert.Equal(new Vector2(50, 0), second.Position);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SteadyState_AllocatesNothing()
    {
        var system = NewSystem(
            Effect() with { LifetimeSeconds = 0.5f, Rate = 120, Capacity = 64, Speed = 50, Drag = new Vector2(1) },
            Effect() with { LifetimeSeconds = 0.25f, Capacity = 32 });
        var frame = TimeSpan.FromSeconds(1 / 60.0);

        // Emitters, bursts with per-burst colors, and placed spawns come and go, and the
        // system empties at the end, returning its buffers
        void Cycle()
        {
            for (int i = 0; i < 4; i++)
            {
                var emitter = system.CreateEmitter(new Vector2(i, 0));
                system.Burst(Vector2.Zero, 8, new ParticleOverrides { Color = ParticleColor.Between(Color.Red, Color.White), DirectionDegrees = (0, 90) });
                system.Emit(new ParticleSpawn { Position = Vector2.One, Velocity = Vector2.UnitX, Color = Color.Blue, Layer = 1 });
                for (int f = 0; f < 10; f++)
                {
                    emitter.Position += Vector2.UnitX;
                    system.Update(frame);
                    _ = system.GetDrawStates(0, Color.White);
                }

                emitter.Stop();
            }

            for (int f = 0; f < 60; f++)
            {
                system.Update(frame);
            }

            Assert.False(system.IsActive);
        }

        // The first cycle grows the emitter pool and rents the buffers
        Cycle();

        long before = GC.GetAllocatedBytesForCurrentThread();
        Cycle();
        Cycle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}

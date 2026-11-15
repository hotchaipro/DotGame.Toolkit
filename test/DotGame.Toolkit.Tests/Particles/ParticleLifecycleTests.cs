// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Toolkit.Graphics;
using DotGame.Toolkit.Particles;
using Xunit;
using static DotGame.Toolkit.Tests.Particles.ParticleTestHelpers;

// NOTE: These test the plain wait. One with a token takes another path, and says so
#pragma warning disable xUnit1051

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// The edges of the simulation: the ring of particles against a model of it, exact motion,
/// emission's corners, the emitter pool, waiting for a system to empty, and drawing through a
/// real sprite batch.
/// </summary>
public sealed class ParticleLifecycleTests
{
    private static readonly TimeSpan Quarter = TimeSpan.FromSeconds(0.25);

    // The ring against a model

    [Theory]
    [InlineData(ParticleOverflow.DropNew, 1, 11)]
    [InlineData(ParticleOverflow.DropNew, 5, 22)]
    [InlineData(ParticleOverflow.DropNew, 16, 33)]
    [InlineData(ParticleOverflow.ReplaceOldest, 1, 44)]
    [InlineData(ParticleOverflow.ReplaceOldest, 5, 55)]
    [InlineData(ParticleOverflow.ReplaceOldest, 16, 66)]
    public void Ring_BehavesLikeAListOfParticles_OldestFirst(
        ParticleOverflow overflow,
        int capacity,
        int seed)
    {
        var system = NewSystem(Effect() with { Capacity = capacity, Overflow = overflow });
        var model = new List<(float Id, float Remaining)>();
        var random = new Random(seed);
        float nextId = 1;

        for (int step = 0; step < 2000; step++)
        {
            if (random.Next(3) != 0)
            {
                // Lifetimes in quarters of a second, which floats hold exactly
                float lifetime = random.Next(1, 8) * 0.25f;
                system.Emit(new ParticleSpawn { Position = new Vector2(nextId, 0), LifetimeSeconds = lifetime });

                if (model.Count == capacity)
                {
                    if (overflow == ParticleOverflow.ReplaceOldest)
                    {
                        model.RemoveAt(0);
                        model.Add((nextId, lifetime));
                    }
                }
                else
                {
                    model.Add((nextId, lifetime));
                }

                nextId += 1;
            }
            else
            {
                system.Update(Quarter);
                for (int i = model.Count - 1; i >= 0; i--)
                {
                    model[i] = (model[i].Id, model[i].Remaining - 0.25f);
                    if (model[i].Remaining <= 0)
                    {
                        model.RemoveAt(i);
                    }
                }
            }

            Assert.Equal(model.Count, system.Count);
            Assert.Equal(model.Select(p => p.Id), Drawn(system).Select(p => p.Position.X));
        }
    }

    [Fact]
    public void Ring_EmptiedAndFilledAgain_StartsClean()
    {
        var system = NewSystem(Effect() with { Capacity = 3, LifetimeSeconds = 0.25f });

        for (int round = 0; round < 5; round++)
        {
            system.Burst(new Vector2(round, 0), 2);
            Assert.Equal(2, system.Count);
            Assert.All(Drawn(system), p => Assert.Equal(0, p.AgeSeconds));
            Assert.All(Drawn(system), p => Assert.Equal(round, p.Position.X));

            system.Update(Quarter);
            Assert.Equal(0, system.Count);
            Assert.Empty(Drawn(system));
        }
    }

    // Exact motion

    [Fact]
    public void Gravity_FollowsTheExactPath()
    {
        var system = NewSystem(Effect() with { Speed = 10, DirectionDegrees = 0, Gravity = new Vector2(0, 8) });
        system.Burst(new Vector2(1, 2), 1);

        Run(system, 2, fps: 7);

        var particle = Drawn(system)[0];
        Assert.Equal(1 + (10 * 2), particle.Position.X, 0.01f);
        Assert.Equal(2 + (0.5f * 8 * 2 * 2), particle.Position.Y, 0.01f);
        Assert.Equal(10, particle.Velocity.X, 0.01f);
        Assert.Equal(8 * 2, particle.Velocity.Y, 0.01f);
    }

    [Fact]
    public void Drag_DecaysTowardTheTerminalVelocity()
    {
        var system = NewSystem(Effect() with { Speed = 100, DirectionDegrees = 0, Gravity = new Vector2(0, 20), Drag = new Vector2(2, 4) });
        system.Burst(Vector2.Zero, 1);

        Run(system, 1);

        var particle = Drawn(system)[0];

        // Along X, no gravity: 100e^(-2t), having traveled 100(1 - e^(-2t)) / 2
        Assert.Equal(100 * MathF.Exp(-2), particle.Velocity.X, 0.05f);
        Assert.Equal(50 * (1 - MathF.Exp(-2)), particle.Position.X, 0.05f);

        // Along Y, from rest toward 20 / 4
        Assert.Equal(5 * (1 - MathF.Exp(-4)), particle.Velocity.Y, 0.05f);

        Run(system, 20);
        Assert.Equal(5, Drawn(system)[0].Velocity.Y, 0.01f);
    }

    [Fact]
    public void Velocity_FromASpawn_MovesAParticleInAStillEffect()
    {
        var system = NewSystem(Effect() with { Drag = new Vector2(1, 0) });
        system.Emit(new ParticleSpawn { Position = Vector2.Zero, Velocity = new Vector2(0, 10) });

        system.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(10, Drawn(system)[0].Position.Y, 0.001f);
    }

    [Fact]
    public void Update_WithNoTime_ChangesNothing()
    {
        var system = NewSystem(Effect() with { Speed = 10, Rate = 100 });
        system.CreateEmitter(Vector2.Zero);
        system.Burst(Vector2.Zero, 1);
        var before = Drawn(system)[0].Position;

        system.Update(TimeSpan.Zero);
        system.Update(TimeSpan.FromSeconds(-1));

        Assert.Equal(1, system.Count);
        Assert.Equal(before, Drawn(system)[0].Position);
    }

    [Fact]
    public void Update_OfAnIdleSystem_DoesNothing()
    {
        var system = NewSystem(Effect());

        system.Update(Quarter);

        Assert.False(system.IsActive);
        Assert.Equal(0, system.Count);
    }

    // Emitting

    [Fact]
    public void ALongFrame_AgesEachParticleByWhenItWasEmitted()
    {
        var system = NewSystem(Effect() with { Rate = 2, Speed = 10, DirectionDegrees = 0 });
        system.CreateEmitter(Vector2.Zero);

        system.Update(TimeSpan.FromSeconds(5));

        var drawn = Drawn(system);
        Assert.InRange(drawn.Length, 10, 11);
        foreach (var particle in drawn)
        {
            Assert.Equal(10 * particle.AgeSeconds, particle.Position.X, 0.001f);
            Assert.InRange(particle.AgeSeconds, 0, 5);
        }

        // Oldest first, evenly apart
        for (int i = 1; i < drawn.Length; i++)
        {
            Assert.True(drawn[i].AgeSeconds < drawn[i - 1].AgeSeconds);
        }
    }

    [Fact]
    public void ALongFrame_StopsAtTheEndOfTheDuration()
    {
        var system = NewSystem(Effect() with { Rate = 10, DurationSeconds = 1 });
        var emitter = system.CreateEmitter(Vector2.Zero);

        system.Update(TimeSpan.FromSeconds(10));

        Assert.InRange(system.Count, 10, 11);
        Assert.False(emitter.IsActive);
    }

    [Fact]
    public void Particles_ThatDieWithinTheFrameTheyreEmitted_AreNotLeftBehind()
    {
        var system = NewSystem(Effect() with { Rate = 1000, LifetimeSeconds = 0.001f });
        var emitter = system.CreateEmitter(Vector2.Zero);

        for (int i = 0; i < 20; i++)
        {
            system.Update(Quarter);

            // Only those emitted in the frame's last millisecond are still alive
            Assert.InRange(system.Count, 0, 2);
        }

        emitter.Stop();
        Run(system, 1);
        Assert.False(system.IsActive);
    }

    [Fact]
    public void Behavior_RemovingAParticleAsItsEmitted_LeavesTheOthers()
    {
        var system = NewSystem(Effect() with { Rate = 100, Behavior = new KeepEveryOther() });
        system.CreateEmitter(Vector2.Zero);

        system.Update(TimeSpan.FromSeconds(1));

        Assert.InRange(system.Count, 49, 51);
    }

    private sealed class KeepEveryOther : IParticleBehavior
    {
        private int _count;

        public void Start(
            ref Particle particle)
        {
        }

        public bool Update(
            ref Particle particle,
            float elapsedSeconds)
        {
            return (this._count++ % 2) == 0;
        }
    }

    [Fact]
    public void Bursts_FireInTimeOrder_HoweverTheyreListed_AfterTheDelay()
    {
        var system = NewSystem(Effect() with
        {
            DelaySeconds = 1,
            Bursts = [new ParticleBurst(0.5f, 3), new ParticleBurst(0, 1), new ParticleBurst(0.25f, 2)],
        });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 0.75, fps: 8);
        Assert.Equal(0, system.Count);

        Run(system, 0.5, fps: 8);
        Assert.Equal(1, system.Count);

        Run(system, 0.25, fps: 8);
        Assert.Equal(3, system.Count);

        Run(system, 0.25, fps: 8);
        Assert.Equal(6, system.Count);
    }

    [Fact]
    public void Bursts_InALongFrame_AllFire_AndOnlyOnce()
    {
        var system = NewSystem(Effect() with { Bursts = [new ParticleBurst(0, 1), new ParticleBurst(0.25f, 2), new ParticleBurst(0.5f, 3)] });
        system.CreateEmitter(Vector2.Zero);

        system.Update(TimeSpan.FromSeconds(1));
        Assert.Equal(6, system.Count);

        system.Update(TimeSpan.FromSeconds(1));
        Assert.Equal(6, system.Count);
    }

    [Fact]
    public void Bursts_AgeFromTheirOwnTime()
    {
        var system = NewSystem(Effect() with { Bursts = [new ParticleBurst(0.25f, 1)] });
        system.CreateEmitter(Vector2.Zero);

        system.Update(TimeSpan.FromSeconds(1));

        Assert.Equal(0.75f, Drawn(system)[0].AgeSeconds, 0.001f);
    }

    [Fact]
    public void RateOverDuration_Constant_ScalesTheRate_WithOrWithoutADuration()
    {
        var withDuration = NewSystem(Effect() with { Rate = 100, DurationSeconds = 1, RateOverDuration = ParticleCurve.Constant(0.5f) });
        withDuration.CreateEmitter(Vector2.Zero);
        Run(withDuration, 1);
        Assert.InRange(withDuration.Count, 49, 51);

        var withoutDuration = NewSystem(Effect() with { Rate = 100, RateOverDuration = ParticleCurve.Constant(0.5f) });
        withoutDuration.CreateEmitter(Vector2.Zero);
        Run(withoutDuration, 1);
        Assert.InRange(withoutDuration.Count, 49, 51);
    }

    [Fact]
    public void RateOverDuration_FollowsTheDuration_NotTheDelay()
    {
        var system = NewSystem(Effect() with { Rate = 100, DelaySeconds = 10, DurationSeconds = 1, RateOverDuration = ParticleCurve.Between(0, 1) });
        system.CreateEmitter(Vector2.Zero);

        Run(system, 10);
        Assert.Equal(0, system.Count);

        Run(system, 1.5);
        Assert.InRange(system.Count, 48, 52);
    }

    [Fact]
    public void Effects_WithDifferentDurations_EndSeparately()
    {
        var short_ = Effect() with { Rate = 10, DurationSeconds = 1 };
        var long_ = Effect() with { Rate = 10, DurationSeconds = 3 };
        var system = NewSystem(short_, long_);
        var emitter = system.CreateEmitter(Vector2.Zero);

        Run(system, 2, fps: 10);
        Assert.True(emitter.IsActive);
        Assert.InRange(Drawn(system, 0).Length, 10, 11);
        Assert.InRange(Drawn(system, 1).Length, 20, 21);

        Run(system, 1.5, fps: 10);
        Assert.False(emitter.IsActive);
        Assert.InRange(Drawn(system, 0).Length, 10, 11);
        Assert.InRange(Drawn(system, 1).Length, 30, 31);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Burst_OfNothing_EmitsNothing(
        int count)
    {
        var system = NewSystem(Effect());

        system.Burst(Vector2.Zero, count);

        Assert.False(system.IsActive);
    }

    [Fact]
    public void Spawn_PlacesExactly_IgnoringTheEffectsShape()
    {
        var system = NewSystem(Effect() with { Shape = EmitShape.Rectangle(100, 100) });

        system.Emit(new ParticleSpawn { Position = new Vector2(5, 7) });

        Assert.Equal(new Vector2(5, 7), Drawn(system)[0].Position);
    }

    [Fact]
    public void Spawn_OverridesTheEffectsValues()
    {
        var system = NewSystem(Effect());

        system.Emit(new ParticleSpawn
        {
            Position = Vector2.Zero,
            Velocity = new Vector2(3, 4),
            LifetimeSeconds = 7,
            Scale = new Vector2(2, 3),
            RotationDegrees = 90,
            Color = Color.Red,
            Opacity = 0.5f,
            Variant = 42,
        });
        system.Update(TimeSpan.FromSeconds(3.5));

        var particle = Drawn(system)[0];
        Assert.Equal(new Vector2(3, 4), particle.Velocity);
        Assert.Equal(0.5f, particle.NormalizedAge, 0.001f);
        Assert.Equal(new Vector2(2, 3), particle.Scale);
        Assert.Equal(Angle.FromDegrees(90).Radians, particle.Rotation.Radians, 0.001f);
        Assert.Equal(Color.Red * 0.5f, particle.Color);
        Assert.Equal(42u, particle.Variant);
    }

    [Fact]
    public void Spawn_Color_KeepsTheEffectsEndColor()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 1, EndColor = ParticleColor.Constant(Color.Blue) });

        system.Emit(new ParticleSpawn { Position = Vector2.Zero, Color = Color.Red });
        system.Update(TimeSpan.FromSeconds(0.5));

        Assert.Equal(Color.Lerp(Color.Red, Color.Blue, 0.5f), Drawn(system)[0].Color);
    }

    [Fact]
    public void Overrides_ReplaceTheShapeDirectionAndColor()
    {
        var system = NewSystem(Effect() with { Speed = 10, DirectionDegrees = 0, Shape = EmitShape.Rectangle(100, 100) });

        system.Burst(
            new Vector2(1, 2),
            1,
            new ParticleOverrides { Shape = EmitShape.Point, DirectionDegrees = 90, Color = ParticleColor.Constant(Color.Green) });

        var particle = Drawn(system)[0];
        Assert.Equal(new Vector2(1, 2), particle.Position);
        Assert.Equal(0, particle.Velocity.X, 0.001f);
        Assert.Equal(10, particle.Velocity.Y, 0.001f);
        Assert.Equal(Color.Green, particle.Color);
    }

    [Fact]
    public void BurstInArea_LeavesAShapeFromTheOverrides()
    {
        var system = NewSystem(Effect());

        system.Burst(new Rectangle(100, 200, 50, 20), 1, new ParticleOverrides { Shape = EmitShape.Point });

        Assert.Equal(new Vector2(125, 210), Drawn(system)[0].Position);
    }

    // Shapes

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EmitterScale_ScalesTheShape_ForEffectsThatAskForIt(
        float scale)
    {
        var system = NewSystem(Effect() with { Rate = 1000, Shape = EmitShape.Ring(10), ShapeScalesWithEmitter = true });
        var emitter = system.CreateEmitter(Vector2.Zero);
        emitter.Scale = scale;
        Assert.Equal(scale, emitter.Scale);

        system.Update(TimeSpan.FromSeconds(0.05));

        Assert.NotEmpty(Drawn(system));
        Assert.All(Drawn(system), p => Assert.Equal(10 * scale, p.Position.Length(), 0.01f));
    }

    [Fact]
    public void EmitterScale_LeavesOtherEffectsShapes()
    {
        var system = NewSystem(Effect() with { Rate = 1000, Shape = EmitShape.Ring(10) });
        var emitter = system.CreateEmitter(Vector2.Zero);
        emitter.Scale = 3;

        system.Update(TimeSpan.FromSeconds(0.05));

        Assert.All(Drawn(system), p => Assert.Equal(10, p.Position.Length(), 0.01f));
    }

    [Fact]
    public void Shapes_StayWithinTheirBounds()
    {
        var random = new ParticleRandom(99);

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(Vector2.Zero, EmitShape.Point.Sample(random, 1));

            Assert.InRange(EmitShape.Circle(10).Sample(random, 2).Length(), 0, 20.001f);
            Assert.Equal(20, EmitShape.Ring(10).Sample(random, 2).Length(), 0.01f);

            var inRectangle = EmitShape.Rectangle(10, 20).Sample(random, 2);
            Assert.InRange(inRectangle.X, -10, 10);
            Assert.InRange(inRectangle.Y, -20, 20);

            // On the line through the emitter
            var onLine = EmitShape.Line(new Vector2(10, 20)).Sample(random, 1);
            Assert.Equal(onLine.X * 2, onLine.Y, 0.001f);
            Assert.InRange(onLine.X, -5, 5);
        }
    }

    [Fact]
    public void Random_WithASeed_IsRepeatable()
    {
        var first = NewSystem(Effect() with { Speed = (10, 20), LifetimeSeconds = (1, 2), Shape = EmitShape.Circle(10) });
        var second = NewSystem(Effect() with { Speed = (10, 20), LifetimeSeconds = (1, 2), Shape = EmitShape.Circle(10) });

        first.Burst(Vector2.Zero, 50);
        second.Burst(Vector2.Zero, 50);

        Assert.Equal(Drawn(first).Select(p => (p.Position, p.Velocity, p.Variant)), Drawn(second).Select(p => (p.Position, p.Velocity, p.Variant)));
    }

    // The emitter pool

    [Fact]
    public void Pool_GrowsPastItsFirstSize_KeepingEachEmittersState()
    {
        var system = NewSystem(
            Effect() with { Bursts = [new ParticleBurst(0, 1)] },
            Effect() with { Bursts = [new ParticleBurst(0.5f, 2)] });

        var emitters = new ParticleEmitter[9];
        for (int i = 0; i < emitters.Length; i++)
        {
            emitters[i] = system.CreateEmitter(new Vector2(i, 0));
        }

        system.Update(Quarter);
        Assert.Equal(9, Drawn(system, 0).Length);
        Assert.Empty(Drawn(system, 1));

        // Grow again, with these part way through their runs
        for (int i = 0; i < 9; i++)
        {
            system.CreateEmitter(new Vector2(100, 0));
        }

        Assert.All(emitters, e => Assert.True(e.IsActive));
        Assert.Equal(new Vector2(3, 0), emitters[3].Position);
        Assert.Equal(0.25f, emitters[3].TimeSeconds);

        system.Update(TimeSpan.FromSeconds(0.5));
        Assert.Equal(18, Drawn(system, 0).Length);
        Assert.Equal(18, Drawn(system, 1).Length);
    }

    [Fact]
    public void Emitter_TimeAccumulates_AndAStoppedOneReadsAsEnded()
    {
        var system = NewSystem(Effect());
        var emitter = system.CreateEmitter(new Vector2(1, 2));

        system.Update(Quarter);
        system.Update(Quarter);
        Assert.Equal(0.5f, emitter.TimeSeconds);

        emitter.Stop();

        Assert.Equal(0, emitter.TimeSeconds);
        Assert.Equal(0, emitter.Scale);
        Assert.Equal(Vector2.Zero, emitter.Position);
    }

    [Fact]
    public void Emitter_Default_IsInactive_AndDoesNothing()
    {
        ParticleEmitter emitter = default;

        emitter.Position = Vector2.One;
        emitter.Scale = 2;
        emitter.Stop();

        Assert.False(emitter.IsActive);
        Assert.Equal(Vector2.Zero, emitter.Position);
        Assert.True(emitter == default);
    }

    [Fact]
    public void Emitter_StoppedThisFrame_KeepsItsSlotUntilTheNextUpdate()
    {
        var system = NewSystem(Effect() with { Rate = 60 });
        var first = system.CreateEmitter(Vector2.Zero);
        first.Stop();

        var second = system.CreateEmitter(Vector2.Zero);

        Assert.NotEqual(first, second);
        Assert.False(first.IsActive);
        Assert.True(second.IsActive);

        Run(system, 1);
        Assert.InRange(system.Count, 60, 61);
    }

    [Fact]
    public void Clear_ThenCreate_ReusesThePool_WithFreshState()
    {
        var system = NewSystem(Effect() with { Bursts = [new ParticleBurst(0, 5)] });
        var old = system.CreateEmitter(Vector2.Zero);
        system.Update(Quarter);
        Assert.Equal(5, system.Count);

        system.Clear();
        var emitter = system.CreateEmitter(Vector2.One);

        Assert.False(old.IsActive);
        Assert.Equal(0, emitter.TimeSeconds);
        Assert.Equal(1, emitter.Scale);

        // Its burst fires again, from where it is now
        system.Update(Quarter);
        Assert.Equal(5, system.Count);
        Assert.All(Drawn(system), p => Assert.Equal(Vector2.One, p.Position));
    }

    // Waiting for a system to empty

    [Fact]
    public void WhenEmpty_WaitsForRunningEmitters_NotJustParticles()
    {
        var system = NewSystem(Effect() with { Rate = 0 });
        var emitter = system.CreateEmitter(Vector2.Zero);

        var empty = system.WhenEmptyAsync();
        system.Update(Quarter);
        Assert.False(empty.IsCompleted);

        emitter.Stop();
        system.Update(Quarter);
        Assert.True(empty.IsCompletedSuccessfully);
    }

    [Fact]
    public void WhenEmpty_CompletesEveryWaiter()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);

        var first = system.WhenEmptyAsync();
        var second = system.WhenEmptyAsync();
        system.Update(Quarter);

        Assert.True(first.IsCompletedSuccessfully);
        Assert.True(second.IsCompletedSuccessfully);
    }

    [Fact]
    public void WhenEmpty_ANewWaitAfterAnEmission_WaitsForThatToo()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);
        system.Update(Quarter);

        system.Burst(Vector2.Zero, 1);
        var empty = system.WhenEmptyAsync();
        Assert.False(empty.IsCompleted);

        system.Update(Quarter);
        Assert.True(empty.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WhenEmpty_Continuation_DoesNotRunInsideUpdate()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);
        bool inUpdate = false;
        bool ranInUpdate = false;
        int thread = Environment.CurrentManagedThreadId;

        var continuation = system.WhenEmptyAsync().ContinueWith(
            _ => ranInUpdate = inUpdate && (Environment.CurrentManagedThreadId == thread),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        inUpdate = true;
        system.Update(Quarter);
        inUpdate = false;

        await continuation;
        Assert.False(ranInUpdate);
    }

    [Fact]
    public async Task WhenEmpty_AWaiterThatEmits_DoesNotCompleteTheNextWaiterEarly()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);

        // NOTE: The gate holds the first waiter's burst until the check, so it can't race it
        using var gate = new ManualResetEventSlim();
        var first = system.WhenEmptyAsync();
        var emitted = first.ContinueWith(
            _ =>
            {
                gate.Wait();
                system.Burst(Vector2.Zero, 1);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        var second = system.WhenEmptyAsync();

        system.Update(Quarter);

        Assert.True(second.IsCompleted);
        Assert.False(system.IsActive);
        gate.Set();
        await emitted;
    }

    [Fact]
    public async Task WhenEmpty_WithAToken_CanBeCanceled_ForASystemNoLongerUpdated()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);
        using var leaving = new CancellationTokenSource();

        var empty = system.WhenEmptyAsync(leaving.Token);
        Assert.False(empty.IsCompleted);

        leaving.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => empty);
    }

    [Fact]
    public async Task WhenEmpty_WithAToken_CompletesWhenEmpty()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 1);
        using var leaving = new CancellationTokenSource();

        var empty = system.WhenEmptyAsync(leaving.Token);
        system.Update(Quarter);

        await empty;
    }

    [Fact]
    public void Reserve_KeepsWorking_AndReleaseAfterIt()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 0.25f });
        system.Reserve();

        system.Burst(Vector2.Zero, 1);
        system.Update(Quarter);
        Assert.False(system.IsActive);

        system.Burst(Vector2.Zero, 1);
        Assert.True(system.IsActive);
        system.Update(Quarter);

        system.ReleaseReserved();
        system.Burst(Vector2.Zero, 1);
        Assert.True(system.IsActive);
    }

    [Fact]
    public void Seed_GivesTheSameParticles()
    {
        static List<float> Run()
        {
            var system = new ParticleSystem(1234, Effect() with { LifetimeSeconds = 2f, Speed = new FloatRange(10, 90) });
            system.Burst(Vector2.Zero, 8);
            system.Update(Quarter);
            var batch = new RecordingBatch();
            system.Draw(batch.Batch);
            return [.. batch.Calls.Select(call => call.Left + call.Top)];
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Teleport_OfAnActiveEmitter_MovesIt()
    {
        var system = NewSystem(Effect() with { LifetimeSeconds = 2f });
        var emitter = system.CreateEmitter(Vector2.Zero);

        emitter.Teleport(new Vector2(500, 0));

        Assert.Equal(new Vector2(500, 0), emitter.Position);
    }

    // Creating

    [Fact]
    public void System_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ParticleSystem((ParticleEffect[])null!));
        Assert.Throws<ArgumentNullException>(() => new ParticleSystem((ParticleEffect)null!));
    }

    [Fact]
    public void System_RejectsAnEffectWithNoRenderer()
    {
        Assert.Throws<ArgumentException>(() => new ParticleSystem(Effect() with { Renderer = null! }));
    }

    [Fact]
    public void System_KeepsItsOwnListOfEffects()
    {
        var first = Effect();
        var second = Effect() with { Capacity = 7 };
        var effects = new[] { first, second };

        var system = new ParticleSystem(effects);
        effects[0] = second;

        Assert.Same(first, system.Effects[0]);
        Assert.Same(second, system.Effects[1]);
    }

    // Drawing through a real sprite batch

    [Fact]
    public void Draw_ThroughASpriteRenderer_ReachesTheBatchInOrder()
    {
        var texture = TestTextures.Create(64, 64);
        var region = new TextureRegion(texture.AsSource(), new RectangleInt(8, 8, 16, 16));
        var system = NewSystem(
            new ParticleEffect { Renderer = ParticleRenderer.Sprite(region), LifetimeSeconds = 100, DirectionDegrees = 0 },
            new ParticleEffect { Renderer = ParticleRenderer.Sprite(texture.AsSource()), LifetimeSeconds = 100, DirectionDegrees = 0 });
        system.Burst(new Vector2(100, 200), 1);
        var batch = new RecordingBatch();

        system.Draw(batch.Batch);

        Assert.Equal(2, batch.Calls.Count);
        Assert.Equal(new Rectangle(8, 8, 16, 16), batch.Calls[0].Source);
        Assert.Equal(new Vector2(8, 8), batch.Calls[0].Origin);
        Assert.Equal(new Vector2(100, 200), batch.Calls[0].Position);
        Assert.Equal(new Rectangle(0, 0, 64, 64), batch.Calls[1].Source);
    }

    [Fact]
    public void Draw_TrimmedSprite_KeepsItsPixelsInPlace()
    {
        var texture = TestTextures.Create(64, 64);
        var trimmed = new TextureRegion(texture.AsSource(), new RectangleInt(0, 0, 32, 32), new RectangleInt(4, 8, 16, 16));
        var system = NewSystem(new ParticleEffect { Renderer = ParticleRenderer.Sprite(trimmed), LifetimeSeconds = 100, DirectionDegrees = 0 });
        system.Burst(new Vector2(100, 100), 1);
        var batch = new RecordingBatch();

        system.Draw(batch.Batch);

        // Centered on the logical 32 x 32: the frame's corner at logical (4, 8) is (-12, -8) off
        var call = Assert.Single(batch.Calls);
        Assert.Equal(88, call.Left);
        Assert.Equal(92, call.Top);
    }

    [Fact]
    public void Draw_TintsEveryParticle()
    {
        var system = NewSystem(new ParticleEffect { Renderer = ParticleRenderer.Sprite(TestTextures.Create(8, 8).AsSource()), LifetimeSeconds = 100, DirectionDegrees = 0 });
        system.Burst(Vector2.Zero, 3);
        var batch = new RecordingBatch();

        system.Draw(batch.Batch, Color.White * 0.5f);

        Assert.Equal(3, batch.Calls.Count);
        Assert.All(batch.Calls, c => Assert.Equal(Color.White * 0.5f, c.Color));
    }

    [Fact]
    public void Draw_TintedToNothing_DrawsNothing()
    {
        var system = NewSystem(new ParticleEffect { Renderer = ParticleRenderer.Sprite(TestTextures.Create(8, 8).AsSource()), LifetimeSeconds = 100, DirectionDegrees = 0 });
        system.Burst(Vector2.Zero, 3);
        var batch = new RecordingBatch();

        system.Draw(batch.Batch, Color.Transparent);

        Assert.Empty(batch.Calls);
    }

    [Fact]
    public void Draw_RejectsANullBatch()
    {
        var system = NewSystem(Effect());

        Assert.Throws<ArgumentNullException>(() => system.Draw(null!));
        Assert.Throws<ArgumentNullException>(() => system.Draw(null!, Color.White));
    }

    [Fact]
    public void Draw_AfterTheLastParticleEnds_DrawsNothing()
    {
        var system = NewSystem(new ParticleEffect { Renderer = ParticleRenderer.Sprite(TestTextures.Create(8, 8).AsSource()), LifetimeSeconds = 0.25f });
        system.Burst(Vector2.Zero, 3);
        system.Update(Quarter);
        var batch = new RecordingBatch();

        system.Draw(batch.Batch);

        Assert.Empty(batch.Calls);
    }
}

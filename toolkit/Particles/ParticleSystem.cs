// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// Runs particle effects: emits, moves, and draws their particles. The caller drives it, as
/// with a sprite animation player: call <see cref="Update"/> each frame, and
/// <see cref="Draw(SpriteBatch)"/> inside a sprite batch's <c>Begin</c> and <c>End</c>.
/// </summary>
/// <remarks>
/// <para>
/// A system runs one or more effects, as layers drawn in order: each emission (a burst, or an
/// emitter) emits each effect's particles.
/// </para>
/// <para>
/// It allocates nothing once running: each effect's particles live in a buffer of its
/// <see cref="ParticleEffect.Capacity"/>, rented from a shared pool when the effect's first
/// particle is emitted and returned when its last one ends, so an idle system holds no
/// particle memory and busy ones reuse the same buffers. Emitters are pooled slots that grow
/// to the most that run at once and are reused from then on.
/// </para>
/// <para>
/// Particles, emitters, bursts and <see cref="Bounds"/> are all in the system's space, which
/// is whatever space it's drawn in. Particles that trail behind something moving need
/// nothing special; particles that move with something (a ship's exhaust that turns with the
/// ship) are drawn in <c>spriteBatch.Begin(transform)</c> with its transform, with their
/// emitter in its local space.
/// </para>
/// </remarks>
public sealed class ParticleSystem
{
    private const float DegreesToRadians = MathF.PI / 180;

    private readonly Layer[] _layers;
    private readonly ParticleEffect[] _effects;
    private readonly ParticleRandom _random;

    // The emitter pool: each slot's state, and per slot and effect (slot * effects + effect)
    // the particles owed, the next burst, and whether the effect's duration is over. Slots
    // are running (in the order they were created), free, or never used yet.
    private EmitterSlot[] _emitterSlots = [];
    private float[] _emitterBudgets = [];
    private int[] _emitterNextBursts = [];
    private bool[] _emitterFinished = [];
    private int[] _runningEmitters = [];
    private int _runningEmitterCount;
    private int[] _freeEmitters = [];
    private int _freeEmitterCount;
    private int _usedEmitterSlots;
    private List<TaskCompletionSource>? _emptyWaiters;

    /// <summary>Makes a system of these effects. Give it a seed to make it play the same way every time.</summary>
    public ParticleSystem(
        params ParticleEffect[] effects)
        : this(new ParticleRandom(), effects)
    {
    }

    /// <summary>
    /// A system whose random choices follow from <paramref name="seed"/>, so the same emissions
    /// and updates give the same particles: for replays, and for effects that must look the same
    /// each time.
    /// </summary>
    public ParticleSystem(
        int seed,
        params ParticleEffect[] effects)
        : this(new ParticleRandom(seed), effects)
    {
    }

    /// <summary>
    /// A system that samples its effects from <paramref name="random"/>, for tests.
    /// </summary>
    internal ParticleSystem(
        ParticleRandom random,
        params ParticleEffect[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        if (effects.Length == 0)
        {
            throw new ArgumentException("A particle system needs at least one effect.", nameof(effects));
        }

        this._random = random;
        this._effects = (ParticleEffect[])effects.Clone();
        this._layers = new Layer[effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            this._layers[i] = new Layer(effects[i]);
        }
    }

    /// <summary>
    /// The effects the system runs, drawn in this order.
    /// </summary>
    public IReadOnlyList<ParticleEffect> Effects => this._effects;

    /// <summary>
    /// The area particles live in, in the system's space: one that leaves it is removed (as
    /// confetti that falls below the screen). Null for no limit.
    /// </summary>
    public Rectangle? Bounds { get; set; }

    /// <summary>
    /// How many particles there are.
    /// </summary>
    public int Count
    {
        get
        {
            int count = 0;
            foreach (var layer in this._layers)
            {
                count += layer.Count;
            }

            return count;
        }
    }

    /// <summary>
    /// Whether there are particles or running emitters.
    /// </summary>
    public bool IsActive => (this._runningEmitterCount > 0) || (this.Count > 0);

    /// <summary>
    /// Emits <paramref name="count"/> particles of each effect at once, around
    /// <paramref name="position"/> (in the system's space).
    /// </summary>
    public void Burst(
        Vector2 position,
        int count,
        in ParticleOverrides overrides = default)
    {
        foreach (var layer in this._layers)
        {
            for (int i = 0; i < count; i++)
            {
                layer.Spawn(position, 1, in overrides, 0, this._random);
            }
        }
    }

    /// <summary>
    /// Emits <paramref name="count"/> particles of each effect at once, anywhere in
    /// <paramref name="area"/> (unless <paramref name="overrides"/> has a shape).
    /// </summary>
    public void Burst(
        Rectangle area,
        int count,
        in ParticleOverrides overrides = default)
    {
        var areaOverrides = overrides with { Shape = overrides.Shape ?? EmitShape.Rectangle(area.Width, area.Height) };
        this.Burst(new Vector2(area.X + (area.Width * 0.5f), area.Y + (area.Height * 0.5f)), count, in areaOverrides);
    }

    /// <summary>
    /// Emits one particle placed exactly.
    /// </summary>
    public void Emit(
        in ParticleSpawn spawn)
    {
        if ((uint)spawn.Layer >= (uint)this._layers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(spawn), "The spawn's layer isn't one of the system's effects.");
        }

        this._layers[spawn.Layer].Spawn(in spawn, this._random);
    }

    /// <summary>
    /// Creates an emitter at <paramref name="position"/> (in the system's space), which emits
    /// until it's stopped or its effects' durations end. Move it by setting its
    /// <see cref="ParticleEmitter.Position"/> before each <see cref="Update"/>.
    /// </summary>
    public ParticleEmitter CreateEmitter(
        Vector2 position)
    {
        int slot;
        if (this._freeEmitterCount > 0)
        {
            slot = this._freeEmitters[--this._freeEmitterCount];
        }
        else
        {
            if (this._usedEmitterSlots == this._emitterSlots.Length)
            {
                this.GrowEmitters();
            }

            slot = this._usedEmitterSlots++;
        }

        ref var state = ref this._emitterSlots[slot];
        state.Generation += 1;
        state.IsActive = true;
        state.Position = position;
        state.HasPreviousPosition = false;
        state.Scale = 1;
        state.Time = 0;

        // A random start, so emitters started together don't emit in step
        int first = slot * this._layers.Length;
        for (int i = 0; i < this._layers.Length; i++)
        {
            this._emitterBudgets[first + i] = this._random.NextSingle();
            this._emitterNextBursts[first + i] = 0;
            this._emitterFinished[first + i] = false;
        }

        this._runningEmitters[this._runningEmitterCount++] = slot;
        return new ParticleEmitter(this, slot, state.Generation);
    }

    /// <summary>
    /// Removes every particle and stops every emitter.
    /// </summary>
    public void Clear()
    {
        foreach (var layer in this._layers)
        {
            layer.Clear();
        }

        while (this._runningEmitterCount > 0)
        {
            this.ReleaseEmitter(this._runningEmitterCount - 1);
        }

        this.CompleteEmptyWaiters();
    }

    /// <summary>
    /// Completes when there are no particles and no running emitters. Code awaiting it resumes
    /// after the update that emptied the system, not in the middle of it. It completes only
    /// through <see cref="Update"/> or <see cref="Clear"/>, so a system that's no longer updated
    /// never completes it.
    /// </summary>
    private Task WhenEmpty()
    {
        if (!this.IsActive)
        {
            return Task.CompletedTask;
        }

        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        (this._emptyWaiters ??= new List<TaskCompletionSource>()).Add(waiter);
        return waiter.Task;
    }

    /// <summary>
    /// Completes when there are no particles and no running emitters, or is canceled by
    /// <paramref name="cancellationToken"/>, as when a scene that stops updating the system leaves.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public Task WhenEmptyAsync(
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var empty = this.WhenEmpty();
        if (empty.IsCompleted || !cancellationToken.CanBeCanceled)
        {
            return empty;
        }

        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => waiter.TrySetCanceled(cancellationToken));
        empty.ContinueWith(
            _ =>
            {
                registration.Dispose();
                waiter.TrySetResult();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return waiter.Task;
    }

    /// <summary>
    /// Takes the particle memory now, and keeps it while the system is idle until
    /// <see cref="ReleaseReserved"/>: otherwise it arrives on the first burst, the frame of the
    /// first explosion, and goes back when the particles have gone. Call it on a loading screen.
    /// </summary>
    public void Reserve()
    {
        foreach (var layer in this._layers)
        {
            layer.Reserve();
        }
    }

    /// <summary>
    /// Lets idle particle memory go back to the pool again, after <see cref="Reserve"/>.
    /// </summary>
    public void ReleaseReserved()
    {
        foreach (var layer in this._layers)
        {
            layer.ReleaseReserved();
        }
    }

    /// <summary>
    /// Advances the particles and emitters by <paramref name="elapsed"/>, such as the frame's
    /// <c>Time.Delta</c>.
    /// </summary>
    public void Update(
        TimeSpan elapsed)
    {
        if (!this.IsActive)
        {
            return;
        }

        float elapsedSeconds = (float)elapsed.TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return;
        }

        // Move the particles there are, then emit new ones (aged by the part of the frame
        // since they were emitted), so no particle moves twice
        foreach (var layer in this._layers)
        {
            layer.Update(elapsedSeconds, this.Bounds);
        }

        for (int i = 0; i < this._runningEmitterCount; i++)
        {
            int slot = this._runningEmitters[i];
            if (this._emitterSlots[slot].IsActive)
            {
                this.Emit(slot, elapsedSeconds);
            }
        }

        for (int i = this._runningEmitterCount - 1; i >= 0; i--)
        {
            if (!this._emitterSlots[this._runningEmitters[i]].IsActive)
            {
                this.ReleaseEmitter(i);
            }
        }

        if (!this.IsActive)
        {
            this.CompleteEmptyWaiters();
        }
    }

    /// <summary>
    /// Draws the particles into <paramref name="spriteBatch"/>, which the caller has begun:
    /// each effect in order, its particles oldest first.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch)
    {
        this.Draw(spriteBatch, Color.White);
    }

    /// <summary>
    /// Draws the particles tinted by <paramref name="color"/>: <see cref="Color.White"/> leaves
    /// them unchanged, and <c>Color.White * 0.5f</c> fades the whole system to half opacity.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Color color)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        this.DrawCore(spriteBatch, color);
    }

    /// <summary>
    /// Hands each effect's particles to its renderer; the sprite batch is only passed through,
    /// so tests can draw to renderers that don't use one.
    /// </summary>
    internal void DrawCore(
        SpriteBatch spriteBatch,
        Color color)
    {
        foreach (var layer in this._layers)
        {
            var particles = layer.GetDrawStates(color);
            if (!particles.IsEmpty)
            {
                layer.Effect.Renderer.Draw(spriteBatch, particles);
            }
        }
    }

    /// <summary>
    /// The draw states of an effect's particles, as <see cref="Draw(SpriteBatch, Color)"/>
    /// hands them to its renderer, for tests.
    /// </summary>
    internal ReadOnlySpan<ParticleDrawState> GetDrawStates(
        int effect,
        Color color)
    {
        return this._layers[effect].GetDrawStates(color);
    }

    internal bool IsEmitterActive(
        int slot,
        int generation)
    {
        return ((uint)slot < (uint)this._usedEmitterSlots)
            && (this._emitterSlots[slot].Generation == generation)
            && this._emitterSlots[slot].IsActive;
    }

    internal Vector2 GetEmitterPosition(
        int slot)
    {
        return this._emitterSlots[slot].Position;
    }

    internal void TeleportEmitter(
        int slot,
        Vector2 position)
    {
        ref var state = ref this._emitterSlots[slot];
        state.Position = position;
        state.HasPreviousPosition = false;
    }

    internal void SetEmitterPosition(
        int slot,
        Vector2 position)
    {
        this._emitterSlots[slot].Position = position;
    }

    internal float GetEmitterScale(
        int slot)
    {
        return this._emitterSlots[slot].Scale;
    }

    internal void SetEmitterScale(
        int slot,
        float scale)
    {
        this._emitterSlots[slot].Scale = scale;
    }

    internal float GetEmitterTime(
        int slot)
    {
        return this._emitterSlots[slot].Time;
    }

    // Stopped emitters stay in the running list until the next update (or clear) frees them,
    // so a slot isn't reused while the update is walking the list
    internal void StopEmitter(
        int slot)
    {
        this._emitterSlots[slot].IsActive = false;
    }

    // Takes the running emitter at index out of the running list, keeping the rest in order,
    // and frees its slot
    private void ReleaseEmitter(
        int index)
    {
        int slot = this._runningEmitters[index];
        this._emitterSlots[slot].IsActive = false;

        this._runningEmitterCount -= 1;
        Array.Copy(this._runningEmitters, index + 1, this._runningEmitters, index, this._runningEmitterCount - index);

        this._freeEmitters[this._freeEmitterCount++] = slot;
    }

    // Doubles the emitter pool; it keeps its size from then on
    private void GrowEmitters()
    {
        int slots = Math.Max(4, this._emitterSlots.Length * 2);
        int layers = this._layers.Length;
        Array.Resize(ref this._emitterSlots, slots);
        Array.Resize(ref this._emitterBudgets, slots * layers);
        Array.Resize(ref this._emitterNextBursts, slots * layers);
        Array.Resize(ref this._emitterFinished, slots * layers);
        Array.Resize(ref this._runningEmitters, slots);
        Array.Resize(ref this._freeEmitters, slots);
    }

    private void Emit(
        int slot,
        float elapsedSeconds)
    {
        ref var state = ref this._emitterSlots[slot];
        var from = state.HasPreviousPosition ? state.PreviousPosition : state.Position;
        var to = state.Position;
        float startTime = state.Time;
        float endTime = startTime + elapsedSeconds;

        int first = slot * this._layers.Length;
        bool isFinished = true;
        for (int i = 0; i < this._layers.Length; i++)
        {
            ref bool finished = ref this._emitterFinished[first + i];
            if (!finished)
            {
                this._layers[i].Emit(
                    state.Scale,
                    ref this._emitterBudgets[first + i],
                    ref this._emitterNextBursts[first + i],
                    ref finished,
                    from,
                    to,
                    startTime,
                    endTime,
                    this._random);
                isFinished &= finished;
            }
        }

        state.Time = endTime;
        state.PreviousPosition = to;
        state.HasPreviousPosition = true;

        if (isFinished)
        {
            state.IsActive = false;
        }
    }

    private void CompleteEmptyWaiters()
    {
        var waiters = this._emptyWaiters;
        if (waiters is null)
        {
            return;
        }

        this._emptyWaiters = null;
        foreach (var waiter in waiters)
        {
            waiter.TrySetResult();
        }
    }

    // One pooled emitter's state; Generation counts its uses, so handles to an earlier use
    // see it as ended
    private struct EmitterSlot
    {
        public Vector2 Position;
        public Vector2 PreviousPosition;
        public bool HasPreviousPosition;
        public float Scale;
        public float Time;
        public int Generation;
        public bool IsActive;
    }

    // How a particle moves along one axis over a time: its exact path under constant
    // acceleration and drag, so moving by two steps is the same as moving by one of their
    // total, and the motion is the same at any frame rate.
    private readonly struct AxisMotion
    {
        private readonly float _time;
        private readonly float _gravity;
        private readonly float _terminalVelocity;
        private readonly float _decay;
        private readonly float _travel;
        private readonly bool _hasDrag;

        public AxisMotion(
            float gravity,
            float drag,
            float time)
        {
            this._time = time;
            this._gravity = gravity;
            this._hasDrag = drag > 0;
            if (this._hasDrag)
            {
                // The velocity decays toward gravity / drag: v(t) = vt + (v0 - vt)e^(-kt)
                this._terminalVelocity = gravity / drag;
                this._decay = MathF.Exp(-drag * time);
                this._travel = (1 - this._decay) / drag;
            }
            else
            {
                this._terminalVelocity = 0;
                this._decay = 1;
                this._travel = time;
            }
        }

        public void Apply(
            ref float position,
            ref float velocity)
        {
            if (this._hasDrag)
            {
                float excess = velocity - this._terminalVelocity;
                position += (this._terminalVelocity * this._time) + (excess * this._travel);
                velocity = this._terminalVelocity + (excess * this._decay);
            }
            else
            {
                position += (velocity * this._time) + (0.5f * this._gravity * this._time * this._time);
                velocity += this._gravity * this._time;
            }
        }
    }

    // One effect's particles: a ring of them, oldest first, so they draw in the order they
    // were emitted, and a buffer of their draw states of the same size. Both are rented from
    // the shared pools while there are particles (and may be longer than the capacity).
    private sealed class Layer
    {
        private bool _isReserved;
        private readonly ParticleEffect _effect;
        private readonly int _capacity;
        private Particle[]? _particles;
        private ParticleDrawState[]? _drawStates;
        private readonly ParticleBurst[] _bursts;
        private readonly IParticleBehavior? _behavior;
        private readonly bool _hasMotion;
        private int _start;
        private int _count;

        public Layer(
            ParticleEffect effect)
        {
            ArgumentNullException.ThrowIfNull(effect);

            if (effect.Renderer is null)
            {
                throw new ArgumentException("A particle effect needs a renderer.", nameof(effect));
            }

            if (effect.Capacity <= 0)
            {
                throw new ArgumentException("A particle effect's capacity must be above 0.", nameof(effect));
            }

            this._effect = effect;
            this._capacity = effect.Capacity;
            this._behavior = effect.Behavior;

            // Bursts in time order, so an emitter fires them by walking forward
            this._bursts = (effect.Bursts.Length == 0) ? [] : (ParticleBurst[])effect.Bursts.Clone();
            if (this._bursts.Length > 1)
            {
                Array.Sort(this._bursts, static (a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            }

            this._hasMotion = (effect.Speed != 0) || (effect.Gravity != Vector2.Zero);
        }

        public ParticleEffect Effect => this._effect;

        public int Count => this._count;

        public void Clear()
        {
            this._start = 0;
            this._count = 0;
            this.ReturnBuffers();
        }

        public void Update(
            float elapsedSeconds,
            Rectangle? bounds)
        {
            if (this._count == 0)
            {
                this.ReturnBuffers();
                return;
            }

            var effect = this._effect;
            var motionX = new AxisMotion(effect.Gravity.X, effect.Drag.X, elapsedSeconds);
            var motionY = new AxisMotion(effect.Gravity.Y, effect.Drag.Y, elapsedSeconds);
            var particles = this._particles!;
            int capacity = this._capacity;

            // Keep the live particles, in order, closing the gaps the expired ones leave
            int kept = 0;
            for (int i = 0; i < this._count; i++)
            {
                ref var particle = ref particles[(this._start + i) % capacity];

                particle.AgeSeconds += elapsedSeconds;
                if (particle.AgeSeconds >= particle.LifetimeSeconds)
                {
                    continue;
                }

                if (this._hasMotion || (particle.Velocity != Vector2.Zero))
                {
                    motionX.Apply(ref particle.Position.X, ref particle.Velocity.X);
                    motionY.Apply(ref particle.Position.Y, ref particle.Velocity.Y);
                }

                if ((this._behavior is not null) && !this._behavior.Update(ref particle, elapsedSeconds))
                {
                    continue;
                }

                if (bounds.HasValue && !bounds.Value.Contains(particle.Position))
                {
                    continue;
                }

                if (kept != i)
                {
                    particles[(this._start + kept) % capacity] = particle;
                }

                kept += 1;
            }

            this._count = kept;
            if (kept == 0)
            {
                this.ReturnBuffers();
            }
        }

        // Emits an emitter's particles for this effect over a frame, from its start time to
        // its end time (in the emitter's run), as it moved from one position to another
        public void Emit(
            float emitterScale,
            ref float emitterBudget,
            ref int nextBurst,
            ref bool isFinished,
            Vector2 from,
            Vector2 to,
            float startTime,
            float endTime,
            ParticleRandom random)
        {
            var effect = this._effect;
            float frameTime = endTime - startTime;

            // The part of the frame the effect's run covers
            float runStart = effect.DelaySeconds;
            float runEnd = (effect.DurationSeconds is float duration) ? runStart + duration : float.PositiveInfinity;
            float emitStart = Math.Max(startTime, runStart);
            float emitEnd = Math.Min(endTime, runEnd);

            if (emitEnd > emitStart)
            {
                // Continuous emission, by time and by distance, owed as a fraction between frames
                float rate = effect.Rate;
                if ((rate > 0) && !effect.RateOverDuration.IsConstant && (effect.DurationSeconds is > 0))
                {
                    float middle = ((emitStart + emitEnd) * 0.5f) - runStart;
                    rate *= effect.RateOverDuration.Evaluate(middle / effect.DurationSeconds.GetValueOrDefault());
                }
                else
                {
                    rate *= effect.RateOverDuration.Evaluate(0);
                }

                float budget = emitterBudget + (rate * (emitEnd - emitStart));
                if (effect.RatePerDistance > 0)
                {
                    budget += effect.RatePerDistance * Vector2.Distance(from, to) * ((emitEnd - emitStart) / frameTime);
                }

                int count = (int)budget;
                emitterBudget = budget - count;

                for (int i = 0; i < count; i++)
                {
                    // Spread evenly over the frame, along the path
                    float time = emitStart + ((emitEnd - emitStart) * ((i + 0.5f) / count));
                    var position = Vector2.Lerp(from, to, (time - startTime) / frameTime);
                    this.Spawn(position, emitterScale, default, endTime - time, random);
                }

                // Bursts due this frame
                var bursts = this._bursts;
                while ((nextBurst < bursts.Length) && (runStart + bursts[nextBurst].TimeSeconds < emitEnd))
                {
                    var burst = bursts[nextBurst];
                    float time = Math.Max(runStart + burst.TimeSeconds, startTime);
                    var position = Vector2.Lerp(from, to, (time - startTime) / frameTime);
                    for (int i = 0; i < burst.Count; i++)
                    {
                        this.Spawn(position, emitterScale, default, endTime - time, random);
                    }

                    nextBurst += 1;
                }
            }

            if (endTime >= runEnd)
            {
                isFinished = true;
            }
        }

        // Starts a particle with the effect's values, already aged by age (when it was emitted
        // part way through a frame)
        public void Spawn(
            Vector2 position,
            float shapeScale,
            in ParticleOverrides overrides,
            float age,
            ParticleRandom random)
        {
            if (!this.TryAdd(out int index))
            {
                return;
            }

            ref var particle = ref this._particles![index];
            this.Initialize(ref particle, position, shapeScale, in overrides, random);
            this.Start(ref particle, age);
        }

        public void Spawn(
            in ParticleSpawn spawn,
            ParticleRandom random)
        {
            if (!this.TryAdd(out int index))
            {
                return;
            }

            ref var particle = ref this._particles![index];
            var overrides = new ParticleOverrides { Shape = EmitShape.Point, Variant = spawn.Variant };
            this.Initialize(ref particle, spawn.Position, 1, in overrides, random);

            particle.Velocity = spawn.Velocity ?? particle.Velocity;
            particle.LifetimeSeconds = spawn.LifetimeSeconds ?? particle.LifetimeSeconds;
            particle.Scale = spawn.Scale ?? particle.Scale;
            particle.Rotation = (spawn.RotationDegrees is float rotation) ? Angle.FromDegrees(rotation) : particle.Rotation;
            particle.Opacity = spawn.Opacity ?? particle.Opacity;
            if (spawn.Color is Color color)
            {
                particle.Color = color;
                particle.EndColor = (this._effect.EndColor is null) ? color : particle.EndColor;
            }

            this.Start(ref particle, 0);
        }

        // Works out each live particle's final drawn state, oldest first, leaving out the
        // fully transparent ones
        public ReadOnlySpan<ParticleDrawState> GetDrawStates(
            Color tint)
        {
            if (this._count == 0)
            {
                return default;
            }

            var effect = this._effect;
            bool isAdditive = effect.Blend == ParticleBlend.Additive;
            float flipMinimum = effect.Flip?.MinWidth ?? 0;
            var scaleOverLife = effect.ScaleOverLife;
            var opacityOverLife = effect.OpacityOverLife;
            var colorEase = effect.ColorEase;

            var particles = this._particles!;
            var drawStates = this._drawStates!;
            int capacity = this._capacity;
            int drawn = 0;
            for (int i = 0; i < this._count; i++)
            {
                ref readonly var particle = ref particles[(this._start + i) % capacity];
                float life = particle.NormalizedAge;

                float alpha = particle.Opacity * opacityOverLife.Evaluate(life);
                if (alpha <= 0)
                {
                    continue;
                }

                var position = particle.Position;
                if (particle.SwayAmplitude != 0)
                {
                    // Starts from no offset, so the particle doesn't jump when it appears
                    position.X += particle.SwayAmplitude * (MathF.Sin(particle.SwayPhase + (particle.SwayFrequency * particle.AgeSeconds)) - MathF.Sin(particle.SwayPhase));
                }

                var scale = particle.Scale * scaleOverLife.Evaluate(life);
                if (particle.FlipFrequency != 0)
                {
                    scale.X *= Math.Max(flipMinimum, MathF.Abs(MathF.Cos(particle.FlipPhase + (particle.FlipFrequency * particle.AgeSeconds))));
                }

                float rotation = (particle.Rotation + (particle.Spin * particle.AgeSeconds)).Radians;
                if (effect.AlignToVelocity)
                {
                    rotation += MathF.Atan2(particle.Velocity.Y, particle.Velocity.X);
                }

                var color = particle.Color;
                if (particle.EndColor != particle.Color)
                {
                    color = Color.Lerp(particle.Color, particle.EndColor, (colorEase is null) ? life : colorEase.Evaluate(life));
                }

                color = color * alpha * tint;
                if ((color.A <= 0) && (color.R <= 0) && (color.G <= 0) && (color.B <= 0))
                {
                    // Tinted to nothing
                    continue;
                }

                if (isAdditive)
                {
                    // With premultiplied alpha, no alpha means the color adds to what's behind
                    color = Color.FromPremultiplied(color.R, color.G, color.B, 0f);
                }

                drawStates[drawn++] = new ParticleDrawState(
                    position,
                    scale,
                    Angle.FromRadians(rotation),
                    color,
                    particle.Velocity,
                    particle.AgeSeconds,
                    life,
                    particle.Variant,
                    particle.Custom);
            }

            return new ReadOnlySpan<ParticleDrawState>(drawStates, 0, drawn);
        }

        public void Reserve()
        {
            this._isReserved = true;
            if (this._particles is null)
            {
                this._particles = ArrayPool<Particle>.Shared.Rent(this._capacity);
                this._drawStates = ArrayPool<ParticleDrawState>.Shared.Rent(this._capacity);
                this._start = 0;
            }
        }

        public void ReleaseReserved()
        {
            this._isReserved = false;
            if (this._count == 0)
            {
                this.ReturnBuffers();
            }
        }

        // Makes room for a particle, as the effect's overflow says
        private bool TryAdd(
            out int index)
        {
            int capacity = this._capacity;
            if (this._particles is null)
            {
                this._particles = ArrayPool<Particle>.Shared.Rent(capacity);
                this._drawStates = ArrayPool<ParticleDrawState>.Shared.Rent(capacity);
                this._start = 0;
            }

            if (this._count == capacity)
            {
                if (this._effect.Overflow == ParticleOverflow.DropNew)
                {
                    index = -1;
                    return false;
                }

                // The oldest makes way
                this._start = (this._start + 1) % capacity;
                this._count -= 1;
            }

            index = (this._start + this._count) % capacity;
            this._count += 1;
            return true;
        }

        // Removes the particle added last
        private void RemoveLast()
        {
            this._count -= 1;
            if (this._count == 0)
            {
                this.ReturnBuffers();
            }
        }

        // Gives the buffers back to the pools once there are no particles. Particles hold no
        // references, so the buffers needn't be cleared.
        private void ReturnBuffers()
        {
            if ((this._particles is null) || this._isReserved)
            {
                return;
            }

            ArrayPool<Particle>.Shared.Return(this._particles);
            ArrayPool<ParticleDrawState>.Shared.Return(this._drawStates!);
            this._particles = null;
            this._drawStates = null;
            this._start = 0;
        }

        private void Initialize(
            ref Particle particle,
            Vector2 position,
            float shapeScale,
            in ParticleOverrides overrides,
            ParticleRandom random)
        {
            var effect = this._effect;

            var shape = overrides.Shape ?? effect.Shape;
            particle.Origin = position;
            particle.Position = position + shape.Sample(random, effect.ShapeScalesWithEmitter ? shapeScale : 1);

            float speed = effect.Speed.Sample(random);
            if (speed != 0)
            {
                float direction = (overrides.DirectionDegrees ?? effect.DirectionDegrees).Sample(random) * DegreesToRadians;
                (float sin, float cos) = MathF.SinCos(direction);
                particle.Velocity = new Vector2(cos * speed, sin * speed);
            }
            else
            {
                particle.Velocity = Vector2.Zero;
            }

            particle.AgeSeconds = 0;
            particle.LifetimeSeconds = effect.LifetimeSeconds.Sample(random);

            float scale = effect.Scale.Sample(random);
            particle.Scale = new Vector2(scale * effect.ScaleX.Sample(random), scale * effect.ScaleY.Sample(random));
            particle.Rotation = Angle.FromDegrees(effect.RotationDegrees.Sample(random));
            particle.Spin = Angle.FromDegrees(effect.SpinDegrees.Sample(random));

            particle.Color = (overrides.Color ?? effect.Color).Sample(random);
            particle.EndColor = (effect.EndColor is ParticleColor endColor) ? endColor.Sample(random) : particle.Color;
            particle.Opacity = effect.Opacity.Sample(random);

            particle.Variant = overrides.Variant ?? random.NextUInt32();

            if (effect.Sway is ParticleSway sway)
            {
                particle.SwayAmplitude = sway.Amplitude.Sample(random);
                particle.SwayFrequency = sway.Frequency.Sample(random) * MathF.Tau;
                particle.SwayPhase = random.NextSingle() * MathF.Tau;
            }
            else
            {
                particle.SwayAmplitude = 0;
                particle.SwayFrequency = 0;
                particle.SwayPhase = 0;
            }

            if (effect.Flip is ParticleFlip flip)
            {
                particle.FlipFrequency = flip.Frequency.Sample(random) * MathF.Tau;
                particle.FlipPhase = random.NextSingle() * MathF.Tau;
            }
            else
            {
                particle.FlipFrequency = 0;
                particle.FlipPhase = 0;
            }

            particle.Custom = default;
        }

        // Runs the behavior's start, then ages the particle by the part of the frame since it
        // was emitted; removes it if that's past its life
        private void Start(
            ref Particle particle,
            float age)
        {
            this._behavior?.Start(ref particle);

            if (age <= 0)
            {
                return;
            }

            particle.AgeSeconds = age;
            if (particle.AgeSeconds >= particle.LifetimeSeconds)
            {
                this.RemoveLast();
                return;
            }

            var effect = this._effect;
            if (this._hasMotion || (particle.Velocity != Vector2.Zero))
            {
                new AxisMotion(effect.Gravity.X, effect.Drag.X, age).Apply(ref particle.Position.X, ref particle.Velocity.X);
                new AxisMotion(effect.Gravity.Y, effect.Drag.Y, age).Apply(ref particle.Position.Y, ref particle.Velocity.Y);
            }

            if ((this._behavior is not null) && !this._behavior.Update(ref particle, age))
            {
                this.RemoveLast();
            }
        }
    }
}

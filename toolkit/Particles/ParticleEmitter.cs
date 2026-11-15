// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A running emission point in a <see cref="ParticleSystem"/>, from
/// <see cref="ParticleSystem.CreateEmitter"/>: it emits its system's effects continuously
/// until it's stopped or their durations end.
/// </summary>
/// <remarks>
/// A handle to one of the system's pooled emitter slots, so creating emitters allocates
/// nothing once the pool has grown to the most that run at once. A handle to an emitter that
/// has ended stays ended, even after its slot is reused: <see cref="IsActive"/> is false,
/// setting <see cref="Position"/> or <see cref="Scale"/> does nothing, and
/// <see cref="Stop"/> does nothing.
/// </remarks>
public readonly struct ParticleEmitter : IEquatable<ParticleEmitter>
{
    private readonly ParticleSystem? _system;
    private readonly int _slot;
    private readonly int _generation;

    internal ParticleEmitter(
        ParticleSystem system,
        int slot,
        int generation)
    {
        this._system = system;
        this._slot = slot;
        this._generation = generation;
    }

    /// <summary>
    /// Where it emits, in its system's space. Set it before each
    /// <see cref="ParticleSystem.Update"/> to move it; the particles of a frame spread along
    /// the path between.
    /// </summary>
    public Vector2 Position
    {
        get => this.IsActive ? this._system!.GetEmitterPosition(this._slot) : default;
        set
        {
            if (this.IsActive)
            {
                this._system!.SetEmitterPosition(this._slot, value);
            }
        }
    }

    /// <summary>
    /// Moves it without emitting along the way, as <see cref="Position"/> would for an effect
    /// that emits by distance: a respawn, or a jump across the screen, leaves no streak.
    /// </summary>
    public void Teleport(
        Vector2 position)
    {
        if (this.IsActive)
        {
            this._system!.TeleportEmitter(this._slot, position);
        }
    }

    /// <summary>
    /// Scales its effects' shapes, for the effects with
    /// <see cref="ParticleEffect.ShapeScalesWithEmitter"/>.
    /// </summary>
    public float Scale
    {
        get => this.IsActive ? this._system!.GetEmitterScale(this._slot) : 0;
        set
        {
            if (this.IsActive)
            {
                this._system!.SetEmitterScale(this._slot, value);
            }
        }
    }

    /// <summary>
    /// How long it has run, in seconds; 0 once it has ended.
    /// </summary>
    public float TimeSeconds => this.IsActive ? this._system!.GetEmitterTime(this._slot) : 0;

    /// <summary>
    /// Whether it's still emitting.
    /// </summary>
    public bool IsActive => (this._system is not null) && this._system.IsEmitterActive(this._slot, this._generation);

    /// <summary>Whether the two are the same.</summary>
    public static bool operator ==(
        ParticleEmitter left,
        ParticleEmitter right)
    {
        return left.Equals(right);
    }

    /// <summary>Whether the two differ.</summary>
    public static bool operator !=(
        ParticleEmitter left,
        ParticleEmitter right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Stops emitting; its particles live out their lives.
    /// </summary>
    public void Stop()
    {
        if (this.IsActive)
        {
            this._system!.StopEmitter(this._slot);
        }
    }

    /// <inheritdoc/>
    public bool Equals(
        ParticleEmitter other)
    {
        return ReferenceEquals(this._system, other._system) && (this._slot == other._slot) && (this._generation == other._generation);
    }

    /// <inheritdoc/>
    public override bool Equals(
        object? obj)
    {
        return (obj is ParticleEmitter other) && this.Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this._system, this._slot, this._generation);
    }
}

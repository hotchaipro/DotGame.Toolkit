// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Particles;

/// <summary>
/// The small, fast generator a particle system samples its effects with (xoshiro128**).
/// Internal, so no generator appears in the public API.
/// </summary>
internal sealed class ParticleRandom
{
    private uint _s0;
    private uint _s1;
    private uint _s2;
    private uint _s3;

    /// <summary>
    /// A generator with a random seed.
    /// </summary>
    public ParticleRandom()
        : this(Random.Shared.Next())
    {
    }

    /// <summary>
    /// A generator that produces the same sequence for the same seed, for tests.
    /// </summary>
    public ParticleRandom(
        int seed)
    {
        // SplitMix32 spreads the seed over the state, which must not be all zero
        uint x = (uint)seed;
        this._s0 = SplitMix(ref x);
        this._s1 = SplitMix(ref x);
        this._s2 = SplitMix(ref x);
        this._s3 = SplitMix(ref x);
        if ((this._s0 | this._s1 | this._s2 | this._s3) == 0)
        {
            this._s0 = 1;
        }
    }

    /// <summary>
    /// A random 32-bit value.
    /// </summary>
    public uint NextUInt32()
    {
        uint result = RotateLeft(this._s1 * 5, 7) * 9;
        uint t = this._s1 << 9;

        this._s2 ^= this._s0;
        this._s3 ^= this._s1;
        this._s1 ^= this._s2;
        this._s0 ^= this._s3;
        this._s2 ^= t;
        this._s3 = RotateLeft(this._s3, 11);

        return result;
    }

    /// <summary>
    /// A random value from 0 (included) to 1 (excluded).
    /// </summary>
    public float NextSingle()
    {
        // The top 24 bits, which a float holds exactly
        return (this.NextUInt32() >> 8) * (1.0f / (1 << 24));
    }

    /// <summary>
    /// A random value from 0 (included) to <paramref name="maxValue"/> (excluded).
    /// </summary>
    public int Next(
        int maxValue)
    {
        return (int)(((ulong)this.NextUInt32() * (uint)maxValue) >> 32);
    }

    private static uint RotateLeft(
        uint value,
        int count)
    {
        return (value << count) | (value >> (32 - count));
    }

    private static uint SplitMix(
        ref uint x)
    {
        x += 0x9e3779b9;
        uint z = x;
        z = (z ^ (z >> 16)) * 0x85ebca6b;
        z = (z ^ (z >> 13)) * 0xc2b2ae35;
        return z ^ (z >> 16);
    }
}

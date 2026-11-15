// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Makes a <see cref="Tween{T}"/>, which moves a value from one place to another over time. For a curve
/// alone, with progress of your own, use <see cref="Easing"/>.
/// </summary>
public static class Tween
{
    /// <summary>A number moving over time.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="duration">How long it takes.</param>
    /// <param name="easing">The curve it follows, or <c>null</c> for a straight line.</param>
    /// <param name="delay">How long it waits before it starts.</param>
    /// <param name="playback">How it plays: once, by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration or delay is negative.</exception>
    public static Tween<float> Create(
        float from,
        float to,
        TimeSpan duration,
        Easing? easing = null,
        TimeSpan delay = default,
        PlaybackMode playback = PlaybackMode.Once)
    {
        return new Tween<float>(from, to, duration, static (a, b, amount) => a + ((b - a) * amount), easing, delay, playback);
    }

    /// <inheritdoc cref="Create(float, float, TimeSpan, Easing, TimeSpan, PlaybackMode)" path="/param"/>
    /// <summary>A position moving over time.</summary>
    public static Tween<Vector2> Create(
        Vector2 from,
        Vector2 to,
        TimeSpan duration,
        Easing? easing = null,
        TimeSpan delay = default,
        PlaybackMode playback = PlaybackMode.Once)
    {
        return new Tween<Vector2>(from, to, duration, static (a, b, amount) => a + ((b - a) * amount), easing, delay, playback);
    }

    /// <inheritdoc cref="Create(float, float, TimeSpan, Easing, TimeSpan, PlaybackMode)" path="/param"/>
    /// <summary>A color changing over time. A curve that overshoots stops at the end colors, as colors beyond them have no meaning.</summary>
    public static Tween<Color> Create(
        Color from,
        Color to,
        TimeSpan duration,
        Easing? easing = null,
        TimeSpan delay = default,
        PlaybackMode playback = PlaybackMode.Once)
    {
        return new Tween<Color>(from, to, duration, static (a, b, amount) => Color.Lerp(a, b, amount), easing, delay, playback);
    }

    /// <inheritdoc cref="Create(float, float, TimeSpan, Easing, TimeSpan, PlaybackMode)" path="/param"/>
    /// <summary>An angle turning over time, the shortest way round.</summary>
    public static Tween<Angle> Create(
        Angle from,
        Angle to,
        TimeSpan duration,
        Easing? easing = null,
        TimeSpan delay = default,
        PlaybackMode playback = PlaybackMode.Once)
    {
        return new Tween<Angle>(from, to, duration, static (a, b, amount) => Angle.Lerp(a, b, amount), easing, delay, playback);
    }

    /// <summary>A value of your own moving over time.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="duration">How long it takes.</param>
    /// <param name="lerp">Finds the value part of the way from one to the other, by an amount that's outside 0 to 1 for a curve that overshoots.</param>
    /// <param name="easing">The curve it follows, or <c>null</c> for a straight line.</param>
    /// <param name="delay">How long it waits before it starts.</param>
    /// <param name="playback">How it plays: once, by default.</param>
    /// <exception cref="ArgumentOutOfRangeException">The duration or delay is negative.</exception>
    public static Tween<T> Create<T>(
        T from,
        T to,
        TimeSpan duration,
        Func<T, T, float, T> lerp,
        Easing? easing = null,
        TimeSpan delay = default,
        PlaybackMode playback = PlaybackMode.Once)
    {
        ArgumentNullException.ThrowIfNull(lerp);

        return new Tween<T>(from, to, duration, lerp, easing, delay, playback);
    }
}

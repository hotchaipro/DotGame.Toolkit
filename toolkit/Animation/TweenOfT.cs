// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// A value moving from one place to another over time, along an <see cref="Easing"/>. Make one with
/// <see cref="Tween.Create(float, float, TimeSpan, Easing, TimeSpan, PlaybackMode)"/>, call <see cref="Update"/>
/// each frame with the time that's passed, and read <see cref="Value"/>.
/// </summary>
/// <remarks>
/// The caller drives it, so it follows whatever time it's given, such as slow motion or a stopped
/// clock, and nothing keeps a list of tweens. It allocates nothing as it plays or restarts.
/// </remarks>
/// <typeparam name="T">The type of the value.</typeparam>
public sealed class Tween<T>
{
    private readonly Func<T, T, float, T> _lerp;
    private readonly Easing _easing;
    private T _from;
    private T _to;
    private TimeSpan _elapsed;
    private bool _isEnded;
    private TaskCompletionSource? _finished;

    internal Tween(
        T from,
        T to,
        TimeSpan duration,
        Func<T, T, float, T> lerp,
        Easing? easing,
        TimeSpan delay,
        PlaybackMode playback)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        this._from = from;
        this._to = to;
        this._lerp = lerp;
        this._easing = easing ?? Easing.Linear;
        this.Duration = duration;
        this.Delay = delay;
        this.Playback = playback;
        this.Value = from;
    }

    /// <summary>Where the value has got to, as of the last <see cref="Update"/>.</summary>
    public T Value { get; private set; }

    /// <summary>How far it has got from its start to its end, from 0 to 1, before its easing.</summary>
    public float Progress { get; private set; }

    /// <summary>How long it takes from its start to its end.</summary>
    public TimeSpan Duration { get; }

    /// <summary>How long it waits before it starts.</summary>
    public TimeSpan Delay { get; }

    /// <summary>How it plays.</summary>
    public PlaybackMode Playback { get; }

    /// <summary>Whether it has reached its end and stopped. One that loops only does with <see cref="Finish"/>.</summary>
    public bool IsFinished => this._isEnded;

    /// <summary>Moves it on by the time that's passed, such as the frame's <c>Time.Delta</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The time is negative.</exception>
    public void Update(
        TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        if (this._isEnded)
        {
            return;
        }

        this._elapsed += elapsed;

        var active = this._elapsed - this.Delay;
        if (active < TimeSpan.Zero)
        {
            return;
        }

        if ((this.Playback == PlaybackMode.Once) && (active >= this.Duration))
        {
            this.Finish();
            return;
        }

        if (this.Duration <= TimeSpan.Zero)
        {
            this.MoveTo(1f);
            return;
        }

        float cycles = (float)(active.Ticks / (double)this.Duration.Ticks);
        this.MoveTo(this.Playback switch
        {
            PlaybackMode.Loop => cycles - MathF.Floor(cycles),
            PlaybackMode.PingPong => 1f - MathF.Abs(1f - (cycles - (2f * MathF.Floor(cycles / 2f)))),
            _ => cycles,
        });
    }

    /// <summary>Goes back to its start, delay included.</summary>
    public void Restart()
    {
        this._elapsed = TimeSpan.Zero;
        this._isEnded = false;
        this.Progress = 0f;
        this.Value = this._from;

        if (this._finished?.Task.IsCompleted == true)
        {
            this._finished = null;
        }
    }

    /// <summary>Goes back to the start of a new journey, so a tween can be used again without allocating.</summary>
    public void Restart(
        T from,
        T to)
    {
        this._from = from;
        this._to = to;
        this.Restart();
    }

    /// <summary>Jumps to its end and stops, whichever way it plays.</summary>
    public void Finish()
    {
        this._isEnded = true;
        this.Progress = 1f;

        // NOTE: Not through its curve or a custom lerp, so it lands exactly on the end
        this.Value = this._to;
        this._finished?.TrySetResult();
    }

    /// <summary>
    /// Completes when it has finished. It needs <see cref="Update"/> to keep being called, and one that
    /// loops needs <see cref="Finish"/>.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was canceled.</exception>
    public Task WhenFinishedAsync(
        CancellationToken cancellationToken = default)
    {
        if (this._isEnded)
        {
            return Task.CompletedTask;
        }

        this._finished ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return this._finished.Task.WaitAsync(cancellationToken);
    }

    private void MoveTo(
        float progress)
    {
        this.Progress = progress;
        this.Value = this._lerp(this._from, this._to, this._easing.Evaluate(progress));
    }
}

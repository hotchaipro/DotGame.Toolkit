// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Framework.Audio;
using DotGame.Framework.Content;
using DotGame.Framework.Time;

namespace DotGame.Toolkit.Audio;

/// <summary>
/// A pool that plays a single <see cref="SoundSegment"/> cue with a
/// fixed cap on the number of concurrent active instances.
/// </summary>
/// <remarks>
/// <para>
/// Instances overlap, up to <see cref="VoiceLimit"/>
/// (<see cref="DefaultVoiceLimit"/> unless the pool is made with another):
/// a play takes a voice that has finished, or creates one while the
/// pool has fewer than its limit, so a cue that
/// never overlaps keeps one voice. The cost of engaging the platform
/// is paid once per voice, when it's created; replays allocate nothing.
/// </para>
/// <para>
/// When every voice is playing at the instance limit, the oldest is
/// stolen to make room for the new play.
/// </para>
/// <para>
/// An instance can play at less than full volume, and the playing
/// instances can fade out before they stop: a voice gets its own
/// <see cref="VolumeControl"/> the first time either is needed.
/// </para>
/// <para>
/// The pool's <see cref="VolumeControl"/> puts every instance under a shared control, so a game's settings can turn
/// all its sound effects up or down.
/// </para>
/// </remarks>
public sealed class SoundPool : IDisposable
{
    /// <summary>
    /// The instances a cue plays at once unless the pool is made with another limit: rapid
    /// replays (collecting items, taps) overlap instead of cutting each
    /// other off.
    /// </summary>
    public const int DefaultVoiceLimit = 8;

    private readonly AudioService _audioService;
    private readonly SoundSegment _segment;

    // Voices are created as plays need them, up to the limit; the first
    // _voiceCount entries exist
    private readonly SoundVoice?[] _voices;
    private int _voiceCount;

    // Each voice's own volume control, created when it first plays at less than full volume
    // or fades out
    private readonly VolumeControl?[] _volumeControls;

    // The fade-out each voice is in, or 0: playing it again takes it out of the fade, so the
    // fade's stop leaves it playing
    private readonly int[] _fadeOuts;
    private int _lastFadeOut;

    // When each voice last started, to steal the oldest at the limit
    private readonly long[] _startedAt;
    private long _plays;
    private int _lastTaken = -1;

    private readonly VolumeControl? _group;

    private bool _isDisposed;

    /// <summary>Makes a pool for a sound or a part of one, with one voice ready.</summary>
    /// <param name="audioService">The audio service.</param>
    /// <param name="sound">The sound to play.</param>
    /// <param name="voiceLimit">The most copies playing at once.</param>
    /// <param name="volumeControl">The control every copy's volume goes through, on top of its own: a
    /// "sound effects" control shared by a game's pools, say. The pool doesn't own it.</param>
    public SoundPool(
        AudioService audioService,
        Sound sound,
        int voiceLimit = DefaultVoiceLimit,
        VolumeControl? volumeControl = null)
        : this(audioService, new SoundSegment(sound), voiceLimit, volumeControl)
    {
    }

    /// <summary>Makes a pool for a sound or a part of one, with one voice ready.</summary>
    /// <param name="audioService">The audio service.</param>
    /// <param name="segment">The part of a sound to play.</param>
    /// <param name="voiceLimit">The most copies playing at once.</param>
    /// <param name="volumeControl">The control every copy's volume goes through, on top of its own: a
    /// "sound effects" control shared by a game's pools, say. The pool doesn't own it.</param>
    public SoundPool(
        AudioService audioService,
        SoundSegment segment,
        int voiceLimit = DefaultVoiceLimit,
        VolumeControl? volumeControl = null)
    {
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(segment.Sound);

        if (voiceLimit < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(voiceLimit),
                "Instance limit must be one or greater.");
        }

        this._audioService = audioService;
        this._group = volumeControl;
        this._segment = segment;
        this._voices = new SoundVoice?[voiceLimit];
        this._volumeControls = new VolumeControl?[voiceLimit];
        this._fadeOuts = new int[voiceLimit];
        this._startedAt = new long[voiceLimit];

        // One voice now, so the first play costs no more than a replay
        this.CreateVoice();
    }

    /// <summary>
    /// Creates voices now, up to <paramref name="voices"/> or the limit, instead of on the first
    /// overlap of the cue, which is usually the busiest moment of the game. Call it while loading.
    /// </summary>
    public void Prepare(
        int voices)
    {
        this.ThrowIfDisposed();

        while ((this._voiceCount < voices) && (this._voiceCount < this._voices.Length))
        {
            this.CreateVoice();
        }
    }

    private int CreateVoice()
    {
        int index = this._voiceCount;
        this._voices[index] = this._audioService.CreateVoice(
            this._segment.Sound,
            this._segment.Offset,
            this._segment.Duration);
        if (this._group is not null)
        {
            this._voices[index]!.VolumeControl = this._group;
        }

        this._voiceCount = index + 1;
        return index;
    }

    // The voice for a new play: one that has finished, a new one while
    // the pool is under its limit, or else the one that started longest ago
    private int TakeVoice(
        out bool isStolen)
    {
        isStolen = false;

        // NOTE: From the voice after the last one taken: it's the likeliest to have finished, as
        // asking a voice costs a platform call
        int oldest = 0;
        int start = this._lastTaken + 1;
        for (int n = 0; n < this._voiceCount; n++)
        {
            int i = (start + n) % this._voiceCount;
            if (!this._voices[i]!.IsPlaying)
            {
                return i;
            }

            if (this._startedAt[i] < this._startedAt[oldest])
            {
                oldest = i;
            }
        }

        if (this._voiceCount < this._voices.Length)
        {
            return this.CreateVoice();
        }

        isStolen = true;
        return oldest;
    }

    /// <summary>
    /// The control every instance's volume goes through, on top of its own, given when the pool was
    /// made. With none, they're directly under <see cref="AudioService.MasterVolumeControl"/>.
    /// </summary>
    public VolumeControl? VolumeControl => this._group;

    /// <summary>
    /// The segment this pool plays.
    /// </summary>
    public SoundSegment Segment => this._segment;

    /// <summary>
    /// Fixed maximum number of concurrent active instances of the
    /// cue.
    /// </summary>
    public int VoiceLimit => this._voices.Length;

    /// <summary>Whether any instance of the cue is playing.</summary>
    public bool IsPlaying
    {
        get
        {
            for (int i = 0; i < this._voiceCount; i++)
            {
                if (this._voices[i]!.IsPlaying)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Starts a new instance of the cue at a volume from 0 (silent) to 1 (full), clamped to that: on a voice that has
    /// finished, on a new voice while the pool is under its limit, or else on the oldest playing voice.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The pool or its sound has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The volume is not a number.</exception>
    public void Play(
        float volume = 1f)
    {
        this.PlayVoice(volume);
    }

    internal SoundVoice PlayVoice(
        float volume = 1f)
    {
        this.ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(((IContent)this._segment.Sound).IsDisposed, this._segment.Sound);

        int index = this.TakeVoice(out bool isStolen);
        var voice = this._voices[index]!;
        this._lastTaken = index;

        if (isStolen)
        {
            voice.Stop();
        }

        // A voice that's been given a volume (or a fade) keeps its
        // control, so set it every time; one at full volume needs none
        this._fadeOuts[index] = 0;
        var volumeControl = this._volumeControls[index];
        if ((volumeControl is null) && (!(volume >= 1f)))
        {
            volumeControl = this.CreateVolumeControl(index);
        }

        volumeControl?.Volume = volume;

        this._startedAt[index] = ++this._plays;
        voice.Play();

        return voice;
    }

    /// <summary>
    /// Fades every playing instance out over <paramref name="duration"/> and then stops it. An
    /// instance played again before then isn't part of the fade any more.
    /// </summary>
    /// <param name="duration">How long the fade takes.</param>
    /// <param name="time">The game's clock, which says when the fade has ended: stopping game time
    /// holds the instances, silent, until it runs again.</param>
    public void FadeOutAll(
        TimeSpan duration,
        TimeService time)
    {
        ArgumentNullException.ThrowIfNull(time);

        int fadeOut = this.FadeOutVoices(duration);
        time.Delay(duration, () => this.StopFadedOut(fadeOut));
    }

    internal int FadeOutVoices(
        TimeSpan duration)
    {
        this.ThrowIfDisposed();

        int fadeOut = ++this._lastFadeOut;
        if (fadeOut == 0)
        {
            fadeOut = ++this._lastFadeOut;
        }

        for (int i = 0; i < this._voiceCount; i++)
        {
            if (!this._voices[i]!.IsPlaying)
            {
                continue;
            }

            var volumeControl = this._volumeControls[i] ?? this.CreateVolumeControl(i);
            volumeControl.FadeTo(0f, duration);
            this._fadeOuts[i] = fadeOut;
        }

        return fadeOut;
    }

    internal void StopFadedOut(
        int fadeOut)
    {
        if (this._isDisposed || (fadeOut == 0))
        {
            return;
        }

        for (int i = 0; i < this._voiceCount; i++)
        {
            if (this._fadeOuts[i] == fadeOut)
            {
                this._voices[i]!.Stop();
                this._fadeOuts[i] = 0;
            }
        }
    }

    private VolumeControl CreateVolumeControl(
        int index)
    {
        var volumeControl = this._audioService.CreateVolumeControl(this._group);
        this._volumeControls[index] = volumeControl;
        this._voices[index]!.VolumeControl = volumeControl;
        return volumeControl;
    }

    /// <summary>
    /// Stops every voice in the pool.
    /// </summary>
    public void StopAll()
    {
        if (this._isDisposed)
        {
            return;
        }

        for (int i = 0; i < this._voiceCount; i++)
        {
            this._voices[i]!.Stop();
            this._fadeOuts[i] = 0;
        }
    }

    /// <summary>Stops and releases the pool's voices and their controls. Its <see cref="VolumeControl"/> isn't the pool's own, so it stays.</summary>
    public void Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;

        for (int i = 0; i < this._voiceCount; i++)
        {
            this._voices[i]!.Dispose();
            this._volumeControls[i]?.Dispose();
            this._volumeControls[i] = null;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this._isDisposed, this);
    }
}

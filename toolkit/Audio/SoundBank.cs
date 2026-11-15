// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using DotGame.Framework.Audio;
using DotGame.Framework.Content;

namespace DotGame.Toolkit.Audio;

/// <summary>
/// A loaded sound bank: one decoded buffer (held by the base
/// <see cref="Sound"/>) plus a set of named <see cref="SoundSegment"/>
/// cues that each describe a sub-range of that buffer.
/// </summary>
/// <remarks>
/// The bank owns the base sound (and therefore its decoded audio).
/// Disposing the bank disposes the base sound; the segments become
/// invalid at that point and must not be played.
/// </remarks>
public sealed class SoundBank : IContent
{
    private readonly Sound _baseSound;
    private readonly Dictionary<string, SoundSegment> _segments;

    internal SoundBank(
        Sound baseSound,
        Dictionary<string, SoundSegment> segments,
        ContentService? content = null)
    {
        ArgumentNullException.ThrowIfNull(baseSound);
        ArgumentNullException.ThrowIfNull(segments);

        this._baseSound = baseSound;
        this._content = content;
        this._segments = segments;
    }

    private readonly ContentService? _content;

    /// <summary>Whether it has been released.</summary>
    public bool IsDisposed => ((IContent)this._baseSound).IsDisposed;

    /// <summary>
    /// Gets a named segment from the sound bank.
    /// </summary>
    /// <param name="name">The name of the segment.</param>
    /// <returns>The <see cref="SoundSegment"/> for the named cue.</returns>
    public SoundSegment GetSegment(
        string name)
    {
        if (!this._segments.TryGetValue(name, out var segment))
        {
            throw new KeyNotFoundException(
                $"Sound bank does not contain a segment named '{name}'.");
        }

        return segment;
    }

    /// <summary>
    /// Gets a named segment from the sound bank, if it has one.
    /// </summary>
    public bool TryGetSegment(
        string name,
        out SoundSegment segment)
    {
        return this._segments.TryGetValue(name, out segment);
    }

    /// <summary>
    /// Gets the names of every segment in the bank, in no particular order.
    /// </summary>
    public IReadOnlyCollection<string> SegmentNames => this._segments.Keys;

    void IDisposable.Dispose()
    {
        // Segments are non-owning views over the base sound's decoded
        // audio; they have nothing to release on their own. Disposing
        // the base sound releases the decoded audio once, which
        // invalidates every segment.
        this._segments.Clear();
        if (this._content is not null)
        {
            _ = this._content.UnloadAsync(this._baseSound);
        }
        else
        {
            ((IDisposable)this._baseSound).Dispose();
        }
    }
}

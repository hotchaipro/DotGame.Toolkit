// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Extension methods that build <see cref="SpriteAnimation"/>s from texture
/// atlases and draw <see cref="SpriteAnimationPlayer"/>s.
/// </summary>
public static class SpriteAnimationExtensions
{
    /// <summary>
    /// Creates an animation from the atlas regions named
    /// <c>&lt;name&gt;_&lt;number&gt;</c>, in numeric order.
    /// </summary>
    /// <remarks>
    /// Call it once when the atlas is loaded
    /// and keep the result.
    /// </remarks>
    /// <param name="atlas">The atlas holding the frames.</param>
    /// <param name="name">The animation's name: the frame names before their last <c>_</c>.</param>
    /// <param name="framesPerSecond">The frame rate. Must be positive and finite.</param>
    /// <param name="playback">How the animation plays through its frames.</param>
    /// <exception cref="KeyNotFoundException">The atlas has no frames for <paramref name="name"/>.</exception>
    /// <exception cref="InvalidDataException">Two frames have the same number.</exception>
    public static SpriteAnimation CreateAnimation(
        this TextureAtlas atlas,
        string name,
        float framesPerSecond,
        PlaybackMode playback = PlaybackMode.Loop)
    {
        return new SpriteAnimation(atlas.GetAnimationFrames(name), framesPerSecond, playback);
    }

    /// <summary>
    /// Gets the atlas regions named <c>&lt;name&gt;_&lt;number&gt;</c>, in
    /// numeric order, for building an animation with per-frame holds.
    /// </summary>
    /// <remarks>
    /// Frame numbers are compared as numbers, so <c>run_2</c> comes before
    /// <c>run_10</c> however they're padded. They needn't start at 0 or be
    /// contiguous. Names are compared without case, like all atlas lookups.
    /// </remarks>
    /// <param name="atlas">The atlas holding the frames.</param>
    /// <param name="name">The animation's name: the frame names before their last <c>_</c>.</param>
    /// <exception cref="KeyNotFoundException">The atlas has no frames for <paramref name="name"/>.</exception>
    /// <exception cref="InvalidDataException">Two frames have the same number.</exception>
    public static IReadOnlyList<TextureRegion> GetAnimationFrames(
        this TextureAtlas atlas,
        string name)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var numbered = new List<(int Number, string RegionName)>();

        foreach (var regionName in atlas.RegionNames)
        {
            int separator = regionName.LastIndexOf('_');

            if ((separator != name.Length)
                || (!regionName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                || (!int.TryParse(regionName.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)))
            {
                continue;
            }

            numbered.Add((number, regionName));
        }

        if (numbered.Count == 0)
        {
            throw new KeyNotFoundException(
                $"Texture atlas does not contain any frames named '{name}_<number>'.");
        }

        numbered.Sort(static (a, b) => a.Number.CompareTo(b.Number));

        var frames = new TextureRegion[numbered.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            if ((i > 0) && (numbered[i].Number == numbered[i - 1].Number))
            {
                throw new InvalidDataException(
                    $"Texture atlas regions '{numbered[i - 1].RegionName}' and '{numbered[i].RegionName}' are both frame {numbered[i].Number} of '{name}'.");
            }

            frames[i] = atlas.GetRegion(numbered[i].RegionName);
        }

        return frames;
    }

    /// <summary>
    /// Queues a player's current frame to be drawn during this batch. Does
    /// nothing when the player has no animation.
    /// </summary>
    /// <remarks>
    /// The frame is drawn trim-aware, like any atlas region, so the origin is
    /// in the frame's logical (untrimmed) coordinates.
    /// <see cref="SpriteBatch.Begin()"/> must be called before this method.
    /// </remarks>
    /// <param name="spriteBatch">The sprite batch.</param>
    /// <param name="player">The player whose frame is drawn.</param>
    /// <param name="position">Where the origin is drawn.</param>
    /// <param name="color">The color tint.</param>
    /// <param name="rotation">The rotation around the origin.</param>
    /// <param name="origin">The pivot in the frame's logical coordinates, from its top-left corner.</param>
    /// <param name="scale">
    /// The scale, or <c>null</c> for none. A negative component mirrors the frame about the
    /// origin: <c>new Vector2(-1, 1)</c> faces it the other way, in place with the origin at its center.
    /// </param>
    /// <param name="flip">
    /// Mirrors the image inside its logical (untrimmed) rectangle, which doesn't move: it flips in
    /// place whatever the origin, and a trimmed frame's pixels land mirrored where they belong.
    /// </param>
    public static void Draw(
        this SpriteBatch spriteBatch,
        SpriteAnimationPlayer player,
        Vector2 position,
        Color color,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        SpriteFlip flip = SpriteFlip.None)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(player);

        var frame = player.Frame;
        if (frame is null)
        {
            return;
        }

        spriteBatch.Draw(
            frame,
            position,
            color,
            rotation,
            origin,
            scale,
            flip);
    }
}

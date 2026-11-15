// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.Graphics;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// One frame of a <see cref="SpriteAnimation"/>.
/// </summary>
/// <param name="Region">The texture region the frame shows.</param>
/// <param name="Hold">
/// How many frames' worth of time the frame shows for, relative to the
/// animation's frame rate: 1 is one frame, 3 holds it three times as long, and
/// 0.5 shows it for half a frame. Must be positive.
/// </param>
public readonly record struct SpriteAnimationFrame(
    TextureRegion Region,
    float Hold = 1);

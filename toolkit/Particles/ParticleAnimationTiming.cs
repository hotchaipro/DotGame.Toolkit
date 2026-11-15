// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// How an <see cref="AnimatedSpriteParticleRenderer"/> times its animation.
/// </summary>
public enum ParticleAnimationTiming
{
    /// <summary>
    /// One pass of the animation over each particle's life, for example smoke that puffs out
    /// and dissipates.
    /// </summary>
    OverLife,

    /// <summary>
    /// The animation at its own <see cref="SpriteAnimation.FramesPerSecond"/> from the
    /// particle's age, for example a spinning coin. The animation's playback (loop, once,
    /// ping-pong) applies as usual.
    /// </summary>
    Loop,
}

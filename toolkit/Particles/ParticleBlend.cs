// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>How particles combine with what's behind them.</summary>
public enum ParticleBlend
{
    /// <summary>
    /// Particles cover what's behind them by their opacity.
    /// </summary>
    Alpha,

    /// <summary>
    /// Particles add their color to what's behind them, so overlapping particles glow.
    /// </summary>
    Additive,
}

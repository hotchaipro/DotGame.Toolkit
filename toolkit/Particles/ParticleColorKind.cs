// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>The kinds of <see cref="ParticleColor"/>.</summary>
internal enum ParticleColorKind
{
    /// <summary>
    /// Every particle has the same color.
    /// </summary>
    Constant,

    /// <summary>
    /// Each particle has a random color between two colors.
    /// </summary>
    Between,

    /// <summary>
    /// Each particle has one of a set of colors, picked at random.
    /// </summary>
    Palette,
}

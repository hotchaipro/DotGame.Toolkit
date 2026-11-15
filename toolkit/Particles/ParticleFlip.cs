// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A flutter, as of a flat piece of paper turning over: each particle's width narrows and
/// widens.
/// </summary>
/// <param name="Frequency">How many turns a second.</param>
/// <param name="MinWidth">The narrowest it gets, as a fraction of its width.</param>
public readonly record struct ParticleFlip(
    FloatRange Frequency,
    float MinWidth = 0.15f);

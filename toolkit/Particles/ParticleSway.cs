// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.Particles;

/// <summary>
/// A sideways sway, as of paper or leaves falling: each particle is offset along X by a sine
/// wave, starting from no offset.
/// </summary>
/// <param name="Amplitude">How far it sways each way, in pixels.</param>
/// <param name="Frequency">How many sways a second.</param>
public readonly record struct ParticleSway(
    FloatRange Amplitude,
    FloatRange Frequency);

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Graphics;
using DotGame.Toolkit.Particles;

namespace DotGame.Toolkit.Tests.Particles;

/// <summary>
/// Systems, effects, and draw states for particle tests, without a graphics device.
/// </summary>
internal static class ParticleTestHelpers
{
    /// <summary>
    /// A renderer for effects whose drawing a test doesn't look at.
    /// </summary>
    public static readonly RecordingRenderer NoRenderer = new RecordingRenderer();

    /// <summary>
    /// An effect that draws nothing, with long-lived, still particles unless the test says
    /// otherwise.
    /// </summary>
    public static ParticleEffect Effect() => new ParticleEffect
    {
        Renderer = NoRenderer,
        LifetimeSeconds = 100,
        DirectionDegrees = 0,
    };

    /// <summary>
    /// A system with a fixed seed, so a test sees the same particles every run.
    /// </summary>
    public static ParticleSystem NewSystem(
        params ParticleEffect[] effects)
    {
        return new ParticleSystem(new ParticleRandom(1234), effects);
    }

    /// <summary>
    /// Runs <paramref name="system"/> for <paramref name="seconds"/> at <paramref name="fps"/>.
    /// </summary>
    public static void Run(
        ParticleSystem system,
        double seconds,
        double fps = 60)
    {
        int frames = (int)Math.Round(seconds * fps);
        var frame = TimeSpan.FromSeconds(1 / fps);
        for (int i = 0; i < frames; i++)
        {
            system.Update(frame);
        }
    }

    /// <summary>
    /// The draw states of the system's first effect, untinted.
    /// </summary>
    public static ParticleDrawState[] Drawn(
        ParticleSystem system,
        int effect = 0)
    {
        return system.GetDrawStates(effect, Color.White).ToArray();
    }

    public static ParticleDrawState State(
        Vector2 position = default,
        Vector2? scale = null,
        float rotationRadians = 0,
        Vector2 velocity = default,
        float age = 0,
        float normalizedAge = 0,
        uint variant = 0)
    {
        return new ParticleDrawState(
            position,
            scale ?? Vector2.One,
            Angle.FromRadians(rotationRadians),
            Color.White,
            velocity,
            age,
            normalizedAge,
            variant,
            default);
    }
}

/// <summary>
/// A texture source with a size and no texture, trimmed when its frame is smaller than its
/// logical rectangle.
/// </summary>
internal sealed class TestSource(
    RectangleInt sourceRectangle,
    RectangleInt frame) : ITextureSource
{
    public TestSource(
        int width,
        int height)
        : this(new RectangleInt(0, 0, width, height), new RectangleInt(0, 0, width, height))
    {
    }

    public Texture Texture => null!;

    public RectangleInt Bounds { get; } = sourceRectangle;

    public RectangleInt PackedFrame { get; } = frame;
}

/// <summary>
/// A renderer that keeps what it's given to draw.
/// </summary>
internal sealed record RecordingRenderer : ParticleRenderer
{
    public List<ParticleDrawState[]> Calls { get; } = [];

    public override void Draw(
        SpriteBatch spriteBatch,
        ReadOnlySpan<ParticleDrawState> particles)
    {
        this.Calls.Add(particles.ToArray());
    }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// Describes something that can be drawn as a textured rectangle: either a
/// whole <see cref="Texture"/> or a sub-rectangle of one (such as a sprite
/// packed into a texture atlas).
/// </summary>
/// <remarks>
/// <para>
/// This is the contract the toolkit's rendering path speaks in. Any type
/// that can be rendered &#8212; a plain <see cref="Texture"/> (via
/// <c>AsSource()</c>), a toolkit-defined texture region, a render target,
/// etc. &#8212; implements this interface. The toolkit's
/// <c>SpriteBatch.Draw</c> extension methods accept it and translate it
/// into the framework's <see cref="Texture"/>-plus-rectangle primitive so
/// the renderer itself never needs to branch on concrete types.
/// </para>
/// <para>
/// <see cref="Bounds"/> is what the caller "sees" &#8212; the
/// sprite's untrimmed bounds in the underlying texture's coordinate space,
/// from a layout / positioning point of view. <see cref="PackedFrame"/> is the
/// sub-rectangle of <see cref="Texture"/> that actually contains the
/// sampleable pixels. For a whole texture the two are equal; for a trimmed
/// atlas region <see cref="PackedFrame"/> is inset into <see cref="Bounds"/>
/// by however many fully-transparent border pixels were removed during
/// packing.
/// </para>
/// </remarks>
public interface ITextureSource
{
    /// <summary>
    /// The underlying GPU-backed texture to sample from.
    /// </summary>
    Texture Texture { get; }

    /// <summary>
    /// The rectangle within <see cref="Texture"/> that contains the
    /// sampleable pixels (TexturePacker's <c>frame</c>).
    /// </summary>
    RectangleInt PackedFrame { get; }

    /// <summary>
    /// The logical (untrimmed) rectangle within <see cref="Texture"/> that
    /// this source occupies from the caller's point of view (TexturePacker's
    /// <c>sourceSize</c>/<c>spriteSourceSize</c>). Equal to <see cref="PackedFrame"/>
    /// for non-trimmed sources.
    /// </summary>
    RectangleInt Bounds { get; }
}

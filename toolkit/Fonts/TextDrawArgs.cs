// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// How one call draws its text: what the public <c>DrawString</c> methods take after the text
/// and its character attributes, passed by reference to the methods that do the drawing.
/// </summary>
/// <remarks>
/// NOTE: The generic drawing methods take these by reference rather than as their own
/// parameters, and that is what keeps them working on Android. Their vectors and colors don't
/// all fit in registers, and Mono's LLVM and JIT code lay out the ones that spill to the stack
/// differently: LLVM gives each float its own 8-byte slot, while the JIT packs a vector into one.
/// A generic instance can run JIT-compiled in an AOT build, so a call from LLVM code into it
/// read every later argument from the wrong slot (the effects as a wild pointer).
/// A reference is one pointer, which both lay out the same way.
/// </remarks>
internal readonly struct TextDrawArgs
{
    /// <summary>The position of <see cref="Origin"/> on the screen.</summary>
    public Vector2 Position { get; init; }

    /// <summary>The color of the text's face, where character attributes don't color it.</summary>
    public Color Color { get; init; }

    /// <summary>The amount by which to fade the text and its effects.</summary>
    public float Opacity { get; init; }

    /// <summary>The rotation of the text around <see cref="Origin"/>.</summary>
    public Angle Rotation { get; init; }

    /// <summary>The point of the text, in unscaled font units, that is drawn at <see cref="Position"/>.</summary>
    public Vector2 Origin { get; init; }

    /// <summary>The scale of the text; <see cref="Vector2.One"/> for none.</summary>
    public Vector2 Scale { get; init; }

    /// <summary>The additional spacing applied after each character, in unscaled font units.</summary>
    public float Tracking { get; init; }

    /// <summary>The text's effects, or <c>null</c> for none.</summary>
    public TextEffects? Effects { get; init; }

    /// <summary>Which layers to draw.</summary>
    public TextLayers Layers { get; init; }
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System.Numerics;
using DotGame.Framework;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Leaves every character of a string with the attributes it is drawn with.
/// </summary>
/// <remarks>
/// <para>
/// This is how the draw methods that take no attributes are expressed in terms of the ones
/// that do, rather than something a caller is expected to use.
/// </para>
/// <para>
/// All members are implemented explicitly (rather than relying on any default interface
/// implementation) to avoid boxing when this struct is used through a generic constraint.
/// </para>
/// </remarks>
internal readonly struct DefaultCharacterAttributes : ICharacterAttributes
{
    public Color? Color
    {
        get
        {
            return null;
        }
    }

    public Vector2 Translation
    {
        get
        {
            return Vector2.Zero;
        }
    }

    public Angle Rotation
    {
        get
        {
            return default;
        }
    }

    public void MoveTo(
        in CharacterCursor cursor)
    {
    }
}

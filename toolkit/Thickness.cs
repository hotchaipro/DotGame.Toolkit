// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit;

/// <summary>A distance on each of four sides: a margin or padding.</summary>
public readonly struct Thickness
{
    /// <summary>Nothing on any side.</summary>
    public static Thickness Zero => new Thickness(0);

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public Thickness(
        float all)
        : this(all, all, all, all)
    {
    }

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public Thickness(
        float leftAndRight,
        float topAndBottom)
        : this(leftAndRight, topAndBottom, leftAndRight, topAndBottom)
    {
    }

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public Thickness(
        float left,
        float top,
        float right,
        float bottom)
    {
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
    }

    /// <summary>The distance on the left.</summary>
    public float Left { get; init; }

    /// <summary>The distance at the top.</summary>
    public float Top { get; init; }

    /// <summary>The distance on the right.</summary>
    public float Right { get; init; }

    /// <summary>The distance at the bottom.</summary>
    public float Bottom { get; init; }

    /// <summary>Left and right together.</summary>
    public float Horizontal
    {
        get
        {
            return this.Left + this.Right;
        }
    }

    /// <summary>Top and bottom together.</summary>
    public float Vertical
    {
        get
        {
            return this.Top + this.Bottom;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(
        object? obj)
    {
        return (obj is Thickness other)
            && this.Left.Equals(other.Left)
            && this.Top.Equals(other.Top)
            && this.Right.Equals(other.Right)
            && this.Bottom.Equals(other.Bottom);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.Left, this.Top, this.Right, this.Bottom);
    }

    /// <summary>Whether the two are the same.</summary>
    public static bool operator ==(Thickness a, Thickness b)
    {
        return (a.Left == b.Left) && (a.Top == b.Top) && (a.Right == b.Right) && (a.Bottom == b.Bottom);
    }

    /// <summary>Whether the two differ.</summary>
    public static bool operator !=(Thickness a, Thickness b)
    {
        return (a.Left != b.Left) || (a.Top != b.Top) || (a.Right != b.Right) || (a.Bottom != b.Bottom);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return FormattableString.Invariant($"({this.Left},{this.Top},{this.Right},{this.Bottom})");
    }
}

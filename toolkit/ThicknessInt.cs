// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit;

/// <summary>A distance on each of four sides in whole pixels: a margin or padding.</summary>
public readonly struct ThicknessInt
{
    /// <summary>Nothing on any side.</summary>
    public static ThicknessInt Zero => new ThicknessInt(0);

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public ThicknessInt(
        int all)
        : this(all, all, all, all)
    {
    }

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public ThicknessInt(
        int leftAndRight,
        int topAndBottom)
        : this(leftAndRight, topAndBottom, leftAndRight, topAndBottom)
    {
    }

    /// <summary>Makes a thickness: the same all round, horizontal and vertical, or each side.</summary>
    public ThicknessInt(
        int left,
        int top,
        int right,
        int bottom)
    {
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
    }

    /// <summary>The distance on the left.</summary>
    public int Left { get; init; }

    /// <summary>The distance at the top.</summary>
    public int Top { get; init; }

    /// <summary>The distance on the right.</summary>
    public int Right { get; init; }

    /// <summary>The distance at the bottom.</summary>
    public int Bottom { get; init; }

    /// <summary>Left and right together.</summary>
    public int Horizontal => this.Left + this.Right;

    /// <summary>Top and bottom together.</summary>
    public int Vertical => this.Top + this.Bottom;

    /// <summary>The same thickness, in floats.</summary>
    public static implicit operator Thickness(ThicknessInt t)
    {
        return new Thickness(t.Left, t.Top, t.Right, t.Bottom);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is ThicknessInt other)
        {
            return this == other;
        }
        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(this.Left, this.Top, this.Right, this.Bottom);
    }

    /// <summary>Whether the two are the same.</summary>
    public static bool operator ==(ThicknessInt a, ThicknessInt b)
    {
        return (a.Left == b.Left) && (a.Top == b.Top) && (a.Right == b.Right) && (a.Bottom == b.Bottom);
    }

    /// <summary>Whether the two differ.</summary>
    public static bool operator !=(ThicknessInt a, ThicknessInt b)
    {
        return (a.Left != b.Left) || (a.Top != b.Top) || (a.Right != b.Right) || (a.Bottom != b.Bottom);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return FormattableString.Invariant($"({this.Left},{this.Top},{this.Right},{this.Bottom})");
    }
}

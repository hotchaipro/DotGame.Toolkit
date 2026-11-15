// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

namespace DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;

/// <summary>
/// MaxRects bin packer with the Best-Short-Side-Fit heuristic: each rectangle
/// goes into the free rectangle whose shorter leftover side is smallest, and the
/// free space is split into maximal (possibly overlapping) free rectangles.
/// Generally outperforms shelf packing in both density and robustness to mixed
/// aspect ratios.
/// </summary>
internal sealed class MaxRectsPacker
{
    private readonly List<Rect> _free = [];

    public MaxRectsPacker(int width, int height)
    {
        this._free.Add(new Rect(0, 0, width, height));
    }

    public bool TryInsert(int width, int height, out int x, out int y)
    {
        int bestShort = int.MaxValue;
        int bestLong = int.MaxValue;
        int bestIndex = -1;
        x = y = 0;
        for (int i = 0; i < this._free.Count; i++)
        {
            Rect free = this._free[i];
            if (free.Width < width || free.Height < height)
            {
                continue;
            }
            int leftoverX = free.Width - width;
            int leftoverY = free.Height - height;
            int shortSide = Math.Min(leftoverX, leftoverY);
            int longSide = Math.Max(leftoverX, leftoverY);
            if (shortSide < bestShort || (shortSide == bestShort && longSide < bestLong))
            {
                bestShort = shortSide;
                bestLong = longSide;
                bestIndex = i;
                x = free.X;
                y = free.Y;
            }
        }
        if (bestIndex < 0)
        {
            return false;
        }

        var used = new Rect(x, y, width, height);
        for (int i = this._free.Count - 1; i >= 0; i--)
        {
            if (this.Split(this._free[i], used))
            {
                this._free.RemoveAt(i);
            }
        }
        this.Prune();
        return true;
    }

    // Adds the parts of 'free' not covered by 'used'; returns false when they do not overlap.
    private bool Split(Rect free, Rect used)
    {
        if (used.X >= free.Right || used.Right <= free.X || used.Y >= free.Bottom || used.Bottom <= free.Y)
        {
            return false;
        }
        if (used.Y > free.Y)
        {
            this._free.Add(new Rect(free.X, free.Y, free.Width, used.Y - free.Y));
        }
        if (used.Bottom < free.Bottom)
        {
            this._free.Add(new Rect(free.X, used.Bottom, free.Width, free.Bottom - used.Bottom));
        }
        if (used.X > free.X)
        {
            this._free.Add(new Rect(free.X, free.Y, used.X - free.X, free.Height));
        }
        if (used.Right < free.Right)
        {
            this._free.Add(new Rect(used.Right, free.Y, free.Right - used.Right, free.Height));
        }
        return true;
    }

    // Removes free rectangles contained in another free rectangle.
    private void Prune()
    {
        for (int i = 0; i < this._free.Count; i++)
        {
            for (int j = i + 1; j < this._free.Count; j++)
            {
                if (this._free[j].Contains(this._free[i]))
                {
                    this._free.RemoveAt(i);
                    i--;
                    break;
                }
                if (this._free[i].Contains(this._free[j]))
                {
                    this._free.RemoveAt(j);
                    j--;
                }
            }
        }
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int Right => this.X + this.Width;

        public int Bottom => this.Y + this.Height;

        public bool Contains(Rect other) =>
            other.X >= this.X && other.Y >= this.Y && other.Right <= this.Right && other.Bottom <= this.Bottom;
    }

    public static int NextPowerOfTwo(int value) =>
        value <= 1 ? 1 : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)value);
}

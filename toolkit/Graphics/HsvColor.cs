// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

#nullable enable
using System;

using DotGame.Framework;

namespace DotGame.Toolkit.Graphics;

/// <summary>
/// A color as hue, saturation and value, for changing one of them:
/// <c>(HsvColor.FromColor(color) with { V = 1f }).ToColor()</c> is the color at full brightness.
/// </summary>
/// <param name="H">The hue, in degrees. Red is 0, green 120 and blue 240, and it wraps around.</param>
/// <param name="S">The saturation, from 0 (gray) to 1.</param>
/// <param name="V">The value, from 0 (black) to 1.</param>
/// <param name="A">The alpha, from 0 (transparent) to 1 (opaque), which isn't premultiplied.</param>
public readonly record struct HsvColor(float H, float S, float V, float A = 1f)
{
    /// <summary>Gets a color as hue, saturation and value, with its alpha.</summary>
    public static HsvColor FromColor(Color color)
    {
        // NOTE: A color holds premultiplied components, whose hue is the same but whose value isn't
        double scale = (color.A > 0f) ? 1.0 / color.A : 1.0;
        double r = color.R * scale;
        double g = color.G * scale;
        double b = color.B * scale;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0.0;
        if (delta != 0.0)
        {
            if (max == r)
            {
                h = 60.0 * (((g - b) / delta) % 6.0);
            }
            else if (max == g)
            {
                h = 60.0 * (((b - r) / delta) + 2.0);
            }
            else
            {
                h = 60.0 * (((r - g) / delta) + 4.0);
            }

            if (h < 0.0)
            {
                h += 360.0;
            }
        }

        return new HsvColor((float)h, (max != 0.0) ? (float)(delta / max) : 0f, (float)max, color.A);
    }

    /// <summary>
    /// Gets this as a <see cref="Color"/>, wrapping <see cref="H"/> around the wheel and clamping
    /// <see cref="S"/> and <see cref="V"/> to their ranges.
    /// </summary>
    public Color ToColor()
    {
        float h = this.H % 360f;
        if (h < 0f)
        {
            h += 360f;
        }

        float s = Math.Clamp(this.S, 0f, 1f);
        float v = Math.Clamp(this.V, 0f, 1f);

        if (s == 0f)
        {
            return new Color(v, v, v, this.A);
        }

        int i = (int)(h / 60f);
        float f = (h % 60f) / 60f;
        float p = v * (1f - s);
        float q = v * (1f - (s * f));
        float t = v * (1f - (s * (1f - f)));

        return i switch
        {
            0 => new Color(v, t, p, this.A),
            1 => new Color(q, v, p, this.A),
            2 => new Color(p, v, t, this.A),
            3 => new Color(p, q, v, this.A),
            4 => new Color(t, p, v, this.A),
            _ => new Color(v, p, q, this.A),
        };
    }
}

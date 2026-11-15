// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Follows a cubic Bézier curve from (0, 0) to (1, 1) with control points (x1, y1) and
/// (x2, y2), the same curve as CSS <c>cubic-bezier(x1, y1, x2, y2)</c>.
/// </summary>
internal sealed class CubicBezierEase : Easing
{
    // These values are established by empiricism with tests (tradeoff: performance VS precision)
    private const int NewtonIterations = 4;
    private const float NewtonMinSlope = 0.001f;
    private const float SubdivisionPrecision = 0.0000001f;
    private const int SubdivisionMaxIterations = 10;

    private const int SplineTableSize = 11;
    private const float SampleStepSize = 1.0f / (SplineTableSize - 1.0f);

    private readonly float _x1, _y1, _x2, _y2;
    private readonly bool _isLinear;
    private readonly float[] _sampleValues = new float[SplineTableSize];

    /// <param name="x1">The first control point's x, from 0 to 1, as in CSS.</param>
    /// <param name="x2">The second control point's x, from 0 to 1, as in CSS.</param>
    /// <summary>Makes an easing from a cubic Bezier's two control points, as CSS's <c>cubic-bezier</c> does.</summary>
    /// <param name="y1">The first control point's y: outside 0 to 1 overshoots, as in CSS.</param>
    /// <param name="y2">The second control point's y: outside 0 to 1 overshoots, as in CSS.</param>
    public CubicBezierEase(
        float x1,
        float y1,
        float x2,
        float y2)
        : base(EasingMode.In)
    {
        // The curve's x must increase with time, as CSS requires
        ArgumentOutOfRangeException.ThrowIfLessThan(x1, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x1, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(x2, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(x2, 1);

        this._x1 = x1;
        this._y1 = y1;
        this._x2 = x2;
        this._y2 = y2;
        this._isLinear = (x1 == y1) && (x2 == y2);

        if (!this._isLinear)
        {
            for (int i = 0; i < SplineTableSize; ++i)
            {
                this._sampleValues[i] = CalcBezier(i * SampleStepSize, this._x1, this._x2);
            }
        }
    }

    private protected override float EaseInCore(
        float normalizedTime)
    {
        if (this._isLinear)
        {
            return normalizedTime;
        }

        return CalcBezier(this.GetTForX(normalizedTime), this._y1, this._y2);
    }

    private static float A(float a1, float a2) { return 1.0f - (3.0f * a2) + (3.0f * a1); }

    private static float B(float a1, float a2) { return (3.0f * a2) - (6.0f * a1); }

    private static float C(float a1) { return 3.0f * a1; }

    // Returns x(t) given t, x1, and x2, or y(t) given t, y1, and y2.
    private static float CalcBezier(float t, float a1, float a2) { return ((((A(a1, a2) * t) + B(a1, a2)) * t) + C(a1)) * t; }

    // Returns dx/dt given t, x1, and x2, or dy/dt given t, y1, and y2.
    private static float GetSlope(float t, float a1, float a2) { return (3.0f * A(a1, a2) * t * t) + (2.0f * B(a1, a2) * t) + C(a1); }

    private static float BinarySubdivide(float x, float a, float b, float x1, float x2)
    {
        float currentX, currentT;
        int i = 0;

        do
        {
            currentT = a + ((b - a) / 2.0f);
            currentX = CalcBezier(currentT, x1, x2) - x;
            if (currentX > 0.0f)
            {
                b = currentT;
            }
            else
            {
                a = currentT;
            }
        }
        while ((MathF.Abs(currentX) > SubdivisionPrecision) && (++i < SubdivisionMaxIterations));

        return currentT;
    }

    private static float NewtonRaphsonIterate(float x, float guessT, float x1, float x2)
    {
        for (int i = 0; i < NewtonIterations; ++i)
        {
            float currentSlope = GetSlope(guessT, x1, x2);
            if (currentSlope == 0.0f)
            {
                return guessT;
            }

            float currentX = CalcBezier(guessT, x1, x2) - x;
            guessT -= currentX / currentSlope;
        }

        return guessT;
    }

    private float GetTForX(float x)
    {
        if (x >= 1.0f)
        {
            return 1.0f;
        }

        if (x <= 0.0f)
        {
            return 0.0f;
        }

        float intervalStart = 0.0f;
        int currentSample = 1;
        const int lastSample = SplineTableSize - 1;

        for (; (currentSample != lastSample) && (this._sampleValues[currentSample] <= x); ++currentSample)
        {
            intervalStart += SampleStepSize;
        }

        --currentSample;

        // Interpolate to provide an initial guess for t
        float dist = (x - this._sampleValues[currentSample]) / (this._sampleValues[currentSample + 1] - this._sampleValues[currentSample]);
        float guessForT = intervalStart + (dist * SampleStepSize);

        float initialSlope = GetSlope(guessForT, this._x1, this._x2);
        if (initialSlope >= NewtonMinSlope)
        {
            return NewtonRaphsonIterate(x, guessForT, this._x1, this._x2);
        }
        else if (initialSlope == 0.0f)
        {
            return guessForT;
        }
        else
        {
            return BinarySubdivide(x, intervalStart, intervalStart + SampleStepSize, this._x1, this._x2);
        }
    }
}

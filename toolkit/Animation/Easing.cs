// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;

namespace DotGame.Toolkit.Animation;

/// <summary>
/// Maps an animation's progress (0 to 1) to the eased progress of its value. An easing is immutable, so
/// one can be shared by every animation that uses the curve. The static properties are the common
/// curves, ready made, and the static methods make ones with your own settings.
/// </summary>
/// <remarks>
/// NOTE: An abstract class rather than an interface, so easing a value each frame is one
/// non-virtual call and one virtual call, without interface dispatch.
/// </remarks>
public abstract class Easing
{
    // Penner's, which overshoots by about 10%
    private const float DefaultBackOvershoot = 1.70158f;

    // Doubles every tenth of the way, as the standard exponential curve does
    private const float PennerExponent = 6.931472f;

    /// <summary>No easing: progress is unchanged.</summary>
    public static Easing Linear { get; } = new LinearEase();

    /// <summary>Starts slowly, on a sine curve.</summary>
    public static Easing SineIn { get; } = new SineEase(EasingMode.In);
    /// <summary>Ends slowly, on a sine curve.</summary>
    public static Easing SineOut { get; } = new SineEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a sine curve.</summary>
    public static Easing SineInOut { get; } = new SineEase(EasingMode.InOut);

    /// <summary>Starts slowly, on a square curve.</summary>
    public static Easing QuadIn { get; } = new QuadEase(EasingMode.In);
    /// <summary>Ends slowly, on a square curve.</summary>
    public static Easing QuadOut { get; } = new QuadEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a square curve.</summary>
    public static Easing QuadInOut { get; } = new QuadEase(EasingMode.InOut);

    /// <summary>Starts slowly, on a cubic curve.</summary>
    public static Easing CubicIn { get; } = new CubicEase(EasingMode.In);
    /// <summary>Ends slowly, on a cubic curve.</summary>
    public static Easing CubicOut { get; } = new CubicEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a cubic curve.</summary>
    public static Easing CubicInOut { get; } = new CubicEase(EasingMode.InOut);

    /// <summary>Starts slowly, on a fourth-power curve.</summary>
    public static Easing QuartIn { get; } = new QuartEase(EasingMode.In);
    /// <summary>Ends slowly, on a fourth-power curve.</summary>
    public static Easing QuartOut { get; } = new QuartEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a fourth-power curve.</summary>
    public static Easing QuartInOut { get; } = new QuartEase(EasingMode.InOut);

    /// <summary>Starts slowly, on a fifth-power curve.</summary>
    public static Easing QuintIn { get; } = new QuintEase(EasingMode.In);
    /// <summary>Ends slowly, on a fifth-power curve.</summary>
    public static Easing QuintOut { get; } = new QuintEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a fifth-power curve.</summary>
    public static Easing QuintInOut { get; } = new QuintEase(EasingMode.InOut);

    /// <summary>Starts very slowly, on an exponential curve.</summary>
    public static Easing ExpoIn { get; } = new ExponentialEase(EasingMode.In, PennerExponent);
    /// <summary>Ends very slowly, on an exponential curve.</summary>
    public static Easing ExpoOut { get; } = new ExponentialEase(EasingMode.Out, PennerExponent);
    /// <summary>Starts and ends very slowly, on an exponential curve.</summary>
    public static Easing ExpoInOut { get; } = new ExponentialEase(EasingMode.InOut, PennerExponent);

    /// <summary>Starts slowly, on a circular curve.</summary>
    public static Easing CircIn { get; } = new CircEase(EasingMode.In);
    /// <summary>Ends slowly, on a circular curve.</summary>
    public static Easing CircOut { get; } = new CircEase(EasingMode.Out);
    /// <summary>Starts and ends slowly, on a circular curve.</summary>
    public static Easing CircInOut { get; } = new CircEase(EasingMode.InOut);

    /// <summary>Pulls back before moving.</summary>
    public static Easing BackIn { get; } = new BackEase(EasingMode.In, DefaultBackOvershoot);

    /// <summary>Overshoots before settling.</summary>
    public static Easing BackOut { get; } = new BackEase(EasingMode.Out, DefaultBackOvershoot);

    /// <summary>Pulls back, then overshoots.</summary>
    public static Easing BackInOut { get; } = new BackEase(EasingMode.InOut, DefaultBackOvershoot);

    /// <summary>Swings with growing amplitude before moving off.</summary>
    public static Easing ElasticIn { get; } = new ElasticEase(EasingMode.In, 1f, ElasticEase.StandardPeriod);

    /// <summary>Swings past the end and settles, like a spring.</summary>
    public static Easing ElasticOut { get; } = new ElasticEase(EasingMode.Out, 1f, ElasticEase.StandardPeriod);

    /// <summary>Swings at both ends.</summary>
    public static Easing ElasticInOut { get; } = new ElasticEase(EasingMode.InOut, 1f, ElasticEase.StandardBothWaysPeriod);

    /// <summary>Hops with growing height before moving off.</summary>
    public static Easing BounceIn { get; } = new BounceEase(EasingMode.In);

    /// <summary>Bounces to a stop at the end, like a dropped ball.</summary>
    public static Easing BounceOut { get; } = new BounceEase(EasingMode.Out);

    /// <summary>Bounces at both ends.</summary>
    public static Easing BounceInOut { get; } = new BounceEase(EasingMode.InOut);

    private protected Easing(
        EasingMode easingMode)
    {
        this.Mode = easingMode;
    }

    internal EasingMode Mode { get; }

    /// <summary>
    /// A curve that goes past its end by <paramref name="overshoot"/> before settling, or pulls back before moving.
    /// </summary>
    /// <param name="mode">How the curve is applied.</param>
    /// <param name="overshoot">How far it goes: larger goes further. The default, as in <see cref="BackOut"/>, goes about 10% past.</param>
    public static Easing Back(
        EasingMode mode,
        float overshoot = DefaultBackOvershoot)
    {
        return new BackEase(mode, overshoot);
    }

    /// <summary>
    /// A curve that swings past its end and settles, with your own settings: Penner's elastic curve, as
    /// <see cref="ElasticOut"/> and the others are. With the defaults it's the same as those. Going out, it's the curve in, mirrored, as every easing is: at an amplitude above 1 that differs from Penner's own easeOutElastic.
    /// </summary>
    /// <param name="mode">How the curve is applied.</param>
    /// <param name="amplitude">
    /// How far the first swing goes: 1, the standard, and larger values swing further. At least 1.
    /// </param>
    /// <param name="period">
    /// The time between swings, as a fraction of the duration: larger values swing fewer times. Null
    /// is the standard for the mode, 0.3, or 0.45 for <see cref="EasingMode.InOut"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="amplitude"/> is less than 1, or <paramref name="period"/> isn't more than 0.
    /// </exception>
    public static Easing Elastic(
        EasingMode mode,
        float amplitude = 1f,
        float? period = null)
    {
        float standard = (mode == EasingMode.InOut) ? ElasticEase.StandardBothWaysPeriod : ElasticEase.StandardPeriod;
        return new ElasticEase(mode, amplitude, period ?? standard);
    }

    /// <summary>
    /// An exponential curve with your own steepness. <see cref="ExpoOut"/> and the others use the standard one, of about 7.
    /// </summary>
    /// <param name="mode">How the curve is applied.</param>
    /// <param name="exponent">How steep it is: 0 is a straight line.</param>
    public static Easing Exponential(
        EasingMode mode,
        float exponent = 2f)
    {
        return new ExponentialEase(mode, exponent);
    }

    /// <summary>
    /// A cubic Bezier curve from (0, 0) to (1, 1) through two control points, as CSS's <c>cubic-bezier(x1, y1, x2, y2)</c>.
    /// </summary>
    /// <param name="x1">The first control point's x, from 0 to 1, as in CSS.</param>
    /// <param name="y1">The first control point's y.</param>
    /// <param name="x2">The second control point's x, from 0 to 1, as in CSS.</param>
    /// <param name="y2">The second control point's y.</param>
    /// <exception cref="ArgumentOutOfRangeException">An x isn't from 0 to 1.</exception>
    public static Easing CubicBezier(
        float x1,
        float y1,
        float x2,
        float y2)
    {
        return new CubicBezierEase(x1, y1, x2, y2);
    }

    /// <summary>
    /// Holds <paramref name="count"/> values, from 0 to 1, each for an equal share of the time, jumping from one to the next: for a clock's hands, or a retro look. Unlike CSS's <c>steps()</c>, the last value, 1, is held as long as the others, so a looping or ping-pong animation shows it too.
    /// </summary>
    /// <param name="count">How many values it holds, the first 0 and the last 1: 2 or more.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is below 2.</exception>
    public static Easing Steps(
        int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 2);

        return new StepsEase(count);
    }

    /// <summary>
    /// Moves as a spring does: quickly at first, past the end, and back and forth until it settles.
    /// It ends at exactly 1, however long the animation is.
    /// </summary>
    /// <param name="damping">How quickly it settles, from above 0, for a lot of bounce, to 1, for none.</param>
    /// <exception cref="ArgumentOutOfRangeException">The damping isn't above 0 and at most 1.</exception>
    public static Easing Spring(
        float damping = 0.5f)
    {
        if (!((damping > 0f) && (damping <= 1f)))
        {
            throw new ArgumentOutOfRangeException(nameof(damping), damping, "The damping is above 0, and at most 1.");
        }

        return new SpringEase(damping);
    }

    /// <summary>A curve of your own.</summary>
    /// <param name="easeIn">The curve as it eases in, which should be 0 at 0 and 1 at 1.</param>
    /// <param name="mode">How the curve is applied.</param>
    public static Easing Custom(
        Func<float, float> easeIn,
        EasingMode mode)
    {
        ArgumentNullException.ThrowIfNull(easeIn);

        return new CustomEase(easeIn, mode);
    }

    /// <summary>
    /// This curve played backward, as if time ran from its end to its start: <see cref="CubicIn"/>
    /// reversed is <see cref="CubicOut"/>. It makes a new easing, so keep the result.
    /// </summary>
    public Easing Reversed()
    {
        return new ReversedEase(this);
    }

    /// <summary>
    /// The eased progress at <paramref name="normalizedTime"/>. It's 0 at or before 0 and 1 at or after 1, so a
    /// frame that runs past the end settles there, and may go outside 0 to 1 in between (to overshoot, for example).
    /// </summary>
    public float Evaluate(
        float normalizedTime)
    {
        float t = Math.Clamp(normalizedTime, 0f, 1f);

        switch (this.Mode)
        {
            case EasingMode.In:
                return this.EaseInCore(t);

            case EasingMode.Out:
                return 1.0f - this.EaseInCore(1.0f - t);

            case EasingMode.InOut:
                if (t < 0.5f)
                {
                    return this.EaseInCore(t * 2.0f) / 2.0f;
                }

                return ((1.0f - this.EaseInCore(2.0f - (t * 2.0f))) / 2.0f) + 0.5f;

            default:
                if (t < 0.5f)
                {
                    return 0.5f * (1f - this.EaseInCore(1 - (t * 2.0f)));
                }

                return (0.5f * this.EaseInCore((t * 2.0f) - 1)) + 0.5f;
        }
    }

    /// <summary>
    /// The curve, as it eases in: 0 at 0 and 1 at 1.
    /// </summary>
    private protected abstract float EaseInCore(
        float normalizedTime);
}

// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit.Animation;
using DotGame.Toolkit.Graphics;

namespace Sample.Pages;

/// <summary>
/// <see cref="Tween{T}"/> moving a dot along each kind of <see cref="Easing"/>: the ready-made curves,
/// and the ones made with settings.
/// </summary>
internal sealed class MotionPage : SamplePage
{
    private const float RowHeight = 34f;
    private const float LabelWidth = 210f;
    private const float DotRadius = 9f;

    private static readonly (string Name, Easing Easing)[] Curves =
    [
        ("Linear", Easing.Linear),
        ("CubicOut", Easing.CubicOut),
        ("CubicIn reversed", Easing.CubicIn.Reversed()),
        ("SineInOut", Easing.SineInOut),
        ("BackOut", Easing.BackOut),
        ("BackInOut", Easing.BackInOut),
        ("ElasticOut", Easing.ElasticOut),
        ("BounceOut", Easing.BounceOut),
        ("Spring, loose", Easing.Spring(0.25f)),
        ("Spring, firm", Easing.Spring(0.6f)),
        ("Spring, no bounce", Easing.Spring(1f)),
        ("Steps(6)", Easing.Steps(6)),
        ("CubicBezier", Easing.CubicBezier(0.68f, -0.55f, 0.27f, 1.55f)),
    ];

    // How far the curves go past each end of the track, as a fraction of it, so every dot stays
    // on screen: the lowest and the highest any of them reaches
    private static readonly (float Below, float Above) Overshoot = MeasureOvershoot();

    private readonly Tween<float>[] _tweens = new Tween<float>[Curves.Length];

    public MotionPage(
        ToolkitSample game)
        : base(game)
    {
        this.StartTweens();
    }

    public override string Title => "Easing and tweens";

    public override string Hint => "Each dot is a tween along an easing. Tap to start them together.";

    public override void Enter()
    {
        this.Restart();
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        if (e.Kind == PointerEventKind.Began)
        {
            this.Restart();
        }
    }

    public override void Update(
        TimeSpan elapsed)
    {
        // NOTE: The caller drives a tween, with the time that's passed
        foreach (var tween in this._tweens)
        {
            tween.Update(elapsed);
        }
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        float room = MathF.Max(bounds.Width - LabelWidth - 32f, 40f);
        float width = room / (1f + Overshoot.Below + Overshoot.Above);
        float left = bounds.X + 16f + LabelWidth + (width * Overshoot.Below);

        using var batch = spriteBatch.Begin();

        for (int i = 0; i < Curves.Length; i++)
        {
            float y = bounds.Y + 16f + (i * RowHeight);
            float middle = y + (RowHeight / 2f);

            game.DrawText(spriteBatch, Curves[i].Name, new Vector2(bounds.X + 16f, y + 6f), 18f, Color.FromRgb(0x3C465A));

            // The track, with marks at the start and the end that a curve can overshoot
            spriteBatch.FillRectangle(new Rectangle(left, middle - 1f, width, 2f), Color.FromRgb(0xC8D2E1));
            spriteBatch.FillRectangle(new Rectangle(left - 1f, middle - 6f, 2f, 12f), Color.FromRgb(0x96A5BE));
            spriteBatch.FillRectangle(new Rectangle(left + width - 1f, middle - 6f, 2f, 12f), Color.FromRgb(0x96A5BE));

            spriteBatch.FillCircle(new Vector2(left + (this._tweens[i].Value * width), middle), DotRadius, Color.FromRgb(0xF0785A));
        }
    }

    private static (float Below, float Above) MeasureOvershoot()
    {
        float lowest = 0f;
        float highest = 1f;
        foreach (var (_, easing) in Curves)
        {
            for (int i = 0; i <= 200; i++)
            {
                float value = easing.Evaluate(i / 200f);
                lowest = MathF.Min(lowest, value);
                highest = MathF.Max(highest, value);
            }
        }

        // A dot's radius more, as a little margin
        return (-lowest + 0.02f, (highest - 1f) + 0.02f);
    }

    private void StartTweens()
    {
        for (int i = 0; i < Curves.Length; i++)
        {
            this._tweens[i] = Tween.Create(
                0f,
                1f,
                TimeSpan.FromSeconds(1.5),
                Curves[i].Easing,
                playback: PlaybackMode.PingPong);
        }
    }

    private void Restart()
    {
        foreach (var tween in this._tweens)
        {
            tween.Restart();
        }
    }
}

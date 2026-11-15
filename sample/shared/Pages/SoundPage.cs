// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Audio;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit.Audio;

namespace Sample.Pages;

/// <summary>
/// A <see cref="SoundBank"/>: many short sounds built into one, so they load in one request and
/// decode once. Each is a <see cref="SoundSegment"/> of it, played with a
/// <see cref="SoundPool"/>, which limits how many copies of it play at once.
/// </summary>
/// <remarks>
/// The bank is built from every sound in <c>Content/Sounds</c> by a SoundBankContent item; each
/// segment is named after its file.
/// </remarks>
internal sealed class SoundPage : SamplePage
{
    private static readonly (string Name, int Limit)[] Sounds =
    [
        ("blip", 1),
        ("coin", 3),
        ("chime", 2),
        ("jump", 4),
        ("pop", 8),
    ];

    private readonly SoundPool[] _pools = new SoundPool[Sounds.Length];
    private readonly RectangleInt[] _buttons = new RectangleInt[Sounds.Length];
    private SoundVoice _chimeStart = null!;
    private RectangleInt _chimeStartButton;
    private RectangleInt _stopButton;

    public SoundPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Sound banks and pools";

    public override string Hint => "Tap a sound quickly: a pool plays at most its limit at once.";

    public override Task Load()
    {
        var game = this.Game;
        for (int i = 0; i < Sounds.Length; i++)
        {
            // A pool owns its voices, created up front; when all are busy, Play restarts the
            // oldest
            this._pools[i] = new SoundPool(game.Audio, game.Sounds.GetSegment(Sounds[i].Name), Sounds[i].Limit);
        }

        // Part of a segment, as a segment of its own: the chime's first note. A single voice
        // plays it, made from the segment with the toolkit's CreateVoice extension.
        var chime = game.Sounds.GetSegment("chime");
        var firstNote = chime.Sound.AsSegment(chime.Offset, TimeSpan.FromSeconds(0.15));
        this._chimeStart = game.Audio.CreateVoice(firstNote);

        return Task.CompletedTask;
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        for (int i = 0; i < this._buttons.Length; i++)
        {
            if (IsTap(e, this._buttons[i]))
            {
                this._pools[i].Play();
                return;
            }
        }

        if (IsTap(e, this._chimeStartButton))
        {
            this._chimeStart.Play();
        }
        else if (IsTap(e, this._stopButton))
        {
            foreach (var pool in this._pools)
            {
                pool.StopAll();
            }
        }
    }

    public override void OnKeyEvent(
        KeyEvent e)
    {
        // Keys 1 to 5 on the numeric keypad play the sounds
        if ((e.Kind == KeyEventKind.Pressed) && (e.Key >= Key.Numpad1) && (e.Key <= Key.Numpad5))
        {
            this._pools[e.Key - Key.Numpad1].Play();
        }
    }

    public override void Dispose()
    {
        foreach (var pool in this._pools)
        {
            pool.Dispose();
        }

        this._chimeStart.Dispose();
        base.Dispose();
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        using var batch = spriteBatch.Begin();

        int x = bounds.X + 20;
        int y = bounds.Y + 16;
        for (int i = 0; i < Sounds.Length; i++)
        {
            var segment = this._pools[i].Segment;
            this._buttons[i] = new RectangleInt(x, y, 150, 64);
            game.DrawButton(spriteBatch, this._buttons[i], Sounds[i].Name);
            game.DrawText(spriteBatch, $"{this._pools[i].VoiceLimit} at once, {segment.Duration.TotalMilliseconds:0} ms, from {segment.Offset.TotalSeconds:0.00} s in the bank", new Vector2(x + 166, y + 20), 17, Color.FromRgb(0x5A6478));
            y += 76;
        }

        y += 8;
        this._chimeStartButton = new RectangleInt(x, y, 230, 56);
        this._stopButton = new RectangleInt(x + 246, y, 150, 56);
        game.DrawButton(spriteBatch, this._chimeStartButton, "Chime, first note");
        game.DrawButton(spriteBatch, this._stopButton, "Stop all");
    }
}

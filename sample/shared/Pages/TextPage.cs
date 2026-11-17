// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit.Fonts;

namespace Sample.Pages;

/// <summary>
/// Text with <see cref="SpriteFont"/>: drawing at any size, effects (outlines, shadows, glows,
/// emboldening, softness, gradients and a sheen), characters colored, moved and turned one by one,
/// wrapping, measuring, tracking and rotation.
/// </summary>
/// <remarks>
/// Every font, size and effect draws with one shader, which the fonts switch to themselves, so the
/// draw calls for the text on this page split only where the atlas page changes.
/// </remarks>
internal sealed class TextPage : SamplePage
{
    private const string Paragraph =
        "Text wraps where you measure it. Each line here is drawn on its own, and the colors " +
        "follow the words across the lines, because each line passes its position in the whole text.";

    // Effects are values: set up once and shared, and derived with 'with'
    private static readonly TextEffects Outlined = new()
    {
        Outline = new TextOutline(Color.FromRgb(0x28325A), Width: 2.5f),
    };

    private static readonly TextEffects Shadowed = new()
    {
        Shadow = new TextShadow(Color.Black * 0.45f, Offset: new Vector2(2, 3), Softness: 2.5f),
    };

    private static readonly TextEffects Glowing = new()
    {
        Glow = new TextGlow(Color.FromRgb(0xFFC83C), Width: 3, Softness: 4),
        Outline = new TextOutline(Color.White, Width: 1),
    };

    private static readonly TextEffects TitleText = Outlined with
    {
        Shadow = new TextShadow(Color.Black * 0.4f, Offset: new Vector2(3, 4), Softness: 3),
        Embolden = 0.5f,
    };

    // A gradient is the face's two colors, from the top of the capitals to the baseline
    private static readonly TextEffects GoldTitle = TitleText with
    {
        Gradient = new TextGradient(Color.FromRgb(0xFFF0A0), Color.FromRgb(0xF08C1E)),
    };

    private static readonly TextEffects Sunset = Outlined with
    {
        Gradient = new TextGradient(Color.FromRgb(0xFFD25A), Color.FromRgb(0xDC4678)),
    };

    private static readonly TextEffects RoundOutline = new()
    {
        Outline = new TextOutline(Color.FromRgb(0xC83C50), Width: 3, Corners: TextCorners.Round),
    };

    private static readonly TextEffects Bold = new() { Embolden = 1 };

    private static readonly TextEffects Thin = new() { Embolden = -0.6f };

    private static readonly TextEffects Soft = new() { Softness = 3 };

    private static readonly (string Name, TextEffects Effects, Color Color)[] Gallery =
    [
        ("Outline", Outlined, Color.White),
        ("Shadow", Shadowed, Color.FromRgb(0x3C465A)),
        ("Glow", Glowing, Color.FromRgb(0x783C14)),
        ("Round", RoundOutline, Color.White),
        ("Bold", Bold, Color.FromRgb(0x3C465A)),
        ("Thin", Thin, Color.FromRgb(0x3C465A)),
        ("Soft", Soft, Color.FromRgb(0x3C465A)),
        ("Gradient", Sunset, Color.White),
    ];

    private readonly List<(int Start, int Length)> _lines = new();
    private readonly StringBuilder _counter = new();
    private int _wrapWidth;
    private int _taps;

    public TextPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Text";

    public override string Hint => "Tap to count: the counter is a StringBuilder, drawn without allocating.";

    /// <summary>
    /// Colors each character from a rainbow that moves with time, bobbing on a wave, and one word
    /// in the paragraph.
    /// </summary>
    /// <remarks>
    /// Character attributes are a struct, so drawing calls them without allocating or boxing.
    /// <see cref="MoveTo"/> is called for each character with its position in the whole text.
    /// </remarks>
    private struct RainbowAttributes : ICharacterAttributes
    {
        private readonly float _time;
        private readonly int _highlightStart;
        private readonly int _highlightLength;
        private Color? _color;
        private float _lift;

        public RainbowAttributes(
            float time,
            int highlightStart = -1,
            int highlightLength = 0)
        {
            this._time = time;
            this._highlightStart = highlightStart;
            this._highlightLength = highlightLength;
            this._color = null;
            this._lift = 0;
        }

        public readonly Color? Color => this._color;

        // A character's move is in the font's own pixels, and every layer of it moves together
        public readonly Vector2 Translation => new Vector2(0, this._lift);

        // (A struct spells out every attribute, the ones it leaves alone too: the interface's
        // own would box it on every call)

        public readonly Angle Rotation => default;

        public void MoveTo(
            in CharacterCursor cursor)
        {
            if (this._highlightStart >= 0)
            {
                // Only the highlighted word is colored; the rest keeps the text's color
                var i = cursor.CharIndex;
                this._color = ((i >= this._highlightStart) && (i < this._highlightStart + this._highlightLength))
                    ? DotGame.Framework.Color.FromRgb(0xDC4678)
                    : null;
            }
            else
            {
                var hue = (cursor.GlyphIndex * 0.07f) + this._time;
                this._color = new DotGame.Framework.Color(0.5f + (0.5f * MathF.Sin(hue * 6.28f)), 0.5f + (0.5f * MathF.Sin((hue + 0.33f) * 6.28f)), 0.5f + (0.5f * MathF.Sin((hue + 0.67f) * 6.28f)));
                this._lift = 5 * MathF.Sin((cursor.GlyphIndex * 0.6f) - (this._time * 8));
            }
        }
    }

    /// <summary>
    /// Sets a line of text along an arch: each character is moved onto a circle and turned to
    /// the circle's slope there.
    /// </summary>
    /// <remarks>
    /// <see cref="CharacterCursor.X"/> is how far along the line the middle of the character is,
    /// so the arch needs nothing but the line's middle and the circle's radius, both in the
    /// font's own pixels. Measuring and wrapping take no notice of a character's move or turn.
    /// </remarks>
    private struct ArchAttributes : ICharacterAttributes
    {
        private readonly float _middle;
        private readonly float _radius;
        private float _angle;

        public ArchAttributes(
            float middle,
            float radius)
        {
            this._middle = middle;
            this._radius = radius;
            this._angle = 0;
        }

        public readonly Color? Color => null;

        public readonly Vector2 Translation => new Vector2(
            this._radius * (MathF.Sin(this._angle) - this._angle),
            this._radius * (1 - MathF.Cos(this._angle)));

        public readonly Angle Rotation => Angle.FromRadians(this._angle);

        public void MoveTo(
            in CharacterCursor cursor)
        {
            this._angle = (cursor.X - this._middle) / this._radius;
        }
    }

    public override void OnPointerEvent(
        PointerEvent e)
    {
        if (e.Kind == PointerEventKind.Began)
        {
            this._taps++;
        }
    }

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        var font = game.Font;
        var time = (float)game.Time.Total.TotalSeconds;

        using var batch = spriteBatch.Begin();

        float x = bounds.X + 20;
        float y = bounds.Y + 8;

        // A title, with several effects drawn in order: shadow, outline, and a face in two colors
        game.DrawText(spriteBatch, "Sprite fonts", new Vector2(x, y), 46, Color.FromRgb(0xFAD25A), GoldTitle);
        y += 64;

        // A gallery of effects, each with its name
        var gallery = Gallery;
        var columnWidth = Math.Max(120, (bounds.Width - 40) / 4);
        for (int i = 0; i < gallery.Length; i++)
        {
            var cell = new Vector2(x + ((i % 4) * columnWidth), y + ((i / 4) * 52));
            game.DrawText(spriteBatch, gallery[i].Name, cell, 32, gallery[i].Color, gallery[i].Effects);
        }

        // In the gallery's next cell, rotation turns the text as one. Level, it's placed like the
        // words around it; it turns about its center.
        var spinText = "Spin";
        var spinScale = new Vector2(32 / font.EmSize);
        var spinHalf = font.MeasureString(spinText) / 2;
        var spinCell = new Vector2(x + ((gallery.Length % 4) * columnWidth), y + ((gallery.Length / 4) * 52));
        spriteBatch.DrawString(font, spinText, spinCell + (spinHalf * spinScale), Color.FromRgb(0x3C465A), 1, Angle.FromRadians(time), spinHalf, spinScale, effects: Shadowed);

        y += 8 + (((gallery.Length / 4) + 1) * 52);

        // Colors and moves per character, with attributes: a rainbow on a wave
        var rainbow = new RainbowAttributes(time * 0.5f);
        var wave = "Colors per character";
        var waveScale = new Vector2(30 / font.EmSize);
        spriteBatch.DrawString(font, wave.AsSpan(), rainbow, CharacterCursor.Start, new Vector2(x, y), Color.White, 1, Angle.Zero, Vector2.Zero, waveScale, effects: Outlined);

        y += 50;

        // Characters moved and turned onto an arch, which breathes; the gradient is measured on
        // the line, so it turns with each character. A sheen crosses it every couple of seconds:
        // a slanted band of light whose position along the line is all that changes
        var arched = "Along an arch";
        var archScale = new Vector2(34 / font.EmSize);
        var archWidth = font.MeasureString(arched).X;
        var arch = new ArchAttributes(archWidth / 2, archWidth * (1.1f + (0.35f * MathF.Sin(time))));
        const float SheenWidth = 46;
        var crossing = (time % 2.4f) / 0.9f;
        var lit = Sunset with
        {
            Sheen = new TextSheen(Color.White, -SheenWidth + (crossing * (archWidth + (2 * SheenWidth))), SheenWidth, Strength: 0.85f),
        };
        spriteBatch.DrawString(font, arched.AsSpan(), arch, CharacterCursor.Start, new Vector2(x, y), Color.White, 1, Angle.Zero, Vector2.Zero, archScale, effects: lit);

        y += 66;

        // Tracking: extra space after each character, in font units, scaled with the text
        float[] trackings = [-1.5f, 0, 4];
        var trackX = x;
        foreach (var tracking in trackings)
        {
            var scale = new Vector2(22 / font.EmSize);
            spriteBatch.DrawString(font, "Tracking", new Vector2(trackX, y), Color.FromRgb(0x3C465A), 1, Angle.Zero, Vector2.Zero, scale, tracking);
            trackX += (font.MeasureString("Tracking", tracking).X * scale.X) + 24;
        }

        y += 40;

        // A wrapped paragraph in a panel. Measuring ignores effects; an effect's reach, from
        // GetExtent, tells how much room to leave around it.
        var paragraphSize = 19f;
        var wrapWidth = Math.Min(bounds.Width - 80, 640);
        this.Wrap(Paragraph, paragraphSize, wrapWidth);
        var lineHeight = font.LineHeight * (paragraphSize / font.EmSize);
        var extent = Shadowed.GetExtent();
        var padding = 14 + (int)MathF.Ceiling(extent.Right * (paragraphSize / font.EmSize));
        var box = new RectangleInt((int)x, (int)y, wrapWidth + (2 * padding), (int)(this._lines.Count * lineHeight) + (2 * padding));
        game.Panel.Draw(spriteBatch, box);

        var highlight = Paragraph.IndexOf("colors", StringComparison.Ordinal);
        var attributes = new RainbowAttributes(0, highlight, "colors".Length);
        var lineY = y + padding;
        foreach (var (start, length) in this._lines)
        {
            // The cursor offset tells the attributes where this line starts in the paragraph
            spriteBatch.DrawString(font, Paragraph.AsSpan(start, length), attributes, new CharacterCursor(start, start),
                new Vector2(x + padding, lineY), Color.FromRgb(0x3C465A), 1, Angle.Zero, Vector2.Zero, new Vector2(paragraphSize / font.EmSize), effects: null);
            lineY += lineHeight;
        }

        y = box.Bottom + 16;

        // A StringBuilder is drawn chunk by chunk, so a changing number allocates no strings
        this._counter.Clear().Append("Taps: ").Append(this._taps);
        spriteBatch.DrawString(font, this._counter, new Vector2(x, y), Color.FromRgb(0x3C465A), 1, Angle.Zero, Vector2.Zero, new Vector2(24 / font.EmSize));
    }

    /// <summary>
    /// Breaks text into lines no wider than a width, at spaces.
    /// </summary>
    private void Wrap(
        string text,
        float pixelSize,
        int width)
    {
        if ((width == this._wrapWidth) && (this._lines.Count > 0))
        {
            return;
        }

        this._wrapWidth = width;
        this._lines.Clear();

        var font = this.Game.Font;
        var maxWidth = width / (pixelSize / font.EmSize);
        int lineStart = 0;
        int lastBreak = -1;
        for (int i = 0; i <= text.Length; i++)
        {
            if ((i < text.Length) && (text[i] != ' '))
            {
                continue;
            }

            if ((font.MeasureString(text.AsSpan(lineStart, i - lineStart)).X > maxWidth) && (lastBreak > lineStart))
            {
                this._lines.Add((lineStart, lastBreak - lineStart));
                lineStart = lastBreak + 1;
            }

            lastBreak = i;
        }

        this._lines.Add((lineStart, text.Length - lineStart));
    }
}

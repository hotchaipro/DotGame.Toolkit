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
/// emboldening and softness), colors per character, wrapping, measuring, tracking and rotation.
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
    /// Colors each character from a rainbow that moves with time, and one word in the paragraph.
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

        public RainbowAttributes(
            float time,
            int highlightStart = -1,
            int highlightLength = 0)
        {
            this._time = time;
            this._highlightStart = highlightStart;
            this._highlightLength = highlightLength;
            this._color = null;
        }

        public readonly Color? Color => this._color;

        // (A struct spells out every attribute, the ones it leaves alone too: the interface's
        // own would box it on every call)
        public readonly Vector2 Translation => Vector2.Zero;

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
            }
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

        // A title, with several effects drawn in order: shadow, outline, face
        game.DrawText(spriteBatch, "Sprite fonts", new Vector2(x, y), 46, Color.FromRgb(0xFAD25A), TitleText);
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

        y += 112;

        // Colors per character, with attributes
        var rainbow = new RainbowAttributes(time * 0.5f);
        var wave = "Colors per character";
        var waveScale = new Vector2(30 / font.EmSize);
        spriteBatch.DrawString(font, wave.AsSpan(), rainbow, CharacterCursor.Start, new Vector2(x, y), Color.White, 1, Angle.Zero, Vector2.Zero, waveScale, effects: Outlined);

        y += 50;

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

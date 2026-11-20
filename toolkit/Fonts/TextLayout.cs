// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Text wrapped to a width: lines broken at spaces, hyphens and line feeds, and between CJK characters
/// with their punctuation rules, aligned, and cut off with an
/// ellipsis past a number of lines. Set its <see cref="Text"/> and properties, then measure it with
/// <see cref="Size"/> and draw it with <see cref="Draw"/> as often as you like: it works out its
/// lines again when they're next needed after a change. It keeps its lines, so a layout that's reused allocates nothing once
/// it's grown to its largest text.
/// </summary>
/// <remarks>
/// Like the rest of the font code, it draws left to right, with no shaping. A word wider than the
/// whole width breaks between its characters.
/// </remarks>
public sealed class TextLayout
{
    private readonly SpriteFont _font;
    private readonly List<TextLayoutLine> _lines = new();
    private string _text = string.Empty;
    private float? _maxWidth;
    private int? _maxLines;
    private float _scale = 1;
    private float _tracking;
    private float _lineSpacing = 1;
    private string _ellipsis = string.Empty;
    private Vector2 _size;
    private bool _isStale;

    private const int EllipsisCharacter = 0x2026;

    /// <summary>Makes a layout for this font.</summary>
    public TextLayout(
        SpriteFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        this._font = font;
        this.Ellipsis = font.TryGetGlyph(EllipsisCharacter, out _) ? "…" : "...";
    }

    /// <summary>The text to lay out.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Text
    {
        get => this._text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.Change(ref this._text, value);
        }
    }

    /// <summary>
    /// How wide lines may be, in drawn pixels, or null to break only at line feeds.
    /// </summary>
    public float? MaxWidth
    {
        get => this._maxWidth;
        set => this.Change(ref this._maxWidth, value);
    }

    /// <summary>
    /// The most lines to show, with an ellipsis if there's more text, or null for no limit.
    /// </summary>
    public int? MaxLines
    {
        get => this._maxLines;
        set => this.Change(ref this._maxLines, value);
    }

    /// <summary>
    /// How much bigger than the font's own size to draw: <c>pixelSize / font.EmSize</c>.
    /// </summary>
    public float Scale
    {
        get => this._scale;
        set => this.Change(ref this._scale, value);
    }

    /// <summary>
    /// Extra space after each character, in font units, as <c>DrawString</c> takes it.
    /// </summary>
    public float Tracking
    {
        get => this._tracking;
        set => this.Change(ref this._tracking, value);
    }

    /// <summary>
    /// The distance between lines, as a multiple of the font's line height.
    /// </summary>
    public float LineSpacing
    {
        get => this._lineSpacing;
        set => this.Change(ref this._lineSpacing, value);
    }

    /// <summary>How lines sit in the width. It needs <see cref="MaxWidth"/> set, or it uses the widest line's.</summary>
    public TextAlignment Alignment { get; set; }

    /// <summary>
    /// What ends a line that was cut off: the ellipsis character if the font has it, or three dots.
    /// </summary>
    public string Ellipsis
    {
        get => this._ellipsis;
        set => this.Change(ref this._ellipsis, value);
    }

    /// <summary>
    /// The lines of the text.
    /// </summary>
    public IReadOnlyList<TextLayoutLine> Lines
    {
        get
        {
            this.Refresh();
            return this._lines;
        }
    }

    /// <summary>
    /// How much space the text takes: the widest line, and the lines' height.
    /// </summary>
    public Vector2 Size
    {
        get
        {
            this.Refresh();
            return this._size;
        }
    }

    private float LineDistance => this._font.LineHeight * this.Scale * this.LineSpacing;

    private void Change<T>(
        ref T field,
        T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            this._isStale = true;
        }
    }

    // Breaks the text into lines with the current properties, if they've changed
    private void Refresh()
    {
        if (!this._isStale)
        {
            return;
        }

        this._isStale = false;
        var text = this._text;
        this._lines.Clear();

        int start = 0;
        while (start <= text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (!this.AddParagraph(start, end))
            {
                break;
            }

            start = end + 1;
        }

        float widest = 0;
        foreach (var line in this._lines)
        {
            widest = MathF.Max(widest, line.Width);
        }

        float height = (this._lines.Count == 0)
            ? 0
            : (this.LineDistance * (this._lines.Count - 1)) + (this._font.LineHeight * this.Scale);

        this._size = new Vector2(widest, height);
    }

    private float Measure(
        int start,
        int length)
    {
        return this.Measure(this._text.AsSpan(start, length));
    }

    private float Measure(
        ReadOnlySpan<char> text)
    {
        return this._font.MeasureString(text, this.Tracking).X * this.Scale;
    }

    // Adds a paragraph's lines. Returns false once the line limit has stopped the text.
    private bool AddParagraph(
        int start,
        int end)
    {
        int lineStart = start;
        do
        {
            int lineEnd = this.FindLineEnd(lineStart, end);
            int visibleEnd = lineEnd;
            while ((visibleEnd > lineStart) && LineBreaking.IsBreakSpace(this._text[visibleEnd - 1]))
            {
                visibleEnd--;
            }

            // The next line starts after the spaces it broke at
            int next = lineEnd;
            while ((next < end) && LineBreaking.IsBreakSpace(this._text[next]))
            {
                next++;
            }

            bool isLimit = this._lines.Count == this.MaxLines - 1;
            bool hasMore = (next < end) || (end < this._text.Length);
            if (isLimit && hasMore)
            {
                this.AddLineWithEllipsis(lineStart, visibleEnd);
                return false;
            }

            this._lines.Add(new TextLayoutLine(lineStart, visibleEnd - lineStart, this.Measure(lineStart, visibleEnd - lineStart), false));
            lineStart = next;
        }
        while (lineStart < end);

        return true;
    }

    // Where the line starting here ends: at the last place it can break that fits, or inside a word
    // that doesn't fit on a line at all
    private int FindLineEnd(
        int start,
        int end)
    {
        if (this.MaxWidth is not float maxWidth)
        {
            return end;
        }

        float width = 0;
        int previousGlyph = 0;
        int before = 0;
        int lastBreak = -1;
        int lastFit = start;
        int i = start;
        while (i < end)
        {
            int codePoint = this.CodePointAt(i, end, out int length);

            // NOTE: Before its width, so a line can end right before a space that doesn't fit
            if ((i > start) && LineBreaking.CanBreakBetween(before, codePoint))
            {
                lastBreak = i;
            }

            width += this._font.MeasureNext(codePoint, this.Tracking, ref previousGlyph) * this.Scale;
            if (width > maxWidth)
            {
                break;
            }

            i += length;
            lastFit = i;
            before = codePoint;
        }

        if (i >= end)
        {
            return end;
        }

        if (lastBreak > start)
        {
            return lastBreak;
        }

        // A character always goes on a line, however narrow the width, or nothing would advance
        if (lastFit == start)
        {
            this.CodePointAt(start, end, out int first);
            return start + first;
        }

        // Inside a word, punctuation still doesn't start a line, if there's a character to take with it
        while ((lastFit < end) && LineBreaking.IsNoStart(this.CodePointAt(lastFit, end, out _)))
        {
            int back = lastFit - (char.IsLowSurrogate(this._text[lastFit - 1]) ? 2 : 1);
            if (back <= start)
            {
                break;
            }

            lastFit = back;
        }

        return lastFit;
    }

    private int CodePointAt(
        int index,
        int end,
        out int length)
    {
        char c = this._text[index];
        if (char.IsHighSurrogate(c) && (index + 1 < end) && char.IsLowSurrogate(this._text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(c, this._text[index + 1]);
        }

        length = 1;
        return c;
    }

    private void AddLineWithEllipsis(
        int start,
        int end)
    {
        float ellipsis = this.Measure(this.Ellipsis);

        // Back off a character at a time until the ellipsis fits, and off any space it would follow
        int length = end - start;
        while ((length > 0) && ((this.Measure(start, length) + ellipsis) > this.MaxWidth))
        {
            length--;
            if ((length > 0) && char.IsLowSurrogate(this._text[start + length]))
            {
                length--;
            }
        }

        while ((length > 0) && LineBreaking.IsBreakSpace(this._text[start + length - 1]))
        {
            length--;
        }

        this._lines.Add(new TextLayoutLine(start, length, this.Measure(start, length) + ellipsis, true));
    }

    /// <summary>
    /// Draws the lines from the top left of <paramref name="position"/>, aligned in
    /// <see cref="MaxWidth"/> if it's set and otherwise in the widest line's width.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Vector2 position,
        Color color,
        float opacity = 1,
        TextEffects? effects = null)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        this.Refresh();

        // Every line's underlays go behind every line's glyphs, so one line's shadow or outline
        // never covers the line above it
        if ((effects is not null) && effects.HasUnderlays)
        {
            this.DrawLines(spriteBatch, position, color, opacity, effects, TextLayers.Underlays);
            this.DrawLines(spriteBatch, position, color, opacity, effects, TextLayers.Face);
        }
        else
        {
            this.DrawLines(spriteBatch, position, color, opacity, effects, TextLayers.All);
        }
    }

    private void DrawLines(
        SpriteBatch spriteBatch,
        Vector2 position,
        Color color,
        float opacity,
        TextEffects? effects,
        TextLayers layers)
    {
        float box = this.MaxWidth ?? this.Size.X;
        var scale = new Vector2(this.Scale);
        float y = position.Y;

        foreach (var line in this._lines)
        {
            float x = this.Alignment switch
            {
                TextAlignment.Center => position.X + ((box - line.Width) / 2),
                TextAlignment.Right => position.X + box - line.Width,
                _ => position.X,
            };

            var text = this._text.AsSpan(line.Start, line.Length);
            spriteBatch.DrawString(this._font, text, new Vector2(x, y), color, opacity, default, default, scale, this.Tracking, effects, layers);

            if (line.HasEllipsis)
            {
                spriteBatch.DrawString(this._font, this.Ellipsis, new Vector2(x + this.Measure(text), y), color, opacity, default, default, scale, this.Tracking, effects, layers);
            }

            y += this.LineDistance;
        }
    }
}

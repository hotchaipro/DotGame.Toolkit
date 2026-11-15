// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using DotGame.Framework;
using DotGame.Framework.Graphics;
using DotGame.Toolkit.Fonts;

namespace Sample.Pages;

/// <summary>
/// <see cref="TextLayout"/>: text wrapped to a width, aligned, and cut off with an ellipsis.
/// </summary>
internal sealed class TextLayoutPage : SamplePage
{
    private const string Text = "Text wraps at spaces to fit its width, and a line feed\nstarts a new line. A layout keeps its lines, so drawing it every frame costs no more than drawing them.";
    private const float PixelSize = 22;

    private readonly TextAlignment[] _alignments = [TextAlignment.Left, TextAlignment.Center, TextAlignment.Right];
    private TextLayout?[] _layouts = [];
    private TextLayout? _cutOff;

    public TextLayoutPage(
        ToolkitSample game)
        : base(game)
    {
    }

    public override string Title => "Text layout";

    public override string Hint => "The width grows and shrinks. The last one stops at two lines.";

    public override void Draw(
        SpriteBatch spriteBatch,
        RectangleInt bounds)
    {
        var game = this.Game;
        float time = (float)game.Time.Total.TotalSeconds;
        float scale = PixelSize / game.Font.EmSize;
        float width = 280 + (60 * MathF.Sin(time));

        if (this._layouts.Length == 0)
        {
            this._layouts = new TextLayout?[this._alignments.Length];
        }

        using var batch = spriteBatch.Begin();
        // Two columns as wide as the text can get, so the layouts never run into each other
        const float ColumnWidth = 340 + 40;
        var origins = new Vector2[this._alignments.Length + 1];
        float rowHeight = 0;
        for (int i = 0; i < this._alignments.Length; i++)
        {
            var layout = this._layouts[i] ??= new TextLayout(game.Font) { Scale = scale, Alignment = this._alignments[i] };
            layout.MaxWidth = width;
            layout.Text = Text;
            if (i < 2)
            {
                rowHeight = MathF.Max(rowHeight, layout.Size.Y);
            }
        }

        for (int i = 0; i < origins.Length; i++)
        {
            origins[i] = new Vector2(bounds.X + 16 + ((i % 2) * ColumnWidth), bounds.Y + 16 + ((i / 2) * (rowHeight + 32)));
        }

        for (int i = 0; i < this._alignments.Length; i++)
        {
            this._layouts[i]!.Draw(spriteBatch, origins[i], Color.FromRgb(0x3C465A));
        }

        this._cutOff ??= new TextLayout(game.Font) { Scale = scale, MaxLines = 2 };
        this._cutOff.MaxWidth = width;
        this._cutOff.Text = Text;
        this._cutOff.Draw(spriteBatch, origins[^1], Color.FromRgb(0xC83C50));
    }
}

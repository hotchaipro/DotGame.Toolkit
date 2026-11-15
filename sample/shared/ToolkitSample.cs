// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DotGame.Framework;
using DotGame.Framework.Diagnostics;
using DotGame.Framework.Graphics;
using DotGame.Framework.Input;
using DotGame.Toolkit;
using DotGame.Toolkit.Audio;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.Graphics;
using Sample.Pages;

namespace Sample;

/// <summary>
/// A tour of the DotGame toolkit: sprite fonts and text effects, texture atlases and
/// nine-patches, sprite animation, and sound banks and pools. One page for each.
/// </summary>
/// <remarks>
/// <para>
/// The toolkit builds on the framework (see the framework sample for the basics: the game
/// class, drawing, input and audio). Its content (fonts, atlases and sound banks) is built by the
/// toolkit content pipeline, which the project imports, and loaded through the framework's
/// <see cref="DotGame.Framework.Content.ContentService"/> once <see cref="GameExtensions.InitializeToolkit"/> has registered
/// the toolkit's loaders.
/// </para>
/// <para>
/// Move between pages with the arrows at the top, the left and right arrow keys, Tab and Shift+Tab,
/// or the number keys. Press ` (backtick, left of 1) for the framework's stats: unlike F1, which a
/// browser keeps for its help, no browser or system uses it, and it needs no Fn key on a Mac laptop.
/// </para>
/// </remarks>
internal sealed class ToolkitSample : Game
{
    private const int HeaderHeight = 64;
    private const int FooterHeight = 44;

    private readonly List<SamplePage> _pages = new();
    private int _pageIndex;
    private bool _isLoaded;

    private static readonly TextEffects TitleEffects = new()
    {
        Shadow = new TextShadow(Color.Black * 0.5f, Offset: new Vector2(1, 2), Softness: 1.5f),
    };

    /// <summary>
    /// Gets the font every page uses.
    /// </summary>
    internal SpriteFont Font { get; private set; } = null!;

    /// <summary>
    /// Gets the sample's texture atlas: every image in <c>Content/Sprites</c>, packed together.
    /// </summary>
    internal TextureAtlas Atlas { get; private set; } = null!;

    /// <summary>
    /// Gets the sample's sound bank: every sound in <c>Content/Sounds</c>, in one file.
    /// </summary>
    internal SoundBank Sounds { get; private set; } = null!;

    /// <summary>
    /// Gets the panel that buttons and boxes are drawn with.
    /// </summary>
    internal NinePatch Panel { get; private set; } = null!;

    internal RectangleInt PageBounds
    {
        get
        {
            var safe = this.SafeAreaBounds;
            return new RectangleInt(safe.X, safe.Y + HeaderHeight, safe.Width, safe.Height - HeaderHeight - FooterHeight);
        }
    }

    protected override async Task OnStart()
    {
        // Registers the loaders for the toolkit's content: fonts, atlases and sound banks
        this.InitializeToolkit();

        this.Font = await this.Content.LoadAsync<SpriteFont>("Fonts/Inter-Medium");
        this.Atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/spritesheet");
        this.Sounds = await this.Content.LoadAsync<SoundBank>("Sounds/soundbank");

        // The panel image has an 8-pixel shadow below and to the right. As an outset, the shadow
        // is drawn outside the bounds the panel is given, so panels line up by their edges.
        var panel = this.Atlas.GetRegion("panel").WithOutset(new ThicknessInt(0, 0, 8, 8));
        this.Panel = new NinePatch(panel, caps: new ThicknessInt(24));

        this._pages.Add(new TextPage(this));
        this._pages.Add(new AtlasPage(this));
        this._pages.Add(new AnimationPage(this));
        this._pages.Add(new SoundPage(this));
        this._pages.Add(new CameraPage(this));
        this._pages.Add(new ShapesPage(this));
        this._pages.Add(new MotionPage(this));
        this._pages.Add(new RuntimeTexturesPage(this));
        this._pages.Add(new TextLayoutPage(this));

        foreach (var page in this._pages)
        {
            await page.Load();
        }

        this._isLoaded = true;
        this._pages[0].Enter();
    }

    protected override Task OnStop()
    {
        // Content loaded through Content is released with it; what the game created itself,
        // it releases
        foreach (var page in this._pages)
        {
            page.Dispose();
        }

        this._pages.Clear();
        this._isLoaded = false;
        return Task.CompletedTask;
    }

    private SamplePage? CurrentPage => this._isLoaded ? this._pages[this._pageIndex] : null;

    private void ShowPage(
        int index)
    {
        index = ((index % this._pages.Count) + this._pages.Count) % this._pages.Count;
        if (!this._isLoaded || (index == this._pageIndex))
        {
            return;
        }

        this._pages[this._pageIndex].Leave();
        this._pageIndex = index;
        this._pages[this._pageIndex].Enter();
    }

    protected override void OnUpdate()
    {
        this.CurrentPage?.Update(this.Time.Delta);
    }

    protected override void OnPointerEvent(
        PointerEvent e)
    {
        var safe = this.SafeAreaBounds;
        if ((e.Kind == PointerEventKind.Began) && (e.Position.Y < safe.Y + HeaderHeight))
        {
            if (e.Position.X < safe.X + HeaderHeight)
            {
                this.ShowPage(this._pageIndex - 1);
            }
            else if (e.Position.X > safe.Right - HeaderHeight)
            {
                this.ShowPage(this._pageIndex + 1);
            }

            return;
        }

        this.CurrentPage?.OnPointerEvent(e);
    }

    protected override void OnKeyEvent(
        KeyEvent e)
    {
        if (e.Kind == KeyEventKind.Pressed)
        {
            if (e.Key == Key.Tab)
            {
                this.ShowPage(this._pageIndex + (((e.Modifiers & ModifierKeys.Shift) != 0) ? -1 : 1));
                return;
            }

            // The left and right arrow keys, like the header's arrows
            if ((e.Key == Key.Left) || (e.Key == Key.Right))
            {
                this.ShowPage(this._pageIndex + ((e.Key == Key.Left) ? -1 : 1));
                return;
            }

            // ` (backtick) cycles the framework's stats overlay: off, frames per second, and detailed
            if (e.Key == Key.Grave)
            {
                this.Diagnostics.StatsOverlayMode = this.Diagnostics.StatsOverlayMode switch
                {
                    StatsOverlayMode.Off => StatsOverlayMode.FpsOnly,
                    StatsOverlayMode.FpsOnly => StatsOverlayMode.Detailed,
                    _ => StatsOverlayMode.Off,
                };
                return;
            }

            // 1 to 9 for the pages, as many as there are
            int number = e.Key - Key.D1;
            if ((number >= 0) && (number <= 8) && (number < this._pages.Count))
            {
                this.ShowPage(number);
                return;
            }
        }

        this.CurrentPage?.OnKeyEvent(e);
    }

    protected override void OnDraw()
    {
        this.Graphics.Clear(Color.FromRgb(0xECF0F6));

        var page = this.CurrentPage;
        if (page is null)
        {
            return;
        }

        var spriteBatch = this.Graphics.SharedSpriteBatch;
        page.Draw(spriteBatch, this.PageBounds);

        // The header and footer: a panel, text and arrows, all in one batch
        var safe = this.SafeAreaBounds;
        using var batch = spriteBatch.Begin();
        var header = new RectangleInt(safe.X + 8, safe.Y + 6, safe.Width - 24, HeaderHeight - 18);
        this.Panel.Draw(spriteBatch, header);
        this.DrawText(spriteBatch, "<", new Vector2(header.X + 18, header.Y + 6), 28, Color.FromRgb(0x2F4F4F));
        this.DrawText(spriteBatch, ">", new Vector2(header.Right - 34, header.Y + 6), 28, Color.FromRgb(0x2F4F4F));
        var title = page.Title;
        var titleSize = 26;
        this.DrawText(spriteBatch, title, new Vector2(header.Center.X - (this.MeasureText(title, titleSize) / 2), header.Y + 7), titleSize, Color.FromRgb(0x3C465A), TitleEffects);
        var footerY = safe.Bottom - FooterHeight + 10;
        var hintColor = Color.FromRgb(0x5A6478);
        this.DrawText(spriteBatch, page.Hint, new Vector2(safe.X + 16, footerY), 17, hintColor);

        // The stats shortcut, at the footer's right, where the page's hint leaves room for it
        const string StatsHint = "Press ` (backtick) for stats";
        var statsWidth = this.MeasureText(StatsHint, 17);
        var statsX = safe.Right - 16 - statsWidth;
        if (statsX > safe.X + 16 + this.MeasureText(page.Hint, 17) + 32)
        {
            this.DrawText(spriteBatch, StatsHint, new Vector2(statsX, footerY), 17, hintColor);
        }
    }

    /// <summary>
    /// Draws text at a size in pixels, with optional effects.
    /// </summary>
    internal void DrawText(
        SpriteBatch spriteBatch,
        ReadOnlySpan<char> text,
        Vector2 position,
        float pixelSize,
        Color color,
        TextEffects? effects = null)
    {
        // Fonts are built at one size (EmSize) and scaled: distance-field fonts stay sharp
        spriteBatch.DrawString(this.Font, text, position, color, scale: new Vector2(pixelSize / this.Font.EmSize), effects: effects);
    }

    /// <summary>
    /// Measures the width of text at a size in pixels.
    /// </summary>
    internal float MeasureText(
        ReadOnlySpan<char> text,
        float pixelSize)
    {
        return this.Font.MeasureString(text).X * (pixelSize / this.Font.EmSize);
    }

    /// <summary>
    /// Draws a button: a panel with its label centered. Call within a text-shader batch.
    /// </summary>
    internal void DrawButton(
        SpriteBatch spriteBatch,
        RectangleInt bounds,
        string label,
        bool isActive = false)
    {
        this.Panel.Draw(spriteBatch, bounds, isActive ? Color.FromRgb(0xBEDCFF) : Color.White);
        const float Size = 20;
        var width = this.MeasureText(label, Size);
        var lineHeight = this.Font.LineHeight * (Size / this.Font.EmSize);
        this.DrawText(spriteBatch, label, new Vector2(bounds.Center.X - (width / 2), bounds.Center.Y - (lineHeight / 2)), Size, Color.FromRgb(0x323C50));
    }
}

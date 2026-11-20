// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using DotGame.Framework;
using DotGame.Framework.Content;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// A font drawn with <see cref="SpriteBatch"/> from glyphs packed into one or more atlas pages,
/// built from a TTF/OTF font (and optional fallback fonts) by the toolkit SpriteFontContent pipeline.
/// </summary>
/// <remarks>
/// <para>
/// All metrics are in pixels at <see cref="EmSize"/>, the size the glyphs were rendered at.
/// Draw at another size by scaling: <c>scale = new Vector2(pixelSize / font.EmSize)</c>.
/// Distance-field fonts (<see cref="GlyphFormat.Sdf"/>, <see cref="GlyphFormat.Msdf"/>,
/// <see cref="GlyphFormat.Mtsdf"/>) stay sharp at any scale, and
/// <see cref="GlyphFormat.Bitmap"/> fonts are pixel-exact at scale 1. Text draws with the font's
/// <see cref="Shader"/>, in any batch, so there's nothing to set up to draw it.
/// </para>
/// <para>
/// Text is laid out from the top-left corner of the first line: the baseline of each line is
/// <see cref="Ascender"/> below its top, and lines are <see cref="LineHeight"/> apart.
/// </para>
/// <para>
/// There is no text shaping: each code point draws its own glyph, left to right. No ligatures,
/// bidirectional reordering or contextual forms are applied, so Arabic, Hebrew and Indic scripts
/// don't draw correctly, and an emoji sequence draws as its separate code points.
/// </para>
/// </remarks>
public sealed class SpriteFont : IContent
{
    private const int AsciiGlyphCount = 128;

    private readonly List<Texture> _textures;
    private readonly Dictionary<int, SpriteFontGlyph> _glyphs;
    private readonly SpriteFontGlyph?[] _asciiGlyphs = new SpriteFontGlyph?[AsciiGlyphCount];
    private readonly ulong[] _kerningKeys;
    private readonly float[] _kerningValues;
    private SpriteFontGlyph? _defaultGlyph;
    private int? _defaultCharacter;
    private readonly ContentService? _content;
    private Shader _shader;

    private bool _isDisposed;

    // The face of text drawn without effects, the common case, set up once
    private readonly TextLayer _plainFace;

    // The Debug-build checks that report a font's limits once each
    private bool _reportedClamp;
    private bool _reportedBitmapEffects;

    internal SpriteFont(
        SpriteFontData data,
        List<Texture> textures,
        Shader shader,
        ContentService? content = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(shader);

        this._textures = textures;
        this._shader = shader;
        this._content = content;
        this.GlyphFormat = data.GlyphFormat;
        this.EmSize = data.EmSize;
        this.DistanceRange = data.GlyphFormat == GlyphFormat.Bitmap ? null : data.DistanceRange;
        this.DistanceRangeMiddle = data.GlyphFormat == GlyphFormat.Bitmap ? 0 : data.DistanceRangeMiddle;
        this.LineHeight = data.LineHeight;
        this.Ascender = data.Ascender;
        this.Descender = data.Descender;
        this.UnderlinePosition = data.UnderlinePosition;
        this.UnderlineThickness = data.UnderlineThickness;
        this.CapHeight = data.CapHeight;
        this.TextureSize = new Vector2(data.Pages[0].Width, data.Pages[0].Height);

        var glyphRecords = data.Glyphs.Span;
        this._glyphs = new Dictionary<int, SpriteFontGlyph>(data.GlyphCount);
        for (int i = 0; i < data.GlyphCount; i++)
        {
            SpriteFontReader.ReadGlyph(glyphRecords, i, out var codePoint, out var page, out var x, out var y, out var width, out var height,
                out var advance, out var left, out var top, out var right, out var bottom);

            if ((uint)page >= (uint)textures.Count)
            {
                throw new System.IO.InvalidDataException($"Glyph U+{codePoint:X4} refers to missing atlas page {page}.");
            }

            var glyph = new SpriteFontGlyph(textures[page], page, new RectangleInt(x, y, width, height), codePoint, advance, left, top, right, bottom);
            this._glyphs[codePoint] = glyph;
            if ((uint)codePoint < AsciiGlyphCount)
            {
                this._asciiGlyphs[codePoint] = glyph;
            }
        }

        var kerningRecords = data.Kerning.Span;
        var kerningCount = kerningRecords.Length / SpriteFontReader.KerningRecordSize;
        this._kerningKeys = new ulong[kerningCount];
        this._kerningValues = new float[kerningCount];
        for (int i = 0; i < kerningCount; i++)
        {
            SpriteFontReader.ReadKerning(kerningRecords, i, out var first, out var second, out var adjustment);
            this._kerningKeys[i] = KerningKey(first, second);
            this._kerningValues[i] = adjustment;
        }

        // The pipeline writes the pairs sorted, but sorting here keeps the lookup correct
        // regardless of the writer.
        Array.Sort(this._kerningKeys, this._kerningValues);

        if (this._glyphs.ContainsKey(0xFFFD))
        {
            this.DefaultCharacter = 0xFFFD;
        }
        else if (this._glyphs.ContainsKey('?'))
        {
            this.DefaultCharacter = '?';
        }

        this._plainFace = new TextLayer(isFace: true, default, Vector2.Zero, this.GetLayerData(0, 0, FaceMiterLimit));
    }

    /// <summary>Whether it has been released.</summary>
    public bool IsDisposed => this._isDisposed;

    /// <summary>
    /// Gets or sets the shader this font's text draws with, whichever the sprite batch is using, which
    /// it goes back to after the text. It starts as the toolkit's text shader, one shared by every
    /// font, which draws every glyph format and every <see cref="TextEffects"/>.
    /// </summary>
    /// <remarks>
    /// The text shader has no parameters: each font's and effect's settings travel with each glyph, so
    /// text in many fonts and effects draws together, split only where the atlas page changes. A custom
    /// shader can read the same settings, as <c>TEXCOORD1</c>; the toolkit's <c>spritefont.slang</c>
    /// describes them. It stays yours to release.
    /// </remarks>
    public Shader Shader
    {
        get => this._shader;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            this._shader = value;
        }
    }

    /// <summary>
    /// Gets how the glyphs are stored in the atlas pages, which decides how the font is drawn.
    /// </summary>
    internal GlyphFormat GlyphFormat
    {
        get;
    }

    /// <summary>
    /// Gets the size, in pixels per em, that the glyphs were rendered at. Every other metric of
    /// the font is in pixels at this size.
    /// </summary>
    public float EmSize
    {
        get;
    }

    /// <summary>
    /// Gets the distance between the baselines of consecutive lines.
    /// </summary>
    public float LineHeight
    {
        get;
    }

    /// <summary>
    /// Gets the height of the font's ascender above the baseline, which is the distance from the
    /// top of a line to its baseline.
    /// </summary>
    public float Ascender
    {
        get;
    }

    /// <summary>
    /// Gets the height of the font's capital letters above the baseline, from the font's OS/2
    /// table or its "H", or zero if the font doesn't give it.
    /// </summary>
    public float CapHeight
    {
        get;
    }

    /// <summary>
    /// Gets the font's descender relative to the baseline; negative values lie below it.
    /// </summary>
    public float Descender
    {
        get;
    }

    /// <summary>
    /// Gets the position of the center of the underline below the baseline.
    /// </summary>
    public float UnderlinePosition
    {
        get;
    }

    /// <summary>
    /// Gets the thickness of the underline.
    /// </summary>
    public float UnderlineThickness
    {
        get;
    }

    /// <summary>
    /// Gets the size of each of the font's atlas pages, in texels.
    /// </summary>
    public Vector2 TextureSize
    {
        get;
    }

    /// <summary>
    /// Gets the font's atlas pages.
    /// </summary>
    public IReadOnlyList<Texture> Pages => this._textures;

    /// <summary>
    /// Gets the width of the range of distances encoded in the distance field, in atlas texels, or
    /// <c>null</c> for <see cref="GlyphFormat.Bitmap"/> fonts.
    /// </summary>
    internal float? DistanceRange
    {
        get;
    }

    /// <summary>
    /// Gets the signed distance, in atlas texels, encoded at the middle of the distance range,
    /// with positive distances inside the glyphs. It is nonzero only for fonts built with a
    /// <c>PxRangeOffset</c>.
    /// </summary>
    internal float DistanceRangeMiddle
    {
        get;
    }

    /// <summary>
    /// Gets whether the font has kerning pairs (SpriteFontContent <c>Kerning="true"</c>).
    /// </summary>
    internal bool HasKerning => this._kerningKeys.Length != 0;

    /// <summary>
    /// Gets the code points that the font has glyphs for.
    /// </summary>
    public IReadOnlyCollection<int> Characters => this._glyphs.Keys;

    /// <summary>
    /// Gets or sets the character drawn in place of characters that the font has no glyph for, or
    /// <c>null</c> to draw nothing for them. Defaults to U+FFFD (replacement character) or '?'
    /// when the font has one of them. Control characters and invisible formatting characters
    /// (such as the variation selector U+FE0F and zero-width joiners) draw nothing instead.
    /// </summary>
    internal int? DefaultCharacter
    {
        get => this._defaultCharacter;
        set
        {
            SpriteFontGlyph? glyph = null;
            if (value is int codePoint && !this._glyphs.TryGetValue(codePoint, out glyph))
            {
                throw new ArgumentException($"The font has no glyph for U+{codePoint:X4}.", nameof(value));
            }

            this._defaultCharacter = value;
            this._defaultGlyph = glyph;
        }
    }

    /// <summary>
    /// Gets the glyph for the specified code point, if the font has one.
    /// </summary>
    internal bool TryGetGlyph(
        int codePoint,
        [NotNullWhen(true)] out SpriteFontGlyph? glyph)
    {
        return this._glyphs.TryGetValue(codePoint, out glyph);
    }

    /// <summary>
    /// Gets the kerning adjustment added to the advance of <paramref name="first"/> when it is
    /// followed by <paramref name="second"/>, or zero when the pair has none.
    /// </summary>
    internal float GetKerning(
        int first,
        int second)
    {
        if (this._kerningKeys.Length == 0)
        {
            return 0;
        }

        var index = Array.BinarySearch(this._kerningKeys, KerningKey(first, second));
        return index >= 0 ? this._kerningValues[index] : 0;
    }

    /// <summary>
    /// Measures the advance width and line height of a single code point.
    /// </summary>
    /// <param name="tracking">
    /// The additional spacing applied after each character, in unscaled font units. Zero
    /// uses the spacing defined by the font; positive values loosen it and negative values
    /// tighten it. This is uniform spacing, unlike the pair-specific adjustment of kerning.
    /// </param>
    /// <param name="codePoint">The character.</param>
    public Vector2 MeasureString(
        int codePoint,
        float tracking = 0)
    {
        if (this.TryGetDrawnGlyph(codePoint, out var glyph))
        {
            return new Vector2(glyph.Advance + tracking, this.LineHeight);
        }

        return new Vector2(0, this.LineHeight);
    }

    /// <summary>
    /// Measures the width of the widest line of the specified text and the total height of
    /// its lines, which are separated by line feed characters.
    /// </summary>
    /// <param name="tracking">
    /// The additional spacing applied after each character, in unscaled font units. Zero
    /// uses the spacing defined by the font; positive values loosen it and negative values
    /// tighten it. This is uniform spacing, unlike the pair-specific adjustment of kerning.
    /// </param>
    /// <param name="text">The text.</param>
    public Vector2 MeasureString(
        ReadOnlySpan<char> text,
        float tracking = 0)
    {
        var state = default(MeasureState);
        this.MeasureSpan(text, tracking, ref state);
        return state.GetSize(this.LineHeight);
    }

    /// <inheritdoc cref="MeasureString(ReadOnlySpan{char}, float)"/>
    public Vector2 MeasureString(
        StringBuilder text,
        float tracking = 0)
    {
        ArgumentNullException.ThrowIfNull(text);

        int length = text.Length;
        char[]? rented = null;
        Span<char> copy = (length <= MaxStackTextLength) ? stackalloc char[MaxStackTextLength] : (rented = ArrayPool<char>.Shared.Rent(length));
        copy = copy[..length];
        text.CopyTo(0, copy, length);

        var size = this.MeasureString((ReadOnlySpan<char>)copy, tracking);

        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }

        return size;
    }

    // The longest StringBuilder that's copied to the stack to measure or draw it
    private const int MaxStackTextLength = 256;

    private struct MeasureState
    {
        public float LineWidth;
        public float MaxWidth;
        public int LineCount;
        public int Previous;

        public readonly Vector2 GetSize(
            float lineHeight)
        {
            return new Vector2(MathF.Max(this.LineWidth, this.MaxWidth), lineHeight * (this.LineCount + 1));
        }
    }

    private void MeasureSpan(
        ReadOnlySpan<char> text,
        float tracking,
        ref MeasureState state)
    {
        foreach (var codePoint in text.EnumerateCodePoints())
        {
            if (codePoint == '\n')
            {
                state.MaxWidth = MathF.Max(state.MaxWidth, state.LineWidth);
                state.LineWidth = 0;
                state.LineCount++;
                state.Previous = 0;
                continue;
            }

            state.LineWidth += this.MeasureNext(codePoint, tracking, ref state.Previous);
        }
    }

    /// <summary>
    /// How much wider a code point makes text whose last glyph is <paramref name="previous"/>, for measuring
    /// text as it goes, without measuring what's before it again.
    /// </summary>
    internal float MeasureNext(
        int codePoint,
        float tracking,
        ref int previous)
    {
        if (!this.TryGetDrawnGlyph(codePoint, out var glyph))
        {
            return 0f;
        }

        float kerning = (previous != 0) ? this.GetKerning(previous, glyph.CodePoint) : 0f;
        previous = glyph.CodePoint;
        return kerning + glyph.Advance + tracking;
    }

    /// <summary>
    /// Draws the specified text in a single color.
    /// </summary>
    /// <param name="spriteBatch">
    /// The batch to draw into.
    /// </param>
    /// <param name="position">The position of <paramref name="origin"/> on the screen.</param>
    /// <param name="color">The color of the text's face.</param>
    /// <param name="opacity">
    /// The amount by which to fade the text and its effects, where 1.0 draws them in their own
    /// colors. This is kept separate from <paramref name="color"/> so that a color that is already
    /// semi-transparent can be faded without the caller combining the two.
    /// </param>
    /// <param name="rotation">The rotation of the text around <paramref name="origin"/>.</param>
    /// <param name="origin">
    /// The point of the text, in unscaled font units from the top-left corner of the first line,
    /// that is drawn at <paramref name="position"/> and that the text rotates and scales around.
    /// </param>
    /// <param name="scale">
    /// The scale of the text, or <c>null</c> for none; <c>pixelSize / EmSize</c> draws the text
    /// at <c>pixelSize</c> pixels per em.
    /// </param>
    /// <param name="tracking">
    /// The additional spacing applied after each character, in unscaled font units. Zero
    /// uses the spacing defined by the font; positive values loosen it and negative values
    /// tighten it. It is added to each character's advance before <paramref name="scale"/>
    /// is applied, so the spacing scales along with the glyphs. This is uniform spacing,
    /// unlike the pair-specific adjustment of kerning.
    /// </param>
    /// <param name="effects">
    /// The text's effects, or <c>null</c> for none. Every glyph's shadow is drawn first, then
    /// every glyph's glow, outline and face, so no glyph's effects cover another's face.
    /// </param>
    /// <param name="layers">
    /// Which layers to draw: all of them by default. Text drawn in several calls (lines, or
    /// runs) keeps its effects behind every face by drawing each call's
    /// <see cref="TextLayers.Effects"/>, then each call's <see cref="TextLayers.Face"/>.
    /// </param>
    /// <param name="text">The text.</param>
    internal void DrawString(
        SpriteBatch spriteBatch,
        ReadOnlySpan<char> text,
        Vector2 position,
        Color color,
        float opacity = 1,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        float tracking = 0,
        TextEffects? effects = null,
        TextLayers layers = TextLayers.All)
    {
        var args = CreateDrawArgs(position, color, opacity, rotation, origin, scale, tracking, effects, layers);
        this.DrawString(spriteBatch, text, default(DefaultCharacterAttributes), CharacterCursor.Start, in args);
    }

    /// <inheritdoc cref="DrawString(SpriteBatch, ReadOnlySpan{char}, Vector2, Color, float, Angle, Vector2, Nullable{Vector2}, float, TextEffects?, TextLayers)"/>
    internal void DrawString(
        SpriteBatch spriteBatch,
        StringBuilder text,
        Vector2 position,
        Color color,
        float opacity = 1,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        float tracking = 0,
        TextEffects? effects = null,
        TextLayers layers = TextLayers.All)
    {
        var args = CreateDrawArgs(position, color, opacity, rotation, origin, scale, tracking, effects, layers);
        this.DrawString(spriteBatch, text, default(DefaultCharacterAttributes), CharacterCursor.Start, in args);
    }

    // Gathers the arguments of a DrawString call for the methods that draw it, which take them
    // by reference (see TextDrawArgs). The generic DrawString methods outside this class build
    // theirs in place, so that no call from a generic instance passes these as parameters.
    private static TextDrawArgs CreateDrawArgs(
        Vector2 position,
        Color color,
        float opacity,
        Angle rotation,
        Vector2 origin,
        Vector2? scale,
        float tracking,
        TextEffects? effects,
        TextLayers layers)
    {
        return new TextDrawArgs
        {
            Position = position,
            Color = color,
            Opacity = opacity,
            Rotation = rotation,
            Origin = origin,
            Scale = scale ?? Vector2.One,
            Tracking = tracking,
            Effects = effects,
            Layers = layers,
        };
    }

    /// <summary>
    /// Draws the specified text, letting the attributes of individual characters override
    /// the specified color.
    /// </summary>
    /// <param name="spriteBatch">
    /// The batch to draw into.
    /// </param>
    /// <param name="characterAttributes">
    /// The attributes of the individual characters. An attribute that the implementation
    /// does not describe keeps the value passed to this method, so attributes that describe
    /// only (for example) a per-glyph translation still draw the text in
    /// <see cref="TextDrawArgs.Color"/>. Colors apply to the face; each effect keeps its own color.
    /// </param>
    /// <param name="cursorOffset">
    /// The position of the first character of <paramref name="text"/> within the text that
    /// <paramref name="characterAttributes"/> describes, or <see cref="CharacterCursor.Start"/>
    /// if they describe exactly the text being drawn. This is what lets a longer text be
    /// drawn in pieces, one wrapped line at a time, while a single set of attributes
    /// describes the whole of it.
    /// </param>
    /// <param name="args">
    /// How to draw it: the position, color, opacity, rotation, origin, scale, tracking, effects
    /// and layers that the public <c>DrawString</c> methods take, as the overload without
    /// attributes describes them. A color that the attributes give a character replaces
    /// <see cref="TextDrawArgs.Color"/>; the opacity still applies to it.
    /// </param>
    /// <param name="text">The text.</param>
    internal void DrawString<TCharacterAttributes>(
        SpriteBatch spriteBatch,
        StringBuilder text,
        TCharacterAttributes characterAttributes,
        in CharacterCursor cursorOffset,
        in TextDrawArgs args)
        where TCharacterAttributes : ICharacterAttributes
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);

        if ((text is null) || (text.Length == 0))
        {
            return;
        }

        int length = text.Length;
        char[]? rented = null;
        Span<char> copy = (length <= MaxStackTextLength) ? stackalloc char[MaxStackTextLength] : (rented = ArrayPool<char>.Shared.Rent(length));
        copy = copy[..length];
        text.CopyTo(0, copy, length);

        this.DrawString(spriteBatch, (ReadOnlySpan<char>)copy, characterAttributes, in cursorOffset, in args);

        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <inheritdoc cref="DrawString{TCharacterAttributes}(SpriteBatch, StringBuilder, TCharacterAttributes, in CharacterCursor, in TextDrawArgs)"/>
    internal void DrawString<TCharacterAttributes>(
        SpriteBatch spriteBatch,
        ReadOnlySpan<char> text,
        TCharacterAttributes characterAttributes,
        in CharacterCursor cursorOffset,
        in TextDrawArgs args)
        where TCharacterAttributes : ICharacterAttributes
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);

        if (text.Length == 0)
        {
            return;
        }

        // NOTE: Once per call, never per glyph. Nothing draws until a sprite needs the change
        using var shader = spriteBatch.UseShader(this._shader);

        // (A few outlines fit on the stack; more than that is unusual)
        int most = TextLayer.GetMaxCount(args.Effects);
        Span<TextLayer> drawn = (most <= MaxStackLayers) ? stackalloc TextLayer[MaxStackLayers] : new TextLayer[most];
        var layerCount = this.GetLayers(args.Effects, drawn, args.Layers);
        for (int i = 0; i < layerCount; i++)
        {
            var state = new DrawState(cursorOffset);
            this.DrawSpan(spriteBatch, text, ref characterAttributes, ref state, in drawn[i], in args);
        }
    }

    // The miter limit of the face and the shadow, which follow the glyphs' corners as an
    // outline's default does
    private const float FaceMiterLimit = 4;

    // The most layers drawn without an array
    private const int MaxStackLayers = 12;

    // A miter limit the shader treats as unlimited, kept within half-float range
    private const float UnlimitedMiterLimit = 1024;

    /// <summary>
    /// Fills <paramref name="layers"/> with the passes that draw text with the specified effects,
    /// in drawing order, and returns how many there are.
    /// </summary>
    /// <param name="include">The layers to draw; the others are left out.</param>
    /// <param name="effects">The effects, or <c>null</c> for none.</param>
    /// <param name="layers">Where the passes go.</param>
    internal int GetLayers(
        TextEffects? effects,
        Span<TextLayer> layers,
        TextLayers include = TextLayers.All)
    {
        bool drawFace = (include & TextLayers.Face) != 0;
        if ((effects is null) || ReferenceEquals(effects, TextEffects.None))
        {
            if (!drawFace)
            {
                return 0;
            }

            layers[0] = this._plainFace;
            return 1;
        }

        if (this.GlyphFormat == GlyphFormat.Bitmap)
        {
            if ((effects.Shadow is not null) || (effects.Glow is not null) || (effects.Outline is not null)
                || (effects.Embolden != 0) || (effects.Softness != 0))
            {
                ReportOnce(ref this._reportedBitmapEffects, 0x6e2b91d4,
                    "Effects aren't drawn for bitmap fonts; only the face is.");
            }

            if (!drawFace)
            {
                return 0;
            }

            layers[0] = this._plainFace;
            return 1;
        }

        int count = 0;
        var embolden = effects.Embolden;

        if ((effects.Shadow is { } shadow) && ((include & TextLayers.Shadow) != 0))
        {
            layers[count++] = new TextLayer(isFace: false, shadow.Color, shadow.Offset,
                this.GetLayerData(embolden + shadow.Spread, shadow.Softness, FaceMiterLimit));
        }

        if ((effects.Glow is { } glow) && ((include & TextLayers.Glow) != 0))
        {
            layers[count++] = new TextLayer(isFace: false, glow.Color, Vector2.Zero,
                this.GetLayerData(embolden + glow.Width, glow.Softness, 1));
        }

        if ((include & TextLayers.Outline) != 0)
        {
            if (effects.Outlines is { } outlines)
            {
                for (int i = 0; i < outlines.Count; i++)
                {
                    layers[count++] = this.GetOutlineLayer(outlines[i], embolden);
                }
            }

            if (effects.Outline is { } outline)
            {
                layers[count++] = this.GetOutlineLayer(outline, embolden);
            }
        }

        if (drawFace)
        {
            layers[count++] = new TextLayer(isFace: true, default, Vector2.Zero,
                this.GetLayerData(embolden, effects.Softness, FaceMiterLimit));
        }

        return count;
    }

    private TextLayer GetOutlineLayer(
        in TextOutline outline,
        float embolden)
    {
        var miterLimit = (outline.Corners == TextCorners.Round) ? 1 : outline.MiterLimit;
        return new TextLayer(isFace: false, outline.Color, outline.Offset,
            this.GetLayerData(embolden + outline.Width, outline.Softness, miterLimit), outline.Gradient);
    }

    /// <summary>
    /// Gets the settings each quad of a layer carries to the text shader (see
    /// <c>spritefont.slang</c>), keeping the layer inside the distances the atlas encodes.
    /// </summary>
    /// <param name="embolden">How far the layer's edges move outward from the glyphs', in pixels of the font's native size.</param>
    /// <param name="softness">How much wider than a screen pixel the layer's edge fades.</param>
    /// <param name="miterLimit">How many times the emboldening a corner may extend; 1 is round.</param>
    internal ShaderCustomData GetLayerData(
        float embolden,
        float softness,
        float miterLimit)
    {
        if (this.DistanceRange is not float distanceRange)
        {
            // A bitmap font: the shader uses the texture's alpha
            return default;
        }

        var middle = this.DistanceRangeMiddle;

        // The edge, and the pixel of anti-aliasing around it, has to stay inside the encoded
        // range, but the true outline is never limited. The atlas is rendered at the font's em
        // size, so a font pixel is an atlas texel.
        var limit = MathF.Max((distanceRange / 2) - 1, MathF.Abs(middle));
        var clampedSoftness = Math.Clamp(softness, 0, 2 * limit);
        var reach = MathF.Max(limit - (clampedSoftness / 2), MathF.Abs(middle));

        // The middle of the range recenters an asymmetric range on the true outline, and the
        // emboldening moves the edges from there
        var distanceOffset = Math.Clamp(middle + embolden, -reach, reach);

        if ((distanceOffset != middle + embolden) || (clampedSoftness != softness))
        {
            ReportOnce(ref this._reportedClamp, 0x3fa7c260,
                $"An effect reaches beyond the distances the atlas encodes (distance range {distanceRange}); it's drawn as far as they go. Build the font with a distance range of at least twice the widest effect.");
        }

        var signedRange = (this.GlyphFormat == GlyphFormat.Mtsdf) ? -distanceRange : distanceRange;

        return new ShaderCustomData(
            distanceOffset,
            clampedSoftness,
            Math.Clamp(float.IsNaN(miterLimit) ? 1 : miterLimit, 1, UnlimitedMiterLimit),
            signedRange);
    }

    private static void ReportOnce(
        ref bool reported,
        uint tag,
        string message)
    {
        if (!reported)
        {
            reported = true;
            DotGame.Framework.Diagnostics.Trace.WriteDebug(tag, message);
        }
    }

    private struct DrawState
    {
        public float PenX;
        public float LineTop;
        public int Previous;

        // Counting from the offset rather than from zero is what positions the attributes
        // within the whole text, and it costs nothing for each glyph.
        public int CharIndex;
        public int GlyphIndex;

        public DrawState(
            in CharacterCursor cursorOffset)
        {
            this.CharIndex = cursorOffset.CharIndex;
            this.GlyphIndex = cursorOffset.GlyphIndex;
        }
    }

    private void DrawSpan<TCharacterAttributes>(
        SpriteBatch spriteBatch,
        ReadOnlySpan<char> text,
        ref TCharacterAttributes characterAttributes,
        ref DrawState state,
        in TextLayer layer,
        in TextDrawArgs args)
        where TCharacterAttributes : ICharacterAttributes
    {
        var position = args.Position;
        var color = args.Color;
        var opacity = args.Opacity;
        var rotation = args.Rotation;
        var origin = args.Origin;
        var scale = args.Scale;
        var tracking = args.Tracking;
        var baseline = this.Ascender;
        var layerOrigin = origin - layer.Offset;
        var layerData = layer.Data;
        var effectColor = layer.Color * opacity;

        // The layer's two colors, if it has them: the face's, or an outline's own
        var gradient = layer.IsFace ? args.Effects?.Gradient : layer.Gradient;

        // The band of light across the face, if it has one that shows
        var sheen = layer.IsFace ? args.Effects?.Sheen : null;
        if ((sheen is { } unlit) && (!(unlit.Width > 0) || !(unlit.Strength > 0) || float.IsNaN(unlit.Position)))
        {
            sheen = null;
        }

        foreach (var codePoint in text.EnumerateCodePoints())
        {
            if (codePoint == '\n')
            {
                state.LineTop += this.LineHeight;
                state.PenX = 0;
                state.Previous = 0;
            }
            else if (this.TryGetDrawnGlyph(codePoint, out var glyph))
            {
                if (state.Previous != 0)
                {
                    state.PenX += this.GetKerning(state.Previous, glyph.CodePoint);
                }

                if (glyph.SourceRectangle.Width > 0)
                {
                    // NOTE: The cursor is absolute, so characters that draw nothing can be
                    // skipped without the attributes losing track of their position.
                    // (The middle of its advance, on the baseline: where it's turned about)
                    var pivot = new Vector2(state.PenX + (glyph.Advance * 0.5f), state.LineTop + baseline);
                    characterAttributes.MoveTo(new CharacterCursor(state.CharIndex, state.GlyphIndex, pivot.X));

                    // The face's color belongs to the caller unless the attributes override it,
                    // so attributes that describe something else leave the text as it was drawn.
                    // An effect keeps its own color, so a highlighted word keeps its outline.
                    var ownColor = characterAttributes.Color;
                    var glyphColor = layer.IsFace ? (ownColor ?? color) * opacity : effectColor;

                    // Where the attributes put the character. Every layer applies it alike, so
                    // a character's shadow, outline and face move and turn as one.
                    var translation = characterAttributes.Translation;
                    var turn = characterAttributes.Rotation;
                    bool isMoved = (translation != Vector2.Zero) || (turn.Radians != 0);

                    // Each glyph is drawn at the text's position with the text's origin moved
                    // to the glyph, so SpriteBatch rotates and scales the whole text as one.
                    var glyphOffset = new Vector2(state.PenX + glyph.Left, state.LineTop + baseline + glyph.Top);

                    if ((sheen is { } light) && SheenTouches(light, glyph, glyphOffset, state.LineTop + baseline))
                    {
                        this.DrawSheenGlyph(spriteBatch, glyph, glyphOffset, pivot, translation, turn, state.LineTop + baseline, glyphColor, (ownColor is null) ? gradient : null, light, layerOrigin, in layerData, in args);
                    }
                    else if ((gradient is { } colors) && ((ownColor is null) || !layer.IsFace))
                    {
                        this.DrawGradientGlyph(spriteBatch, glyph, glyphOffset, pivot, translation, turn, state.LineTop + baseline, colors, layerOrigin, in layerData, in args);
                    }
                    else if (isMoved)
                    {
                        // The glyph turns about its pivot, so the pivot is its origin, and is
                        // put where the text's own rotation and scale carry the moved pivot
                        var place = (pivot + translation - layerOrigin) * scale;
                        if (rotation.Radians != 0)
                        {
                            (float sin, float cos) = rotation.SinCos;
                            place = new Vector2((place.X * cos) - (place.Y * sin), (place.X * sin) + (place.Y * cos));
                        }

                        spriteBatch.Draw(glyph.Texture,
                        position + place,
                        glyphColor,
                        rotation + turn,
                        pivot - glyphOffset,
                        scale,
                        glyph.SourceRectangle,
                        customData: in layerData);
                    }
                    else
                    {
                        spriteBatch.Draw(glyph.Texture,
                        position,
                        glyphColor,
                        rotation,
                        layerOrigin - glyphOffset,
                        scale,
                        glyph.SourceRectangle,
                        customData: in layerData);
                    }
                }

                state.PenX += glyph.Advance + tracking;
                state.Previous = glyph.CodePoint;
            }

            state.CharIndex += codePoint.Utf16SequenceLength();
            state.GlyphIndex++;
        }
    }

    /// <summary>
    /// Draws a glyph of the face in the gradient's two colors. A sprite has one color, so the
    /// glyph is drawn as triangles, in up to three bands down its rectangle: what's above the
    /// gradient in the top color, the gradient itself, and what's below it in the bottom color.
    /// (A glyph's rectangle is larger than the glyph by the distance range, so blending between
    /// its own corners would squeeze the gradient into the middle of every letter.)
    /// </summary>
    private void DrawGradientGlyph(
        SpriteBatch spriteBatch,
        SpriteFontGlyph glyph,
        Vector2 glyphOffset,
        Vector2 pivot,
        Vector2 translation,
        Angle turn,
        float baselineY,
        TextGradient colors,
        Vector2 layerOrigin,
        in ShaderCustomData layerData,
        in TextDrawArgs args)
    {
        var source = glyph.SourceRectangle;
        float left = glyphOffset.X;
        float right = left + source.Width;
        float top = glyphOffset.Y;
        float bottom = top + source.Height;

        // Where the gradient begins and ends on this line, and so within this rectangle
        float rise = (this.CapHeight > 0) ? this.CapHeight : this.Ascender;
        float gradientTop = baselineY - rise;
        float first = Math.Clamp(gradientTop, top, bottom);
        float last = Math.Clamp(baselineY, top, bottom);

        var topColor = colors.Top * args.Opacity;
        var bottomColor = colors.Bottom * args.Opacity;
        var firstColor = Color.Lerp(topColor, bottomColor, (first - gradientTop) / rise);
        var lastColor = Color.Lerp(topColor, bottomColor, (last - gradientTop) / rise);

        float invWidth = 1f / glyph.Texture.Size.X;
        float invHeight = 1f / glyph.Texture.Size.Y;
        float u0 = source.X * invWidth;
        float u1 = (source.X + source.Width) * invWidth;

        (float turnSin, float turnCos) = turn.SinCos;
        (float sin, float cos) = args.Rotation.SinCos;
        var position = args.Position;
        var scale = args.Scale;

        // A point of the glyph's rectangle, in the text's space, to where it's drawn: turned
        // about the pivot and moved, then scaled and rotated about the text's origin
        Vector2 Place(float x, float y)
        {
            float dx = x - pivot.X;
            float dy = y - pivot.Y;
            float px = (pivot.X + translation.X + (dx * turnCos) - (dy * turnSin) - layerOrigin.X) * scale.X;
            float py = (pivot.Y + translation.Y + (dx * turnSin) + (dy * turnCos) - layerOrigin.Y) * scale.Y;
            return new Vector2(position.X + (px * cos) - (py * sin), position.Y + (px * sin) + (py * cos));
        }

        Span<Vertex> vertices = stackalloc Vertex[18];
        int count = 0;

        void Band(Span<Vertex> into, float from, float to, Color fromColor, Color toColor)
        {
            if (to <= from)
            {
                return;
            }

            float v0 = (source.Y + (from - top)) * invHeight;
            float v1 = (source.Y + (to - top)) * invHeight;
            var a = new Vertex(Place(left, from), new Vector2(u0, v0), fromColor);
            var b = new Vertex(Place(right, from), new Vector2(u1, v0), fromColor);
            var c = new Vertex(Place(right, to), new Vector2(u1, v1), toColor);
            var d = new Vertex(Place(left, to), new Vector2(u0, v1), toColor);
            into[count++] = a;
            into[count++] = b;
            into[count++] = c;
            into[count++] = a;
            into[count++] = c;
            into[count++] = d;
        }

        Band(vertices, top, first, topColor, topColor);
        Band(vertices, first, last, firstColor, lastColor);
        Band(vertices, last, bottom, bottomColor, bottomColor);

        spriteBatch.DrawTriangles(glyph.Texture, vertices[..count], in layerData);
    }

    /// <summary>
    /// Places a band of light on this text by how far across it the band has come: at 0 it is
    /// just off the start of the text's widest line, touching no letter, and at 1 just off its
    /// end. Draw the text each frame with <paramref name="progress"/> a little greater and the
    /// light crosses it, with no measuring by the caller.
    /// </summary>
    /// <param name="text">The text the band will cross.</param>
    /// <param name="progress">How far across: 0 to 1, and beyond either for further off.</param>
    /// <param name="sheen">The band: its color, width, angle, strength and softness. Its position is replaced.</param>
    /// <param name="tracking">The tracking the text is drawn with.</param>
    /// <returns>The band at that place, for <see cref="TextEffects.Sheen"/>.</returns>
    public TextSheen SheenAcross(
        ReadOnlySpan<char> text,
        float progress,
        TextSheen sheen,
        float tracking = 0)
    {
        return sheen.Across(progress, this.MeasureString(text, tracking).X, this.Ascender, Math.Max(this.LineHeight - this.Ascender, 0));
    }

    /// <summary>
    /// How far a point of a line is from the middle of the band of light, along the line: the
    /// band leans, so the middle is further along the higher the point is above the baseline.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SheenDistance(
        float slant,
        float position,
        float x,
        float y,
        float baselineY)
    {
        return x - (slant * (baselineY - y)) - position;
    }

    /// <summary>
    /// Whether any of the band of light falls on a glyph's rectangle.
    /// </summary>
    private static bool SheenTouches(
        in TextSheen sheen,
        SpriteFontGlyph glyph,
        Vector2 glyphOffset,
        float baselineY)
    {
        float half = sheen.Width * 0.5f;
        float slant = sheen.Slant;
        float left = glyphOffset.X;
        float right = left + glyph.SourceRectangle.Width;
        float top = glyphOffset.Y;
        float bottom = top + glyph.SourceRectangle.Height;

        // (The distance is linear, so its least and greatest are at corners)
        float a = SheenDistance(slant, sheen.Position, left, top, baselineY);
        float b = SheenDistance(slant, sheen.Position, right, top, baselineY);
        float c = SheenDistance(slant, sheen.Position, right, bottom, baselineY);
        float d = SheenDistance(slant, sheen.Position, left, bottom, baselineY);
        float least = Math.Min(Math.Min(a, b), Math.Min(c, d));
        float greatest = Math.Max(Math.Max(a, b), Math.Max(c, d));
        return (least < half) && (greatest > -half);
    }

    /// <summary>
    /// Draws a glyph of the face that the band of light falls on. The light is strongest along
    /// the band's middle and fades in a straight line to each edge, so the glyph's rectangle is
    /// cut along the two edges and the middle (and, with a gradient, across where the gradient
    /// begins and ends): within each piece the color changes evenly, which is what colors at
    /// the corners of triangles can draw.
    /// </summary>
    private void DrawSheenGlyph(
        SpriteBatch spriteBatch,
        SpriteFontGlyph glyph,
        Vector2 glyphOffset,
        Vector2 pivot,
        Vector2 translation,
        Angle turn,
        float baselineY,
        Color glyphColor,
        TextGradient? gradient,
        TextSheen sheen,
        Vector2 layerOrigin,
        in ShaderCustomData layerData,
        in TextDrawArgs args)
    {
        var source = glyph.SourceRectangle;
        float left = glyphOffset.X;
        float right = left + source.Width;
        float top = glyphOffset.Y;
        float bottom = top + source.Height;

        // The face's own color at a height: the gradient's, or the one color it has
        float rise = (this.CapHeight > 0) ? this.CapHeight : this.Ascender;
        float gradientTop = baselineY - rise;
        var topColor = glyphColor;
        var bottomColor = glyphColor;
        float first = top;
        float last = top;
        if (gradient is { } colors)
        {
            topColor = colors.Top * args.Opacity;
            bottomColor = colors.Bottom * args.Opacity;
            first = Math.Clamp(gradientTop, top, bottom);
            last = Math.Clamp(baselineY, top, bottom);
        }

        var lightColor = sheen.Color * args.Opacity;
        float slant = sheen.Slant;
        float strength = Math.Clamp(sheen.Strength, 0f, 1f);
        float half = sheen.Width * 0.5f;
        float plateau = sheen.Plateau;
        float fade = half - plateau;

        float invWidth = 1f / glyph.Texture.Size.X;
        float invHeight = 1f / glyph.Texture.Size.Y;

        (float turnSin, float turnCos) = turn.SinCos;
        (float sin, float cos) = args.Rotation.SinCos;
        var position = args.Position;
        var scale = args.Scale;

        // A point of the glyph's rectangle, in the text's space, as a corner of a triangle:
        // where it's drawn (turned about the pivot and moved, then scaled and rotated about
        // the text's origin), its place in the atlas, and its color under the light
        Vertex Corner(Vector2 point)
        {
            float dx = point.X - pivot.X;
            float dy = point.Y - pivot.Y;
            float px = (pivot.X + translation.X + (dx * turnCos) - (dy * turnSin) - layerOrigin.X) * scale.X;
            float py = (pivot.Y + translation.Y + (dx * turnSin) + (dy * turnCos) - layerOrigin.Y) * scale.Y;
            var place = new Vector2(position.X + (px * cos) - (py * sin), position.Y + (px * sin) + (py * cos));

            var coordinate = new Vector2((source.X + (point.X - left)) * invWidth, (source.Y + (point.Y - top)) * invHeight);

            var own = Color.Lerp(topColor, bottomColor, (point.Y - gradientTop) / rise);
            float lit = Math.Clamp((half - MathF.Abs(SheenDistance(slant, sheen.Position, point.X, point.Y, baselineY))) / fade, 0f, 1f) * strength;
            return new Vertex(place, coordinate, Color.Lerp(own, lightColor, lit));
        }

        // The rows of the rectangle (above the gradient, the gradient, below it), each cut into
        // what's before the band, its rising edge, its middle at full strength (which a band
        // that fades all the way from its middle hasn't), its falling edge and what's after it
        Span<float> rows = [top, first, last, bottom];
        Span<float> cuts = [float.NegativeInfinity, -half, -plateau, plateau, half, float.PositiveInfinity];
        Span<Vector2> piece = stackalloc Vector2[8];
        Span<Vector2> spare = stackalloc Vector2[8];
        Span<Vertex> vertices = stackalloc Vertex[18];

        for (int row = 0; row < 3; row++)
        {
            float from = rows[row];
            float to = rows[row + 1];
            if (to <= from)
            {
                continue;
            }

            for (int cut = 0; cut < 5; cut++)
            {
                if (cuts[cut] >= cuts[cut + 1])
                {
                    continue;
                }

                piece[0] = new Vector2(left, from);
                piece[1] = new Vector2(right, from);
                piece[2] = new Vector2(right, to);
                piece[3] = new Vector2(left, to);
                int corners = 4;

                if (!float.IsInfinity(cuts[cut]))
                {
                    corners = ClipToSheen(piece[..corners], spare, sheen, baselineY, cuts[cut], keepBeyond: true);
                    spare[..corners].CopyTo(piece);
                }

                if (!float.IsInfinity(cuts[cut + 1]))
                {
                    corners = ClipToSheen(piece[..corners], spare, sheen, baselineY, cuts[cut + 1], keepBeyond: false);
                    spare[..corners].CopyTo(piece);
                }

                if (corners < 3)
                {
                    continue;
                }

                // A fan of triangles from the piece's first corner (it has no hollows)
                var hub = Corner(piece[0]);
                var previous = Corner(piece[1]);
                int count = 0;
                for (int i = 2; i < corners; i++)
                {
                    var next = Corner(piece[i]);
                    vertices[count++] = hub;
                    vertices[count++] = previous;
                    vertices[count++] = next;
                    previous = next;
                }

                spriteBatch.DrawTriangles(glyph.Texture, vertices[..count], in layerData);
            }
        }
    }

    /// <summary>
    /// Keeps the part of a shape that is on one side of a line parallel to the band of light:
    /// at <paramref name="distance"/> from the band's middle or beyond it, or up to it. Returns
    /// how many corners the part has.
    /// </summary>
    private static int ClipToSheen(
        ReadOnlySpan<Vector2> shape,
        Span<Vector2> into,
        in TextSheen sheen,
        float baselineY,
        float distance,
        bool keepBeyond)
    {
        // (Nothing is left of a shape that an earlier line cut away altogether)
        if (shape.IsEmpty)
        {
            return 0;
        }

        float slant = sheen.Slant;

        int count = 0;
        var from = shape[^1];
        float fromSide = SheenDistance(slant, sheen.Position, from.X, from.Y, baselineY) - distance;
        if (!keepBeyond)
        {
            fromSide = -fromSide;
        }

        foreach (var to in shape)
        {
            float toSide = SheenDistance(slant, sheen.Position, to.X, to.Y, baselineY) - distance;
            if (!keepBeyond)
            {
                toSide = -toSide;
            }

            // (An edge that crosses the line adds the point where it does)
            if ((fromSide >= 0) != (toSide >= 0))
            {
                into[count++] = from + ((to - from) * (fromSide / (fromSide - toSide)));
            }

            if (toSide >= 0)
            {
                into[count++] = to;
            }

            from = to;
            fromSide = toSide;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetDrawnGlyph(
        int codePoint,
        out SpriteFontGlyph glyph)
    {
        SpriteFontGlyph? found;
        if ((uint)codePoint < AsciiGlyphCount)
        {
            found = this._asciiGlyphs[codePoint];
        }
        else
        {
            this._glyphs.TryGetValue(codePoint, out found);
        }

        // Control characters such as carriage return and tab, and invisible formatting
        // characters such as the emoji variation selector in "©️", draw nothing rather than
        // the default character.
        found ??= ((codePoint >= 0x20) && !((codePoint >= 0x7F) && (codePoint <= 0x9F)) && !IsDefaultIgnorable(codePoint)) ? this._defaultGlyph : null;

        glyph = found!;
        return found is not null;
    }

    /// <summary>
    /// Whether a code point is one that Unicode says to draw as nothing when a font has no
    /// glyph for it (Default_Ignorable_Code_Point): variation selectors, zero-width joiners
    /// and spaces, bidirectional marks, the soft hyphen, and the like.
    /// </summary>
    private static bool IsDefaultIgnorable(
        int codePoint)
    {
        return codePoint switch
        {
            0x00AD => true, // Soft hyphen
            0x034F => true, // Combining grapheme joiner
            0x061C => true, // Arabic letter mark
            0x115F or 0x1160 => true, // Hangul fillers
            0x17B4 or 0x17B5 => true, // Khmer inherent vowels
            >= 0x180B and <= 0x180F => true, // Mongolian variation selectors and vowel separator
            >= 0x200B and <= 0x200F => true, // Zero-width space, joiners, and directional marks
            >= 0x202A and <= 0x202E => true, // Bidirectional embeddings and overrides
            >= 0x2060 and <= 0x206F => true, // Word joiner, invisible operators, and isolates
            0x3164 => true, // Hangul filler
            >= 0xFE00 and <= 0xFE0F => true, // Variation selectors
            0xFEFF => true, // Zero-width no-break space (byte order mark)
            0xFFA0 => true, // Halfwidth Hangul filler
            >= 0xFFF0 and <= 0xFFF8 => true, // Unassigned specials
            >= 0x1BCA0 and <= 0x1BCA3 => true, // Shorthand format controls
            >= 0x1D173 and <= 0x1D17A => true, // Musical symbol formatting
            >= 0xE0000 and <= 0xE0FFF => true, // Tags and variation selectors supplement
            _ => false,
        };
    }

    private static ulong KerningKey(
        int first,
        int second)
    {
        return ((ulong)(uint)first << 32) | (uint)second;
    }

    void IDisposable.Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._isDisposed = true;

        // NOTE: Its shader stays loaded, as other fonts share it

        foreach (var texture in this._textures)
        {
            // NOTE: Its pages were loaded by key, so they're the content service's to release
            if (this._content is not null)
            {
                _ = this._content.UnloadAsync(texture);
            }
            else
            {
                texture.Dispose();
            }
        }
    }
}

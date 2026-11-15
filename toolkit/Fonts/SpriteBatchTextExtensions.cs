// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using System.Numerics;
using System.Text;
using DotGame.Framework;
using DotGame.Framework.Graphics;

namespace DotGame.Toolkit.Fonts;

/// <summary>
/// Draws text in a sprite batch, the way <see cref="SpriteBatch.Draw"/> draws a sprite.
/// </summary>
/// <remarks>
/// Text draws with its font's <see cref="SpriteFont.Shader"/>, whichever the batch is using, and the
/// batch goes back to its shader after it.
/// </remarks>
public static class SpriteBatchTextExtensions
{
    /// <summary>Draws text in this font.</summary>
    public static void DrawString(
        this SpriteBatch spriteBatch,
        SpriteFont font,
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
        ArgumentNullException.ThrowIfNull(font);

        font.DrawString(spriteBatch, text, position, color, opacity, rotation, origin, scale, tracking, effects, layers);
    }

    /// <summary>Draws text in this font.</summary>
    public static void DrawString(
        this SpriteBatch spriteBatch,
        SpriteFont font,
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
        ArgumentNullException.ThrowIfNull(font);

        font.DrawString(spriteBatch, text, position, color, opacity, rotation, origin, scale, tracking, effects, layers);
    }

    /// <summary>Draws text in this font.</summary>
    public static void DrawString<TCharacterAttributes>(
        this SpriteBatch spriteBatch,
        SpriteFont font,
        ReadOnlySpan<char> text,
        TCharacterAttributes characterAttributes,
        in CharacterCursor cursorOffset,
        Vector2 position,
        Color color,
        float opacity = 1,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        float tracking = 0,
        TextEffects? effects = null,
        TextLayers layers = TextLayers.All)
        where TCharacterAttributes : ICharacterAttributes
    {
        ArgumentNullException.ThrowIfNull(font);

        // NOTE: Built in place rather than by a helper, so the drawing is reached by reference
        // from a generic instance (see TextDrawArgs)
        var args = new TextDrawArgs
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

        font.DrawString(spriteBatch, text, characterAttributes, in cursorOffset, in args);
    }

    /// <summary>Draws text in this font.</summary>
    public static void DrawString<TCharacterAttributes>(
        this SpriteBatch spriteBatch,
        SpriteFont font,
        StringBuilder text,
        TCharacterAttributes characterAttributes,
        in CharacterCursor cursorOffset,
        Vector2 position,
        Color color,
        float opacity = 1,
        Angle rotation = default,
        Vector2 origin = default,
        Vector2? scale = null,
        float tracking = 0,
        TextEffects? effects = null,
        TextLayers layers = TextLayers.All)
        where TCharacterAttributes : ICharacterAttributes
    {
        ArgumentNullException.ThrowIfNull(font);

        // NOTE: Built in place rather than by a helper, so the drawing is reached by reference
        // from a generic instance (see TextDrawArgs)
        var args = new TextDrawArgs
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

        font.DrawString(spriteBatch, text, characterAttributes, in cursorOffset, in args);
    }
}

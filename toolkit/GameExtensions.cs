// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using System;
using DotGame.Toolkit.Audio;
using DotGame.Toolkit.Fonts;
using DotGame.Toolkit.Graphics;

namespace DotGame.Toolkit;

/// <summary>Adds the toolkit to a game.</summary>
public static class GameExtensions
{
    /// <summary>Registers the toolkit's content loaders, for fonts, atlases and sound banks. Call it first in <c>OnInitialize</c> if the game uses any of them: nothing does it for you.</summary>
    public static void InitializeToolkit(
        this Framework.Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        game.Content.RegisterLoader(new TextureAtlasLoader(game.Content));
        game.Content.RegisterLoader(new SoundBankLoader(game.Content));
        game.Content.RegisterLoader(new SpriteFontLoader(game.Content));
    }
}

## Getting started

Add this package beside the platform package in each of the game's heads, and in a shared library
if the game's code is there. Its content goes in a head as items, built at the game's build into
each platform's formats:

```xml
<ItemGroup>
  <SpriteFontContent Include="Content\Fonts\*.ttf" />
  <TextureAtlasContent Include="Content\Sprites\*.png" />
  <SoundBankContent Include="Content\Sounds\*.wav" />
</ItemGroup>
```

Sprite fonts are signed-distance-field fonts made from TrueType or OpenType files, so text stays sharp
at any size. A texture atlas packs a folder's sprites into one texture, and a sound bank joins many
short sounds into one file.

Its content pipeline, `DotGame.Toolkit.Build`, comes with it; don't reference it directly. It brings
`DotGame.Imaging` too, for PNG and JPEG textures made at run time.

It needs the framework's packages at exactly its own version, and nothing beyond what the framework
needs: [Getting started](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/getting-started.md),
and [Platforms](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/platforms.md) for what each platform
needs besides the .NET SDK. [Using the toolkit](https://github.com/hotchaipro/DotGame.Toolkit/blob/main/docs/guide/toolkit.md)
maps what's in it.

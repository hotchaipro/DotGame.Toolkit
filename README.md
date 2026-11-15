# DotGame.Toolkit

Higher-level helpers for [DotGame](https://github.com/hotchaipro/DotGame.Framework) games: sprite fonts
and text layout, texture atlases and nine-patches, sprite animation, particle effects, easing and
tweens, sound banks and pools, cameras and shapes. Its content pipeline builds sprite fonts, texture
atlases and sound banks as part of a normal `dotnet build`.

## Getting started

[Using the toolkit](docs/guide/toolkit.md) adds it to a game and maps what's in it.

## Documentation

- [Guides](docs/guide/toolkit.md):
  - [Text and fonts](docs/guide/text.md): sprite fonts, drawing and laying out text, outlines, shadows and glows
  - [Atlases and animation](docs/guide/atlases-and-animation.md): texture atlases, nine-patches and frame-by-frame animation
  - [Particles and easing](docs/guide/particles.md): particle effects, and easing functions for anything that moves
  - [Sound banks and pools](docs/guide/sound-banks.md): many short sounds in one file, and limiting how many play at once
  - [Cameras, scaling and shapes](docs/guide/camera-and-shapes.md): designing for one size, moving a view around a world, and drawing lines and shapes
  - [Images at run time](docs/guide/images.md): textures from PNG and JPEG images that arrive while the game runs
- [API reference](docs/api/README.md): a page for every public type
- Architecture, for contributors: [text effects](docs/architecture/text-effects.md), [sprite animation](docs/architecture/sprite-animation.md) and [particles](docs/architecture/particles.md)
- [The sample](sample/README.md): a tour of the toolkit on every platform
- [Agent skills](skills/README.md): for AI coding agents working on a game that uses the toolkit
- [DotGame.Framework](https://github.com/hotchaipro/DotGame.Framework): the framework the toolkit builds on, and its guides

## Contributing

[CONTRIBUTING.md](CONTRIBUTING.md) covers building from source, the repository's layout, tests and
pull requests.

## License

[MIT](LICENSE) © Hot Chai Games LLC

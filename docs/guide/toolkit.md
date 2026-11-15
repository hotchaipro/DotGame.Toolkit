# Using the toolkit

The toolkit is an optional layer on top of the framework, with the things most games want: text,
atlases, animation, particles, sound banks, cameras and shapes. Use as much or as little of it as you
like.

## What you need

Nothing beyond what the framework needs: the toolkit's content tools come with its package. Start
with the framework's [Getting started](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/getting-started.md),
and [Platforms](https://github.com/hotchaipro/DotGame.Framework/blob/main/docs/guide/platforms.md) has what each platform
needs besides the .NET SDK: a workload for the web, Android and iOS, Python 3 for the web on Linux,
the Android SDK and a JDK for Android, and a Mac with Xcode for iOS.

## Adding it to a game

Add the toolkit's package beside the platform's, in each platform's project, or once in the shared
`.props` file they import. It brings its own content pipeline, and the framework's content types
stay available:

```xml
<PackageReference Include="DotGame.Toolkit" Version="$(DotGameVersion)" />
```

Every DotGame package has the same version, and the toolkit needs the framework's packages at exactly
its own, so one `DotGameVersion` names them all. The `dotnet new dotgame` template sets it in the
game's `Directory.Build.props`.

Call `InitializeToolkit` in your game's `OnStart`, to register the toolkit's content loaders. It needs
`using DotGame.Toolkit;`:

```csharp
protected override async Task OnStart()
{
    this.InitializeToolkit();

    this.font = await this.Content.LoadAsync<SpriteFont>("Fonts/title");
}
```

## What's in it

| Namespace | What it has | Guide |
|---|---|---|
| `DotGame.Toolkit.Fonts` | Sprite fonts, text drawing, effects and layout | [Text and fonts](text.md) |
| `DotGame.Toolkit.Graphics` | Atlases, nine-patches, cameras, scaling, shapes and PNG images | [Atlases and animation](atlases-and-animation.md), [Cameras, scaling and shapes](camera-and-shapes.md), [Images at run time](images.md) |
| `DotGame.Toolkit.Animation` | Easing functions, tweens and sprite animation | [Particles and easing](particles.md#easing-functions), [Atlases and animation](atlases-and-animation.md) |
| `DotGame.Toolkit.Particles` | Particle effects | [Particles and easing](particles.md) |
| `DotGame.Toolkit.Audio` | Sound banks and pools | [Sound banks and pools](sound-banks.md) |
| `DotGame.Toolkit` | `Thickness` for margins and padding | |

The [API reference](../api/README.md) lists everything.

## Content types

The toolkit adds three content items. It encodes their textures and sounds with the framework's
processors, so the output suits the platform automatically.

| Item | Source | Output |
|---|---|---|
| [`SpriteFontContent`](text.md#building-a-font) | `.ttf`, `.otf`, `.ttc` | A `.spritefont` and its atlas pages |
| [`TextureAtlasContent`](atlases-and-animation.md#texture-atlases) | `.png` files | Packed atlas pages and a descriptor |
| [`SoundBankContent`](sound-banks.md#sound-banks) | `.wav` files | One sound file and a descriptor |

## In Visual Studio

The DotGame Toolkit extension for Visual Studio is optional: a game builds and runs the same without
it. It's worth installing if you edit atlases and sound banks in Visual Studio:

- Select a texture atlas's or sound bank's folder in Solution Explorer, and the Properties window shows
  the atlas's or bank's settings, such as its compression and quality. Changing one writes it to the
  item in the project file that includes the folder's files, so you don't have to find that item, keep
  the setting the same on every file, or remember each setting's name and values. It refuses a change
  that would reach beyond the folder, and says why.
- Solution Explorer gives the `Content` folder, and each atlas's and bank's folder, an icon of its own,
  so they're easy to tell from other folders.

It's for Visual Studio 2022 (17.14 or later) and Visual Studio 2026 on Windows. To install it, choose
**Extensions → Manage Extensions** in Visual Studio, search for DotGame, install DotGame Toolkit, and
restart Visual Studio.

To have Visual Studio offer it to everyone who opens your game, put a `.vsconfig` file that lists it
next to the solution. Solution Explorer then shows it as missing, with a link to install it:

```json
{
  "version": "1.0",
  "components": [],
  "extensions": [
    "https://marketplace.visualstudio.com/items?itemName=HotChaiGames.DotGameToolkit"
  ]
}
```

The [extension's README](../../vs/README.md) has the details of what it does, and how to build it.

## See also

- The [toolkit sample](../../sample/README.md), a tour with a page for each part

# DotGame Toolkit for Visual Studio

A Visual Studio extension for games made with DotGame. It gives DotGame's folders their icons in Solution
Explorer, and the Properties window of a texture atlas's or sound bank's folder its settings.

## Icons

| Folder | Icon |
| --- | --- |
| `Content` (`wwwroot\Content` in a web project) | The DotGame dot, in place of the folder, as `wwwroot` has its globe |
| A folder with `TextureAtlasContent` items | A folder with tiles |
| A folder with `SoundBankContent` items | A folder with level bars |

They apply to projects with the `DotGame` capability, which the framework's content pipeline declares.
`Images\draw_icons.py` draws them, and `Images\DotGame.imagemanifest` registers them.

## Atlas and sound bank settings

Select a texture atlas's or sound bank's folder in Solution Explorer, and the Properties window shows the
atlas's or bank's settings: the metadata of its `TextureAtlasContent` or `SoundBankContent` items that
apply to the whole atlas or bank.

| Folder | Settings |
| --- | --- |
| Texture Atlas | Atlas Name, Compression, Lossless Quality, Lossy Quality, GPU Compression, Preload |
| Sound Bank | Bank Name, Compression, Lossy Quality, Lossless Format, Lossy Format, Preload |

A folder's items are the ones Solution Explorer shows in it: the folder their `Link` puts them in (as
`LinkBase` sets it), or their own folder without one. That is also how the content pipeline groups them:
one atlas or bank per folder and `Name`.

Changing a setting writes it to the project file, on the element that includes the folder's items,
so a wildcard stays a wildcard:

```xml
<TextureAtlasContent Include="..\Content\Sprites\*.png">
    <LinkBase>Content\Sprites</LinkBase>
    <Compression>Lossy</Compression>
    <LossyQuality>7</LossyQuality>
</TextureAtlasContent>
```

- Setting a value to the pipeline's default removes it from the project file.
- The setting is removed from `Update` elements that set it on the folder's items, so the folder's value
  is the one that counts.
- Switching an atlas's Compression removes the other compression's quality, which would be an error.
- A setting whose items differ shows blank. Setting it makes them the same.
- The extension refuses a change, and says why, when it would reach beyond the folder: an element that
  also includes items of other folders, an `Update` that also sets other folders' items, or items
  declared outside the project file. A folder whose items make more than one atlas (different
  `Name`s) can't be renamed from the folder.

The extension applies only to projects with the `DotGameToolkit` capability, which the toolkit's content
pipeline declares, and only to C# projects' folders that have atlas or bank items directly in them.

## How it works

The settings are an automation extender of the folder's Properties window (the C# folder browse object),
registered by a package that loads in the background with a solution. `ContentFolder` finds a folder's
items and writes their settings with MSBuild's construction model; `ContentFolderProject` does that
under the project system's read and write locks.

The icons come from the project system (CPS): `ContentFolderIconsDataSource` watches the project's items
and publishes a new `ContentFolderIconProvider` whenever they change, which matches folders by their full
path. The images are resources of the extension's assembly, which the package's binding path lets Visual
Studio load by name.

## Installing it

The extension is published to the Visual Studio Marketplace as `HotChaiGames.DotGameToolkit`
(`publishManifest.json` gives its ID): in Visual Studio, **Extensions → Manage Extensions**, search for
DotGame, and install it. Until its first release there, build it and open the `.vsix` (below).
A game's repository can have Visual Studio offer it: a `.vsconfig` next to the solution that lists it, as
this repository's does for the sample, makes Solution Explorer show it as missing, with a link to install
it.

```json
{
  "version": "1.0",
  "components": [],
  "extensions": [
    "https://marketplace.visualstudio.com/items?itemName=HotChaiGames.DotGameToolkit"
  ]
}
```

Visual Studio only offers extensions that are on the Marketplace this way; a `.vsix` file is installed by
opening it.

## Building and trying it

It needs Visual Studio 2026 (18.x) on Windows. The extension development workload isn't needed: the
VSSDK build tools come from NuGet.

Build it with Visual Studio's MSBuild (the VSSDK's tasks run on .NET Framework) and install it in the
experimental instance:

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe" -restore -p:DeployExtension=true vs/src/DotGame.Toolkit.VisualStudio/DotGame.Toolkit.VisualStudio.csproj
```

Then start the experimental instance on the sample, and select `Content\Sprites` or `Content\Sounds` in
a platform project. Start it where `DotGameFrameworkDir` is set (a shell started after it was), or the
projects don't import the framework and the icons don't apply:

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/Common7/IDE/devenv.exe" DotGame.Toolkit.slnx /rootsuffix Exp
```

`bin\Debug\net48\DotGame.Toolkit.VisualStudio.vsix` installs it in Visual Studio itself.

## Releasing

Raise the version in `source.extension.vsixmanifest` (and the assembly's), then run the `VSIX`
workflow (`.github/workflows/vsix.yml`), which tests the extension and builds it in Release. Upload
the `.vsix` from its run on the Marketplace's page for the `HotChaiGames` publisher.

The tests of `ContentFolder` evaluate projects with MSBuild and need no Visual Studio:

```bash
dotnet test vs/test/DotGame.Toolkit.VisualStudio.Tests
```

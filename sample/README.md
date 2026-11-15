# DotGame toolkit sample

A tour of the toolkit: one page for each part of it. Each is a small,
self-contained example with comments on why, not just what.

| Page | What it shows |
|---|---|
| Text | Sprite fonts at any size; outlines, shadows, glows, emboldening and softness; colors per character; wrapping, measuring, tracking and rotation; drawing a `StringBuilder` without allocating |
| Atlases and nine-patches | A texture atlas packed from many images, and nine-patch panels at any size, one resizable |
| Sprite animation | Looping and once-only animations from atlas frames, frame cues and finish callbacks, speed, pausing, flipping, and a shared animation drawn without players |
| Sound banks and pools | Many sounds in one bank, pools that limit how many copies play at once, and part of a sound as a segment of its own |

The toolkit builds on the framework, which has its own sample of the basics
(the game class, drawing, input and audio) in the framework's `samples` folder.

It runs on every platform: `desktop` (Windows, macOS and Linux), `webgl`,
`android` and `ios`. The game code is a shared project in `shared`, which each
platform's project compiles in; a platform's project holds only its entry point
and the content it builds. They're laid out as a game's are, but build the
toolkit and the framework from their source (the clone of DotGame.Framework that
`DotGameFrameworkDir` names) where a game uses their packages: `Directory.Build.targets`
imports the toolkit's build files, which add the framework for each platform,
and references the toolkit.

```sh
dotnet run --project sample/desktop/DotGame.Toolkit.Sample.Desktop.csproj
```

Or open `DotGame.Toolkit.slnx`, which has the sample under `sample`. Move between pages with the
arrows at the top, the left and right arrow keys, Tab and Shift+Tab, or the number keys; press
`` ` `` (backtick) for stats.

## Layout

```
shared/              The game's code, shared by every platform (DotGame.Toolkit.Sample.shproj)
  ToolkitSample.cs   The game: loads the font, atlas and sound bank, switches pages
  SamplePage.cs      What a page is
  Pages/             One file per page
Content/             A font, the images packed into one atlas, and the sounds built into one bank
desktop/ webgl/ android/ ios/
```

The content items in each platform's project show how the toolkit's content is
built: `SpriteFontContent`, `TextureAtlasContent` and `SoundBankContent`.

The sprites and sounds are procedural, made by `Content/make-content.py`, and the launch screen's
logo by the framework sample's. The Inter font is licensed under the SIL Open Font License.
[Content/CREDITS.md](Content/CREDITS.md) lists where each file comes from.

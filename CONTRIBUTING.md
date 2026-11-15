# Contributing

Thanks for helping with DotGame.Toolkit. Bug reports, fixes and new tests are all welcome.

## Reporting a bug

Open an issue with what you did, what you expected and what happened instead, and the platform it
happened on. For a security problem, see `SECURITY.md` instead.

## Building from source

The toolkit builds on the framework's source, in a clone of
[DotGame.Framework](https://github.com/hotchaipro/DotGame.Framework) anywhere, which the
`DotGameFrameworkDir` environment variable names (or `-p:DotGameFrameworkDir=<folder>` on a build).
Install the content pipeline's tools from it first, as its CONTRIBUTING.md describes, then run the
sample:

```sh
git clone https://github.com/hotchaipro/DotGame.Framework.git
git clone https://github.com/hotchaipro/DotGame.Toolkit.git
export DotGameFrameworkDir=$PWD/DotGame.Framework   # Windows: setx DotGameFrameworkDir %CD%\DotGame.Framework
cd DotGame.Toolkit
dotnet run --project sample/desktop/DotGame.Toolkit.Sample.Desktop.csproj
```

Set it for good (your shell's profile, or `setx` on Windows) so that Visual Studio sees it too.

Nothing else is needed to build, test or check the docs. Python, with numpy and Pillow, is needed only
to regenerate the checked-in files that scripts make: the sample's art and sounds
(`sample/Content/make-content.py`) and the Visual Studio extension's icons
(`vs/src/DotGame.Toolkit.VisualStudio/Images/draw_icons.py`).

`DotGame.Toolkit.slnx` has every project: the toolkit, its content pipeline, the tests, the sample for
every platform (under `sample`), and the framework's projects they build, from the clone beside this one
(`../DotGame.Framework`, as above). It's a contributor's workspace: a game sees the toolkit's and the
framework's packages, not their projects. The tests run without a graphics device, so they cover
everything except drawing.

The content pipeline's build tool (`pipeline/tool/`) is built on demand the first time a game's build
needs it. To use a prebuilt one instead, set `ToolkitContentPipelineBuildTool` to its `.dll` or native
executable.

## Repository layout

```text
toolkit/       The toolkit library (DotGame.Toolkit)
pipeline/      The content pipeline: MSBuild items and targets (build/ is the include games import), its
               MSBuild tasks (tasks/) and the build tool that processes content (tool/)
native/        Native tools the content pipeline runs (glyphgen, the glyph renderer for sprite fonts)
sample/       The sample, for every platform
test/          Tests
docs/          Guides, architecture notes and the API reference
tools/         The docs' tools: the API reference's generator and the guides' checker (api-docs/)
skills/        Agent skills for games using the toolkit
packaging/     NuGet packaging: the packages' MSBuild files and READMEs
eng/           The repository's own build rules: package versions, public API lists
```

## Making a change

Build and test with the .NET 11 SDK or later, from `DotGame.Toolkit.slnx`, which has every project:

```bash
dotnet build DotGame.Toolkit.slnx
dotnet test --solution test/DotGame.Toolkit.Tests.slnf
```

`test/DotGame.Toolkit.Tests.slnf` is the solution's test projects: the sample's apps aren't tests, and
`dotnet test` on the whole solution would ask for an iOS or Android device to run them on.

The build treats every warning as an error, including the code style in `.editorconfig`, so a change
that builds cleanly already follows it. `dotnet format` fixes most style errors.

A few things the toolkit holds to:

- **The same behavior on every platform:** desktop, web, Android and iOS.
- **Trim and AOT safe, with no reflection or LINQ.** The build fails on either (`eng/BannedSymbols.txt`).
- **Each fix comes with a test** that fails without it.
- **Public API changes are discussed first,** in an issue, before a pull request. `eng/api/<project>/`
  lists the public API, and the build fails until a change to it is in `PublicAPI.Unshipped.txt`: the
  analyzer's code fix (or `dotnet format analyzers --diagnostics RS0016`) adds it.
- **The docs follow the code.** `docs/api` is generated from the doc comments: after changing a public
  API, run `dotnet run tools/api-docs/generate.cs` (`-- --check` fails if it's out of date). It reads
  the framework's and DotGame.Imaging's clones (`DotGameFrameworkDir` and `DotGameImagingDir`), for links
  to their types. The generator is DotGame.Imaging's, copied: what's particular to this repository is
  in `tools/api-docs/packages.json`, and a change to `generate.cs` itself is made in DotGame.Imaging
  and copied here (CI fails while they differ). After changing a guide,
  `dotnet run tools/api-docs/check-docs.cs` checks its links and compiles its samples; it's
  DotGame.Imaging's too, with this repository's settings in `tools/api-docs/check-docs.json`.

## Native tools

The glyphgen binaries are prebuilt and checked in, so you only rebuild them
when you update glyphgen or its submodules. Usually you just update a
submodule and push to `main`: the
[native-glyphgen](.github/workflows/native-glyphgen.yml)
workflow cross-compiles all six desktop targets with Zig on one Linux runner
and commits the binaries.

To build locally:

```sh
git submodule update --init --recursive
pwsh native/build.ps1                # current host platform
pwsh native/build.ps1 -Platform all  # every desktop platform
```

glyphgen can build any desktop target from any host, and downloads Zig
automatically. See [native/glyphgen/README.md](native/glyphgen/README.md).

## Pull requests

Open a pull request against `main`. By contributing, you agree that your contribution is licensed under
the MIT License, as the rest of the toolkit is (`LICENSE`).
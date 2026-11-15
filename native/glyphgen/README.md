# glyphgen

Small native command-line tool used by the toolkit `SpriteFontContent` pipeline
(the `spritefont` command of `build`) to render font glyphs.

**glyphgen renders outlines to pixels; everything read from the font's tables is C#.**
glyphgen turns glyph outlines into per-glyph bitmaps, with their bounds and, for bitmap
fonts, their hinted advances, which only rendering gives. `build/Fonts/FontFile.cs`
reads the rest: the font metrics, names, character map (which glyphs a font has, and so the
fallbacks), advances and cap height, and `OpenTypeKerning.cs` the kerning. Atlas packing,
font merging and the `.spritefont` file format are C# too.

Prebuilt binaries are committed per runtime identifier so building fonts needs no
extra install:

| RID | Binary |
|---|---|
| win-x64, win-arm64 | `<rid>/glyphgen.exe` (static, no CRT dependency) |
| linux-x64, linux-arm64 | `<rid>/glyphgen` (glibc 2.17+, static libc++) |
| osx-x64, osx-arm64 | `<rid>/glyphgen` (macOS 11+, ad-hoc signed on arm64) |

`toolkit/pipeline/ContentPipeline.Toolkit.targets` picks the host binary (`GlyphGenExe`;
override it to use your own build).

## Usage

```
glyphgen --font FILE --glyphs FILE --output FILE
         [--format msdf|mtsdf|sdf|bitmap] [--size PX_PER_EM]
         [--range-lower PX] [--range-upper PX] [--threads N]
```

- `--glyphs`: whitespace-separated decimal glyph indices (from the font's character map,
  which the caller reads), rendered sorted and without duplicates. An index past the
  font's glyph count is an error.
- `--range-lower` (< 0, outside the glyph) / `--range-upper` (> 0, inside):
  distance range in atlas pixels.
- `bitmap`: FreeType hinted grayscale coverage at `--size` pixels per em.
- Short line edges (under 0.005 em) that turn back on a neighboring edge are removed first.
  Some fonts step a few units backward where a curve meets a stem; a distance field keeps
  that as an extremely sharp corner, which an outline grows into a long spike.
- Distance-field glyphs with overlapping contours (common in fonts exported from
  variable masters) are replaced by the union of their contours (curves
  flattened to within 2e-5 em) before generation, avoiding MSDF artifacts where
  hidden edges run inside the glyph. Other glyphs keep their exact curves.

Output (little-endian): `"GLGN"`, u32 version (2), u32 glyph count, then per glyph:
u32 glyphIndex; f64 advance (em: a bitmap glyph's hinted advance, zero for distance
fields, whose advance is the font's); f64 planeLeft, planeBottom, planeRight, planeTop
(em, y-up: where the pixels sit relative to the glyph origin); u32 width, height,
channels; then `width * height * channels` bytes, rows top to bottom.

## Third-party components

| Component | Version | License |
|---|---|---|
| [msdfgen](https://github.com/Chlumsky/msdfgen) (core + ext/import-font) | v1.13 (submodule `src/msdfgen`) | MIT |
| [FreeType](https://freetype.org) | 2.14.3 (submodule `src/freetype`) | FreeType License (FTL) |
| [Clipper2](https://github.com/AngusJohnson/Clipper2) (CPP/Clipper2Lib) | 1.5.4 (submodule `src/clipper2`) | Boost Software License 1.0 |

The FreeType module list is reduced by `src/config/freetype/config/ftmodule.h`
(TrueType, CFF/OpenType, Type 1 helpers, autofit, smooth and mono rasterizers).

## Building

```powershell
git submodule update --init toolkit/native/glyphgen/src/msdfgen toolkit/native/glyphgen/src/freetype toolkit/native/glyphgen/src/clipper2
pwsh toolkit/native/glyphgen/build.ps1                      # host RID
pwsh toolkit/native/glyphgen/build.ps1 -Platform linux-x64  # one RID
pwsh toolkit/native/glyphgen/build.ps1 -Platform all        # all RIDs
```

The build uses [Zig](https://ziglang.org) 0.16.0 as a self-contained C/C++
cross-compiler (clang + libc for every target), so all six binaries are built from
one machine with no Visual Studio, Xcode, CMake or vcpkg. Pass `-ZigExe` to use an
installed Zig; otherwise the script downloads it into `build/zig` (git-ignored),
verifying the archive against the SHA-256 published in
`https://ziglang.org/download/index.json`.

The `native-glyphgen` GitHub workflow (`.github/workflows/native-glyphgen.yml`)
runs `toolkit/native/build.ps1 -Platform all` on Linux and commits the binaries
whenever the submodules or build scripts change on `main`, so you normally only
update a submodule and push.

After rebuilding on Windows, keep the Unix executable bit in git:

```
git update-index --chmod=+x toolkit/native/glyphgen/linux-x64/glyphgen toolkit/native/glyphgen/linux-arm64/glyphgen toolkit/native/glyphgen/osx-x64/glyphgen toolkit/native/glyphgen/osx-arm64/glyphgen
```

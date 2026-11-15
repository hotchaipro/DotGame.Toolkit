#!/bin/bash
# pack.sh: the toolkit's packages, built from this repository in Release: DotGame.Toolkit and its
# content pipeline, DotGame.Toolkit.Build.
#
#   VERSION=0.1.0-local.1 packaging/pack.sh
#
# The toolkit builds on the framework's source, in the clone DotGameFrameworkDir names (required), and
# its packages depend on the framework's packages at FRAMEWORK_VERSION (by
# default VERSION: every DotGame package has the same version), which the framework's packaging/pack.sh
# makes. The framework's packaging/pack-lib.sh is what the two pack.sh share.
#
# Writes artifacts/packages/*.nupkg (or OUT) and checks them with the framework's packaging/check.sh
# against this repository's packaging/contents.txt.
set -euo pipefail
REPO=$(cd "$(dirname "$0")/.." && pwd)
# The framework, as the toolkit's Directory.Build.props finds it
FRAMEWORK=${DotGameFrameworkDir:-}
[ -f "$FRAMEWORK/framework/DotGame.Framework.csproj" ] || { echo "pack.sh: no framework at $FRAMEWORK (DotGameFrameworkDir)"; exit 1; }
FRAMEWORK=$(cd "$FRAMEWORK" && pwd)
FRAMEWORK_VERSION=${FRAMEWORK_VERSION:-${VERSION:-0.1.0-local.1}}
source "$FRAMEWORK/packaging/pack-lib.sh"
KP=$REPO/packaging
rm -f "$OUT"/DotGame.Toolkit.[0-9]*.nupkg "$OUT"/DotGame.Toolkit.Build.[0-9]*.nupkg

# ---- Build ----
build toolkit/DotGame.Toolkit.csproj
build pipeline/tool/DotGame.Toolkit.ContentPipeline.BuildTool.csproj
build pipeline/tasks/DotGame.Toolkit.ContentPipeline.Tasks.csproj

# ---- Stage and pack ----
N=$FRAMEWORK/THIRD-PARTY-NOTICES.md
TN=$REPO/THIRD-PARTY-NOTICES.md
IN=$IMAGING_NOTICES
AN=$AUDIOCODECS_NOTICES
[ -f "$IN" ] || { echo "pack.sh: no DotGame.Imaging $IMAGING_VERSION notices at $IN"; exit 1; }

# DotGame.Toolkit
K=$REPO/toolkit/bin/Release/net11.0
put DotGame.Toolkit lib/net11.0 "$K/DotGame.Toolkit.dll" "$K/DotGame.Toolkit.xml"
README_BODY=$KP/readme/toolkit.md
pack DotGame.Toolkit "DotGame's toolkit: sprite fonts and text, texture atlases and animation, particles and easing, sound banks, cameras and shapes, for every platform." \
  "$(dep net11.0 "$(pin DotGame.Framework "$FRAMEWORK_VERSION")<dependency id=\"DotGame.Imaging\" version=\"[$IMAGING_VERSION]\" />$(pin DotGame.Toolkit.Build)")"

# DotGame.Toolkit.Build: its content pipeline's targets, tasks, build tool and glyphgen
TB=DotGame.Toolkit.Build
put $TB pipeline "$REPO"/pipeline/*.targets "$REPO"/pipeline/*.xaml
put $TB pipeline/Shaders "$REPO"/pipeline/Shaders/*
# The font shader compiled for every platform, as each platform's shader command would compile it
# (desktop: SPIR-V, Metal and DXIL; iOS: Metal, the same as desktop's; Android and web: GLSL), so a
# game's fonts need no shader tools. The pipeline copies its platform's files (Precompiled metadata).
# The framework's build tool compiles it, as built for the toolkit's.
FT=$FRAMEWORK/pipeline/tool/bin/Release/net11.0
PRE=$STAGE/$TB/payload/pipeline/Shaders/precompiled; mkdir -p "$PRE" "$STAGE/shader-work"
shader() { dotnet "$FT/DotGame.Framework.ContentPipeline.BuildTool.dll" shader --slangc "$TOOLS/slangc" --spirv-cross "$TOOLS/spirv-cross" \
  --input "$REPO/pipeline/Shaders/spritefont.slang" --intermediate-dir "$STAGE/shader-work" "$@"; }
shader --shader-types spirv,msl,dxil --spirv2dxil "$TOOLS/spirv2dxil" --output "$PRE/spritefont.frag.spv"
shader --shader-types glsl --output "$PRE/spritefont.glsl"
put $TB pipeline/tasks "$REPO/pipeline/tasks/bin/Release/netstandard2.0/DotGame.Toolkit.ContentPipeline.Tasks.dll"
put $TB pipeline/tool "$REPO/pipeline/tool/ContentVersions.props"
TT=$REPO/pipeline/tool/bin/Release/net11.0
put $TB pipeline/tool "$TT"/DotGame.Toolkit.ContentPipeline.BuildTool.dll "$TT"/DotGame.Toolkit.ContentPipeline.BuildTool.deps.json "$TT"/DotGame.Toolkit.ContentPipeline.BuildTool.runtimeconfig.json "$TT"/DotGame.Framework.ContentPipeline.BuildTool.dll "$TT"/DotGame.Imaging.dll "$TT"/DotGame.AudioCodecs.dll
# glyphgen for the five build machines (not its sources, nor osx-x64, which isn't one)
for rid in win-x64 win-arm64 osx-arm64 linux-x64 linux-arm64; do
  put $TB native/glyphgen/$rid "$REPO"/native/glyphgen/$rid/*
done
put $TB buildTransitive "$KP"/buildTransitive/$TB/*
NOTICES=("=Portions of this software are copyright © 2026 The FreeType Project (https://freetype.org). All rights reserved." \
  "$TN#msdfgen" "$TN#FreeType" "$TN#Clipper2" "$IN#libjpeg-turbo" "$IN#libwebp" "$AN#libopus" "$AN#libopusenc" "$N#DirectX Shader Compiler")
pack $TB "Added by DotGame.Toolkit; don't reference it directly. The toolkit's content pipeline: sprite fonts, texture atlases and sound banks at build time. Nothing in it ships in a game." \
  "$(dep net11.0 "$(pin DotGame.Framework.Build "$FRAMEWORK_VERSION")")"

ls -la "${PACKED[@]}"
# The framework's [*] lines (every package's readme and notices), then the toolkit's packages
"$P/check.sh" --contents "$P/contents.txt" --contents "$KP/contents.txt" "${PACKED[@]}"

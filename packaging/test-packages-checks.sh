# test-packages-checks.sh: the toolkit's own checks, which the framework's packaging/test-packages.sh
# sources (DOTGAME_LIBRARY_CHECKS) when it builds the toolkit's samples from the packages.

# After each head builds: the font shader comes precompiled in the package, not compiled
library_head_checks() {
  check "toolkit sample, $platform: the font shader comes precompiled, not compiled" 'grep -qE "pipeline[/\\\\]Shaders[/\\\\]precompiled[/\\\\]spritefont" "$log" && ! grep -q "spritefont.slang\" --output" "$log"'
}

# Fonts need no shader tools: the toolkit sample (fonts, atlases, banks, no shaders of its own) with
# every shader tool pointing nowhere
library_checks() {
  fresh noshader; export NUGET_PACKAGES=$T/nuget-noshader
  local project
  project=$(ls "$T/noshader/toolkit/desktop"/*.csproj)
  PATH=$CLEAN_PATH dotnet build "$project" -nologo -v n -p:ContentCacheEnabled=false -p:SlangCExe=/nonexistent/slangc -p:SpirvCrossExe=/nonexistent/spirv-cross -p:Spirv2DxilExe=/nonexistent/spirv2dxil > "$T/noshader.log" 2>&1
  check "toolkit sample, desktop: builds with no shader tools" '[ $? -eq 0 ] && ls "$T/noshader/toolkit/desktop"/bin/Debug/*/Content/_toolkit/Shaders/spritefont.frag.dxil >/dev/null 2>&1'
  grep -E "error [A-Z]+[0-9]*:" "$T/noshader.log" | sed "s|$T/||g" | sort -u | head -3 | cut -c1-600
}

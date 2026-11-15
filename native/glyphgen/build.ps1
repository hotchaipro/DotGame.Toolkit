#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the native glyphgen tool for one or all desktop platforms.

.DESCRIPTION
    glyphgen renders font glyphs (MSDF / MTSDF / SDF via msdfgen, hinted bitmaps via
    FreeType) for the SpriteFontContent content pipeline item.

    Every platform is cross-compiled from any host with Zig (a self-contained
    clang + libc toolchain), so all six binaries can be produced on one machine.
    Zig is downloaded on first use into toolkit/native/glyphgen/build/zig (gitignored)
    unless -ZigExe is given or 'zig' is on PATH.

    Sources come from the git submodules under toolkit/native/glyphgen/src
    (msdfgen, freetype). Binaries are written to toolkit/native/glyphgen/<rid>/.

.PARAMETER Platform
    Target platform(s): win-x64, win-arm64, linux-x64, linux-arm64, osx-x64,
    osx-arm64, or 'all'. Defaults to the current host platform.

.PARAMETER ZigExe
    Path to a zig executable. Defaults to 'zig' on PATH, else a downloaded copy.

.EXAMPLE
    ./toolkit/native/glyphgen/build.ps1
    Builds for the current host platform.

.EXAMPLE
    ./toolkit/native/glyphgen/build.ps1 -Platform all
    Cross-compiles all six desktop platforms.
#>
param(
    [Parameter()]
    [ValidateSet('all', 'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string[]]$Platform,

    [Parameter()]
    [string]$ZigExe
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ZigVersion = '0.16.0'

$ScriptDir = $PSScriptRoot
$SrcDir = Join-Path $ScriptDir 'src'
$MsdfgenDir = Join-Path $SrcDir 'msdfgen'
$FreetypeDir = Join-Path $SrcDir 'freetype'
$ClipperDir = Join-Path $SrcDir 'clipper2/CPP/Clipper2Lib'
$BuildBaseDir = Join-Path $ScriptDir 'build'

foreach ($required in @((Join-Path $MsdfgenDir 'msdfgen.h'), (Join-Path $FreetypeDir 'include/ft2build.h'), (Join-Path $ClipperDir 'include/clipper2/clipper.h'))) {
    if (-not (Test-Path $required)) {
        throw "Submodule source not found ('$required'). Run 'git submodule update --init' first."
    }
}

# Zig targets: Linux binaries link against glibc 2.17 so they run on old distributions;
# libc++ is linked statically everywhere.
$Targets = [ordered]@{
    'win-x64'     = @{ Triple = 'x86_64-windows-gnu';       Exe = 'glyphgen.exe' }
    'win-arm64'   = @{ Triple = 'aarch64-windows-gnu';      Exe = 'glyphgen.exe' }
    'linux-x64'   = @{ Triple = 'x86_64-linux-gnu.2.17';    Exe = 'glyphgen' }
    'linux-arm64' = @{ Triple = 'aarch64-linux-gnu.2.17';   Exe = 'glyphgen' }
    'osx-x64'     = @{ Triple = 'x86_64-macos.11.0';        Exe = 'glyphgen' }
    'osx-arm64'   = @{ Triple = 'aarch64-macos.11.0';       Exe = 'glyphgen' }
}

if (-not $Platform) {
    $hostOS = if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'win' } elseif ($IsMacOS) { 'osx' } elseif ($IsLinux) { 'linux' } else { throw 'Could not detect host OS. Specify -Platform explicitly.' }
    $hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
    $Platform = @("$hostOS-$hostArch")
    Write-Host "Auto-detected platform: $Platform"
}
elseif ($Platform -contains 'all') {
    $Platform = @($Targets.Keys)
}

function Get-Zig {
    if ($ZigExe) {
        return $ZigExe
    }
    $onPath = Get-Command zig -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
        'Arm64' { 'aarch64' }
        'X64' { 'x86_64' }
        default { throw "Unsupported host architecture: $_" }
    }
    $os = if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'windows' } elseif ($IsMacOS) { 'macos' } else { 'linux' }
    $name = "zig-$arch-$os-$ZigVersion"
    $zigDir = Join-Path $BuildBaseDir 'zig'
    $exe = Join-Path $zigDir "$name/zig$(if ($os -eq 'windows') { '.exe' })"
    if (Test-Path $exe) {
        return $exe
    }

    $archive = if ($os -eq 'windows') { "$name.zip" } else { "$name.tar.xz" }
    New-Item -ItemType Directory -Force -Path $zigDir | Out-Null
    $archivePath = Join-Path $zigDir $archive
    # The checksum always comes from ziglang.org; the archive may come from a community
    # mirror (ziglang.org downloads are heavily throttled).
    $index = Invoke-RestMethod -Uri 'https://ziglang.org/download/index.json'
    $expected = $index.$ZigVersion."$arch-$os".shasum
    $downloaded = $false
    foreach ($url in @("https://pkg.machengine.org/zig/$archive", "https://ziglang.org/download/$ZigVersion/$archive")) {
        Write-Host "Downloading $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile $archivePath
        }
        catch {
            Write-Warning "Download failed: $($_.Exception.Message)"
            continue
        }
        if ((Get-FileHash -Path $archivePath -Algorithm SHA256).Hash -eq $expected) {
            $downloaded = $true
            break
        }
        Write-Warning "Checksum mismatch for $url"
        Remove-Item $archivePath
    }
    if (-not $downloaded) {
        throw "Unable to download $archive"
    }
    tar -xf $archivePath -C $zigDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to extract $archive" }
    Remove-Item $archivePath
    return $exe
}

$FreetypeSources = @(
    'src/base/ftsystem.c', 'src/base/ftinit.c', 'src/base/ftdebug.c', 'src/base/ftbase.c',
    'src/base/ftbbox.c', 'src/base/ftglyph.c', 'src/base/ftbitmap.c', 'src/base/ftmm.c',
    'src/autofit/autofit.c', 'src/truetype/truetype.c', 'src/cff/cff.c', 'src/psaux/psaux.c',
    'src/psnames/psnames.c', 'src/pshinter/pshinter.c', 'src/sfnt/sfnt.c', 'src/smooth/smooth.c',
    'src/raster/raster.c', 'src/gzip/ftgzip.c'
) | ForEach-Object { Join-Path $FreetypeDir $_ }

$MsdfgenSources = @(Get-ChildItem (Join-Path $MsdfgenDir 'core') -Filter '*.cpp' | ForEach-Object FullName) +
    (Join-Path $MsdfgenDir 'ext/import-font.cpp')

$ClipperSources = @(Get-ChildItem (Join-Path $ClipperDir 'src') -Filter '*.cpp' | ForEach-Object FullName)

$Zig = Get-Zig
Write-Host "Using $Zig ($(& $Zig version))"

foreach ($rid in $Platform) {
    $target = $Targets[$rid]
    Write-Host ""
    Write-Host "=== glyphgen $rid ($($target.Triple)) ===" -ForegroundColor Cyan

    $objDir = Join-Path $BuildBaseDir "obj/$rid"
    New-Item -ItemType Directory -Force -Path $objDir | Out-Null
    $outDir = Join-Path $ScriptDir $rid
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $common = @('-target', $target.Triple, '-O2', '-DNDEBUG', '-ffunction-sections', '-fdata-sections')
    $includes = @("-I$(Join-Path $SrcDir 'config')", "-I$(Join-Path $FreetypeDir 'include')", "-I$(Join-Path $ClipperDir 'include')")

    $jobs = @()
    foreach ($source in $FreetypeSources) {
        $jobs += @{
            Args = $common + $includes + @('-w', '-DFT2_BUILD_LIBRARY', '-std=c99', '-c', $source,
                '-o', (Join-Path $objDir ("ft-" + [IO.Path]::GetFileNameWithoutExtension($source) + '.o')))
            Driver = 'cc'
        }
    }
    foreach ($source in $MsdfgenSources + $ClipperSources + (Join-Path $SrcDir 'main.cpp')) {
        # Third-party sources build without warnings; libc++ headers trip clang's
        # nullability-completeness warning on some targets.
        $warnings = if ($source -like '*main.cpp') { @('-Wall', '-Wno-nullability-completeness') } else { @('-w') }
        $jobs += @{
            Args = $common + $includes + $warnings + @("-I$MsdfgenDir", '-DMSDFGEN_PUBLIC=', '-DMSDFGEN_EXT_PUBLIC=',
                '-DMSDFGEN_USE_CPP11', '-DMSDFGEN_DISABLE_SVG', '-DMSDFGEN_DISABLE_PNG', '-std=c++17', '-c', $source,
                '-o', (Join-Path $objDir ("cpp-" + [IO.Path]::GetFileNameWithoutExtension($source) + '.o')))
            Driver = 'c++'
        }
    }

    $failures = $jobs | ForEach-Object -ThrottleLimit ([Environment]::ProcessorCount) -Parallel {
        $output = & $using:Zig $_.Driver @($_.Args) 2>&1
        if ($LASTEXITCODE -ne 0) {
            "$($_.Args -join ' ')`n$($output -join "`n")"
        }
    }
    if ($failures) {
        $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "Compilation failed for $rid"
    }

    $exePath = Join-Path $outDir $target.Exe
    $linkArgs = @('c++', '-target', $target.Triple, '-O2', '-s', '-Wl,--gc-sections', '-o', $exePath) +
        @(Get-ChildItem $objDir -Filter '*.o' | ForEach-Object FullName)
    if ($rid -like 'win-*') {
        $linkArgs += @('-lshell32', '-static')
    }
    elseif ($rid -like 'linux-*') {
        $linkArgs += @('-pthread')
    }
    else {
        # ld64 does not understand --gc-sections
        $linkArgs = $linkArgs | Where-Object { $_ -ne '-Wl,--gc-sections' }
        $linkArgs += @('-Wl,-dead_strip')
    }
    & $Zig @linkArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Link failed for $rid"
    }
    Get-ChildItem $outDir -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item
    Write-Host "  -> $exePath ($([math]::Round((Get-Item $exePath).Length / 1KB)) KB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. On macOS/Linux, git records the executable bit from 'git update-index --chmod=+x'." -ForegroundColor Yellow

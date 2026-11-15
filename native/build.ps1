#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the toolkit's native tools.

.DESCRIPTION
    Runs the build script of each native component under native/
    (currently glyphgen, native/glyphgen/build.ps1, used by the
    SpriteFontContent pipeline item). Built binaries are written to
    native/<component>/<rid>/ and are checked in, so this is only needed
    when updating a native dependency. The native-glyphgen GitHub workflow
    runs this script for every platform when the native sources change.

    The framework's native libraries are built separately by native/build.ps1.

.PARAMETER Platform
    Target platform(s): win-x64, win-arm64, linux-x64, linux-arm64, osx-x64,
    osx-arm64, or 'all'. Defaults to the current host platform. glyphgen is
    cross-compiled with Zig, so any platform can be built from any host.

.PARAMETER ZigExe
    Path to a zig executable. Defaults to 'zig' on PATH, else a downloaded copy.

.EXAMPLE
    ./native/build.ps1
    Builds for the current host platform.

.EXAMPLE
    ./native/build.ps1 -Platform all
    Builds every platform.
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

$glyphgenArgs = @{}
if ($Platform) {
    $glyphgenArgs.Platform = $Platform
}
if ($ZigExe) {
    $glyphgenArgs.ZigExe = $ZigExe
}
& (Join-Path $PSScriptRoot 'glyphgen/build.ps1') @glyphgenArgs
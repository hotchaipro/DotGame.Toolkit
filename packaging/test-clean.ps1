# test-clean.ps1 <feed> <version> [platform...]: the toolkit's guide, on a machine with only the .NET SDK:
# the framework's template from nuget.org, the toolkit's package from a folder of packages beside it.
#
#   pwsh packaging/test-clean.ps1 artifacts/packages 0.1.0-preview.1 webgl android
#
# The publish workflow runs it in Microsoft's .NET SDK container images, which have nothing else: no JDK,
# no Android SDK, no Python, no compilers. A game from `dotnet new dotgame`, with the toolkit's package
# added as docs/guide/toolkit.md says, a font, an atlas and a sound bank from the sample's content, and
# the game drawing text with the font. Its desktop head built and published (every file the pipelines
# built is beside the app), then each platform named here after installing what the framework's
# Platforms guide says it needs. The framework's packages come from nuget.org, as a game gets them.
# Each step's log is in the work folder (TEMP, or DOTGAME_CLEAN_WORK); any failed step fails the script.
param(
    [Parameter(Mandatory)][string]$Feed,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(ValueFromRemainingArguments)][string[]]$Platforms = @()
)
$ErrorActionPreference = 'Stop'
$Feed = (Resolve-Path $Feed).Path
$Repo = Split-Path $PSScriptRoot -Parent
$Work = $env:DOTGAME_CLEAN_WORK
if (-not $Work) { $Work = Join-Path ([IO.Path]::GetTempPath()) "dotgame-toolkit-clean-$PID" }
New-Item -ItemType Directory -Force $Work | Out-Null
$script:Failed = 0

function Step([string]$Name, [scriptblock]$Command) {
    $log = Join-Path $Work "$Name.log"
    & $Command *> $log
    if ($LASTEXITCODE -eq 0) {
        Write-Host "PASS  $Name"
    } else {
        Write-Host "FAIL  $Name (exit $LASTEXITCODE)"
        Get-Content $log | Select-String -Pattern ': error |error :|^error|not found|No such file' | Select-Object -Unique -First 10 | ForEach-Object { Write-Host "        $($_.Line.Trim())" }
        Get-Content $log -Tail 4 | ForEach-Object { Write-Host "        $_" }
        $script:Failed++
    }
}

function Check([string]$Name, [bool]$Passed, [string]$Detail = '') {
    if ($Passed) { Write-Host "PASS  $Name" } else { Write-Host "FAIL  $Name $Detail"; $script:Failed++ }
}

# Every file a head's content pipelines built (the framework's and the toolkit's) is in a folder
function Missing([string]$Built, [string]$Folder) {
    @(Get-ChildItem $Built -Recurse -File | ForEach-Object { [IO.Path]::GetRelativePath($Built, $_.FullName) } |
        Where-Object { -not (Test-Path (Join-Path $Folder $_)) })
}

Write-Host "dotnet $(dotnet --version), on $([Runtime.InteropServices.RuntimeInformation]::OSDescription)"
foreach ($tool in 'java', 'python3', 'python', 'slangc', 'cmake', 'cl') {
    if (Get-Command $tool -ErrorAction SilentlyContinue) { Write-Host "note: $tool is on PATH" }
}

# The feed beside nuget.org, for everything under the work folder
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="packages" value="$Feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content (Join-Path $Work 'nuget.config')

Push-Location $Work
try {
    Step 'template-install' { dotnet new install "DotGame.Templates::$Version" }
    Step 'template-new' { dotnet new dotgame -n FontGame }
    Set-Location FontGame

    # The toolkit's package, once in the props every project imports, as the guide says
    $props = Get-Content Directory.Build.props -Raw
    if ($props -notmatch "<DotGameVersion>$([regex]::Escape($Version))</DotGameVersion>") {
        Write-Host "FAIL  template-new: not version $Version"; $script:Failed++
    }
    $props.Replace('</Project>', @'
    <ItemGroup>
        <PackageReference Include="DotGame.Toolkit" Version="$(DotGameVersion)" />
    </ItemGroup>

</Project>
'@) | Set-Content Directory.Build.props

    # A font, an atlas and a sound bank, from the sample's content, as items in each head
    foreach ($folder in 'Fonts', 'Sprites', 'Sounds') {
        New-Item -ItemType Directory -Force "Content/$folder" | Out-Null
    }
    Copy-Item (Join-Path $Repo 'sample/Content/Fonts/Inter-Medium.ttf') Content/Fonts/
    Get-ChildItem (Join-Path $Repo 'sample/Content/Sprites') -Filter 'coin_*.png' | Select-Object -First 4 | Copy-Item -Destination Content/Sprites/
    Get-ChildItem (Join-Path $Repo 'sample/Content/Sounds') -Filter '*.wav' | Select-Object -First 2 | Copy-Item -Destination Content/Sounds/
    $items = @'
    <ItemGroup>
        <SpriteFontContent Include="..\Content\Fonts\*.ttf">
            <LinkBase>Content\Fonts</LinkBase>
        </SpriteFontContent>
        <TextureAtlasContent Include="..\Content\Sprites\*.png">
            <LinkBase>Content\Sprites</LinkBase>
        </TextureAtlasContent>
        <SoundBankContent Include="..\Content\Sounds\*.wav">
            <LinkBase>Content\Sounds</LinkBase>
        </SoundBankContent>
    </ItemGroup>

</Project>
'@
    # The heads: each folder with a project of its name (FontGame itself is the shared project's)
    foreach ($head in Get-ChildItem -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") }) {
        $project = Join-Path $head.FullName "$($head.Name).csproj"
        $text = Get-Content $project -Raw
        $at = $text.LastIndexOf('</Project>')
        Set-Content $project ($text.Substring(0, $at) + $items)
    }

    # The game loads them and draws text, as the guides do
    $game = Get-Content FontGame/MainGame.cs -Raw
    $game = $game.Replace('using DotGame.Framework.Input;', "using DotGame.Framework.Input;`nusing DotGame.Toolkit;`nusing DotGame.Toolkit.Audio;`nusing DotGame.Toolkit.Fonts;`nusing DotGame.Toolkit.Graphics;")
    $game = $game.Replace('    private Texture _ball = null!;', "    private Texture _ball = null!;`n    private SpriteFont _font = null!;`n    private TextureAtlas _atlas = null!;`n    private SoundBank _sounds = null!;")
    $game = $game.Replace('        this._ball = await this.Content.LoadAsync<Texture>("Textures/ball");', @'
        this.InitializeToolkit();
        this._ball = await this.Content.LoadAsync<Texture>("Textures/ball");
        this._font = await this.Content.LoadAsync<SpriteFont>("Fonts/Inter-Medium");
        this._atlas = await this.Content.LoadAsync<TextureAtlas>("Sprites/spritesheet");
        this._sounds = await this.Content.LoadAsync<SoundBank>("Sounds/soundbank");
'@)
    $game = $game.Replace('        spriteBatch.Draw(this._ball, this._position, Color.White);', "        spriteBatch.Draw(this._ball, this._position, Color.White);`n        spriteBatch.DrawString(this._font, `$`"{this._atlas.RegionNames.Count} sprites`", new Vector2(16, 16), Color.White);")
    if ($game -notmatch 'InitializeToolkit' -or $game -notmatch 'DrawString') { throw 'MainGame.cs: the template changed; update this script' }
    Set-Content FontGame/MainGame.cs $game

    # The desktop: built, then published as the guide says, for this machine
    Step 'desktop' { dotnet build FontGame.Desktop }
    $built = (Resolve-Path 'FontGame.Desktop/obj/ContentPipeline/Output' -ErrorAction SilentlyContinue).Path
    $toolkit = @(if ($built) { Get-ChildItem $built -Recurse -File | Where-Object { $_.FullName -match 'Fonts|Sprites|Sounds|_toolkit' } })
    Check 'desktop-content: the font, the atlas, the sound bank and the font shader are built' ($toolkit.Count -ge 4) "($($toolkit.Count) files)"
    $published = Join-Path $Work 'desktop'
    Step 'desktop-publish' { dotnet publish FontGame.Desktop -c Release --use-current-runtime -o $published }
    $missing = if ($built) { Missing $built $published } else { @('everything') }
    Check 'desktop-publish-content: every built content file is published' ($missing.Count -eq 0) "(not published: $($missing -join ', '))"

    foreach ($platform in $Platforms) {
        switch ($platform) {
            'webgl' {
                Step 'workload-wasm-tools' { dotnet workload install wasm-tools }
                Step 'webgl' { dotnet build FontGame.WebGL }
                $web = Join-Path $Work 'web'
                Step 'webgl-publish' { dotnet publish FontGame.WebGL -o $web }
                $fonts = @(Get-ChildItem (Join-Path $web 'wwwroot/Content') -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match 'Fonts|_toolkit' })
                Check 'webgl-publish-content: the font and its shader are published' ($fonts.Count -ge 2) "($($fonts.Count) files)"
            }
            'android' {
                Step 'workload-android' { dotnet workload install android }
                # As the framework's Platforms guide says: the SDK and a JDK into folders of the work folder,
                # then found through ANDROID_HOME and JAVA_HOME. The installer needs USER, which containers don't set.
                if (-not $env:USER -and -not $env:USERNAME) { $env:USER = 'dotgame' }
                $sdk = Join-Path $Work 'android-sdk'; $jdk = Join-Path $Work 'jdk'
                Step 'android-dependencies' { dotnet build FontGame.Android -t:InstallAndroidDependencies "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" -p:AcceptAndroidSDKLicenses=True -p:AndroidManifestType=GoogleV2 }
                $env:ANDROID_HOME = $sdk; $env:JAVA_HOME = $jdk
                Step 'android' { dotnet build FontGame.Android }
            }
            'ios' {
                Step 'workload-ios' { dotnet workload install ios }
                Step 'ios' { dotnet build FontGame.iOS -p:RuntimeIdentifier=iossimulator-arm64 }
            }
            default { throw "unknown platform: $platform" }
        }
    }
} finally { Pop-Location }

Write-Host "logs: $Work"
if ($script:Failed) { Write-Host "$($script:Failed) failed"; exit 1 }
Write-Host 'all passed'

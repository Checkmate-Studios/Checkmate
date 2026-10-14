<#
.SYNOPSIS
  Builds Checkmate for Windows, Linux AND macOS in one go and packs each into a zip.

.DESCRIPTION
  Output goes to dist\release\ :
     Checkmate-windows-x64.zip   Checkmate.exe
     Checkmate-linux-x64.zip     Checkmate (Linux program)
     Checkmate-macos-arm64.zip   Checkmate.app (Apple Silicon)
     Checkmate-macos-x64.zip     Checkmate.app (Intel Macs)

  Run from the repo's scripts folder to build your working copy, or run on its own
  to download the code from GitHub first. Needs the .NET 8+ SDK (and Git when downloading).

  NOTE: Windows cannot store "this file is executable" or sign Mac apps. The Linux and
  macOS zips therefore contain a README-FIRST.txt with the two commands to run once
  after unzipping. For the cleanest result build natively on each system with
  build-linux.sh / build-macos.sh, or run build-all.sh on a Mac or Linux PC.

.PARAMETER Only       Which systems to build: windows, linux, macos (default: all three).
.PARAMETER NoPrompt   Never ask questions.
#>
[CmdletBinding()]
param(
    [string]$RepoUrl = 'https://github.com/Checkmate-Studios/Checkmate.git',
    [string]$Branch  = 'main',
    [ValidateSet('windows', 'linux', 'macos')][string[]]$Only = @('windows', 'linux', 'macos'),
    [string]$Version = '0.1.0',
    [switch]$NoPrompt
)

$ErrorActionPreference = 'Stop'

function Step($m) { Write-Host ''; Write-Host "==> $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "    $m" -ForegroundColor Green }
function Warn($m) { Write-Host "    $m" -ForegroundColor Yellow }
function Fail($m) { Write-Host ''; Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
function Ask($q)  {
    if ($NoPrompt) { return $false }
    $a = Read-Host "$q [Y/n]"
    return ($a -eq '' -or $a -match '^(y|yes)$')
}
function Test-Cmd($name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

Write-Host ''
Write-Host '  CHECKMATE - build for Windows + Linux + macOS' -ForegroundColor Magenta

function Install-WithWinget($id, $label) {
    if (-not (Test-Cmd 'winget')) { Fail "$label is missing and winget is not available. Please install it yourself, then run this script again." }
    if (Ask "$label is missing. Install it now with winget?") {
        winget install --id $id -e --accept-source-agreements --accept-package-agreements
        Write-Host ''
        Warn "$label was installed. Close this window, open a NEW one and run the script again."
        exit 0
    }
    Fail "$label is required."
}

function Get-DotnetMajor {
    if (-not (Test-Cmd 'dotnet')) { return 0 }
    $best = 0
    foreach ($line in (& dotnet --list-sdks 2>$null)) {
        if ($line -match '^(\d+)\.') { $best = [Math]::Max($best, [int]$Matches[1]) }
    }
    return $best
}

Step 'Checking tools'
$major = Get-DotnetMajor
if ($major -lt 8) { Install-WithWinget 'Microsoft.DotNet.SDK.8' '.NET 8 SDK' }
Ok ".NET SDK $major found"

$parent = Split-Path -Parent $PSScriptRoot
if (Test-Path (Join-Path $parent 'Checkmate.sln')) {
    $root = $parent
    Ok "Building your working copy: $root"
}
else {
    if (-not (Test-Cmd 'git')) { Install-WithWinget 'Git.Git' 'Git' }
    $root = Join-Path $PSScriptRoot 'Checkmate-source'
    if (Test-Path (Join-Path $root '.git')) {
        Step 'Updating Checkmate from GitHub'
        git -C $root pull --ff-only
        if ($LASTEXITCODE -ne 0) { Fail 'Could not update the downloaded code.' }
    }
    else {
        Step 'Downloading Checkmate from GitHub'
        git clone --depth 1 --branch $Branch $RepoUrl $root
        if ($LASTEXITCODE -ne 0) { Fail 'Could not download Checkmate. Check your internet connection and the repository address.' }
    }
}

# ---- what to build ----
$all = @(
    @{ Group = 'windows'; Name = 'windows-x64' ; Rid = 'win-x64'   },
    @{ Group = 'linux'  ; Name = 'linux-x64'   ; Rid = 'linux-x64' },
    @{ Group = 'macos'  ; Name = 'macos-arm64' ; Rid = 'osx-arm64' },
    @{ Group = 'macos'  ; Name = 'macos-x64'   ; Rid = 'osx-x64'   }
)
$targets = @($all | Where-Object { $Only -contains $_.Group })

$project = Join-Path $root 'src\Checkmate.Desktop\Checkmate.Desktop.csproj'
$dist = Join-Path $root 'dist'
$release = Join-Path $dist 'release'
New-Item -ItemType Directory -Force -Path $release | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$results = @()

foreach ($t in $targets) {
    Step "Building $($t.Name)"
    $bin = Join-Path $dist "$($t.Name)-bin"
    if (Test-Path $bin) { Remove-Item $bin -Recurse -Force }

    & dotnet publish $project -c Release -r $t.Rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
        -o $bin
    if ($LASTEXITCODE -ne 0) { Fail "The $($t.Name) build failed. Scroll up and look for the first 'error' line." }

    $pack = Join-Path $dist $t.Name
    if (Test-Path $pack) { Remove-Item $pack -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $pack | Out-Null

    switch ($t.Group) {
        'windows' {
            if (-not (Test-Path (Join-Path $bin 'Checkmate.exe'))) { Fail "Checkmate.exe was not created for $($t.Name)." }
            Copy-Item (Join-Path $bin '*') $pack -Recurse
        }
        'linux' {
            Copy-Item (Join-Path $bin '*') $pack -Recurse
            $note = "After unzipping, run once in a terminal in this folder:`n`n    chmod +x Checkmate`n    ./Checkmate`n`nIf it will not start, install the common desktop libraries:`n    Debian/Ubuntu: sudo apt install libfontconfig1 libx11-6 libice6 libsm6 libopenal1`n"
            [System.IO.File]::WriteAllText((Join-Path $pack 'README-FIRST.txt'), $note, $utf8)
        }
        'macos' {
            $app = Join-Path $pack 'Checkmate.app'
            $macos = Join-Path $app 'Contents\MacOS'
            New-Item -ItemType Directory -Force -Path $macos | Out-Null
            New-Item -ItemType Directory -Force -Path (Join-Path $app 'Contents\Resources') | Out-Null
            Copy-Item (Join-Path $bin '*') $macos -Recurse
            $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Checkmate</string>
  <key>CFBundleDisplayName</key><string>Checkmate</string>
  <key>CFBundleIdentifier</key><string>io.github.checkmate-studios.checkmate</string>
  <key>CFBundleVersion</key><string>$Version</string>
  <key>CFBundleShortVersionString</key><string>$Version</string>
  <key>CFBundleExecutable</key><string>Checkmate</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>10.15</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.graphics-design</string>
  <key>NSMicrophoneUsageDescription</key><string>Checkmate records your voice when you use Record voice.</string>
</dict>
</plist>
"@
            [System.IO.File]::WriteAllText((Join-Path $app 'Contents\Info.plist'), $plist, $utf8)
            $note = "After unzipping, open Terminal in this folder and run these two commands once:`n`n    chmod +x Checkmate.app/Contents/MacOS/Checkmate`n    xattr -cr Checkmate.app && codesign --force --deep --sign - Checkmate.app`n`nThen double-click Checkmate.app. (The second command gives the app a free local signature;`nApple Silicon Macs refuse to start unsigned programs.)`nIf macOS still warns you, right-click the app and choose Open.`n"
            [System.IO.File]::WriteAllText((Join-Path $pack 'README-FIRST.txt'), $note, $utf8)
        }
    }

    $zip = Join-Path $release "Checkmate-$($t.Name).zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $pack '*') -DestinationPath $zip
    Remove-Item $bin -Recurse -Force
    $mb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
    Ok "Packed: $zip ($mb MB)"
    $results += $zip
}

Write-Host ''
Write-Host 'Done! Your downloads are in:' -ForegroundColor Green
Write-Host "  $release" -ForegroundColor Green
foreach ($r in $results) { Write-Host "    $(Split-Path -Leaf $r)" }
Write-Host ''
if (Ask 'Open that folder now?') { Start-Process $release }

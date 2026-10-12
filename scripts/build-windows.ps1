<#
.SYNOPSIS
  Builds Checkmate into a single, ready-to-run Windows program (Checkmate.exe).

.DESCRIPTION
  * Run from the repo's scripts folder, it builds your working copy.
  * Run on its own (for example downloaded alone), it first downloads the code
    from GitHub into a "Checkmate-source" folder next to the script.
  * Needs the .NET 8+ SDK (and Git when it has to download). If they are missing,
    the script offers to install them with winget.
  * The resulting Checkmate.exe needs nothing else to run.

.PARAMETER Rid        win-x64 (default) or win-arm64 (chosen automatically on ARM PCs)
.PARAMETER Shortcut   Create a Desktop shortcut without asking.
.PARAMETER Run        Start Checkmate when the build is finished.
.PARAMETER NoPrompt   Never ask questions (for automation).
#>
[CmdletBinding()]
param(
    [string]$RepoUrl = 'https://github.com/Checkmate-Studios/Checkmate.git',
    [string]$Branch  = 'main',
    [ValidateSet('win-x64', 'win-arm64')][string]$Rid = 'win-x64',
    [switch]$Shortcut,
    [switch]$Run,
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
Write-Host '  CHECKMATE - Windows build' -ForegroundColor Magenta

if (-not $PSBoundParameters.ContainsKey('Rid') -and $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { $Rid = 'win-arm64' }

function Install-WithWinget($id, $label) {
    if (-not (Test-Cmd 'winget')) {
        Fail "$label is missing and winget is not available. Please install $label yourself, then run this script again."
    }
    if (Ask "$label is missing. Install it now with winget?") {
        winget install --id $id -e --accept-source-agreements --accept-package-agreements
        Write-Host ''
        Warn "$label was installed. Close this window, open a NEW one and run the script again (Windows needs a fresh window to find it)."
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

# ---- tools ----
Step 'Checking tools'
$major = Get-DotnetMajor
if ($major -lt 8) { Install-WithWinget 'Microsoft.DotNet.SDK.8' '.NET 8 SDK' }
Ok ".NET SDK $major found"

# ---- where is the code? ----
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

# ---- build ----
Step "Building Checkmate ($Rid). The first build downloads packages, so it can take a few minutes"
$project = Join-Path $root 'src\Checkmate.Desktop\Checkmate.Desktop.csproj'
$out = Join-Path $root "dist\$Rid"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

& dotnet publish $project -c Release -r $Rid --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o $out
if ($LASTEXITCODE -ne 0) { Fail "The build failed. Scroll up and look for the first 'error' line." }

$exe = Join-Path $out 'Checkmate.exe'
if (-not (Test-Path $exe)) { Fail "The build finished but Checkmate.exe was not created in $out" }
$mb = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
Ok "Built: $exe ($mb MB)"

# ---- optional extras ----
if ($Shortcut -or (Ask 'Create a Desktop shortcut?')) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $desktop 'Checkmate.lnk'))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $out
    $link.IconLocation = "$exe,0"
    $link.Description = 'Checkmate - 2D animation studio'
    $link.Save()
    Ok 'Desktop shortcut created.'
}

Write-Host ''
Write-Host "Done!  Your program is here:  $exe" -ForegroundColor Green
Write-Host ''

if ($Run -or (Ask 'Start Checkmate now?')) { Start-Process $exe }

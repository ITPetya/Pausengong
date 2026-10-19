<#
.SYNOPSIS
    Builds the one file that is actually handed to end users: a self-contained,
    single-file Pausengong.App.exe. No installer, no separate .NET runtime.

.DESCRIPTION
    Wraps the publish command documented in HANDOFF.md so it is run the same
    way every time, and fails loudly if the output is not the single exe the
    whole architecture promises (see HANDOFF.md "Hard constraint" / Gotchas:
    PublishSingleFile has previously dropped loose Content assets silently).

.NOTES
    Must be run on Windows with the .NET 8 SDK installed (see HANDOFF.md
    "Environment needed to build"). Not runnable or tested on this machine -
    no Windows available in this tool environment, see repo-wide note on that
    limitation.
#>

[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "..\publish")
)

$ErrorActionPreference = "Stop"
$repoRoot = Join-Path $PSScriptRoot ".."
$appProject = Join-Path $repoRoot "src\Pausengong.App\Pausengong.App.csproj"

Write-Host "Publishing $appProject -> $OutputDir" -ForegroundColor Cyan

dotnet publish $appProject `
    -c Release `
    -r win-x64 `
    -p:WindowsAppSDKSelfContained=true `
    -p:WindowsPackageType=None `
    -p:PublishSingleFile=true `
    --self-contained true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# The whole point of single-file publish is "nothing to install but the exe".
# Anything beyond the exe itself and an optional .pdb means something is
# silently relying on a loose file again (see HANDOFF.md Gotchas) - catch
# that here instead of only noticing when a user's machine is missing it.
$exe = Join-Path $OutputDir "Pausengong.App.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Expected $exe to exist after publish, but it does not."
    exit 1
}

$extraFiles = Get-ChildItem -Path $OutputDir -File |
    Where-Object { $_.Name -ne "Pausengong.App.exe" -and $_.Extension -ne ".pdb" }

if ($extraFiles) {
    Write-Warning "Output directory contains files besides the exe/pdb - single-file publish may not be self-contained as expected:"
    $extraFiles | ForEach-Object { Write-Warning "  $($_.Name)" }
    exit 1
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "OK: $exe ($sizeMb MB), nothing else in the output directory." -ForegroundColor Green
Write-Host "This is the only file a user needs - no installer, no .NET runtime, no NuGet cache." -ForegroundColor Green

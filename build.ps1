# Builds PawSip and its installer.
#   powershell -ExecutionPolicy Bypass -File build.ps1
# Output: dist\PawSip-Setup-<version>.exe  (the app itself is in publish\)
param(
    [string]$Version = "1.0.1",
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root "src\PawSip\PawSip.csproj"
$publish = Join-Path $root "publish"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Find-Tool([string[]]$candidates) {
    foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { return $c } }
    return $null
}

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = Find-Tool @("$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe", $(if ($dotnetCmd) { $dotnetCmd.Source }))
if (-not $dotnet) { throw ".NET 10 SDK not found. Install it from https://dot.net" }

$iscc = Find-Tool @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe")
if (-not $iscc -and -not $SkipInstaller) { throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php" }

Write-Host "==> Publishing PawSip $Version" -ForegroundColor Cyan
$running = Get-Process PawSip -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($publish, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "PawSip is running from $publish - exit it from the tray and build again." }
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish -p:DebugType=none -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

if ($SkipInstaller) {
    Write-Host "Done: $publish" -ForegroundColor Green
    return
}

Write-Host "==> Compiling installer" -ForegroundColor Cyan
& $iscc "/DAppVersion=$Version" (Join-Path $root "installer\PawSip.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

$setup = Join-Path $root "dist\PawSip-Setup-$Version.exe"
Write-Host ("Done: {0} ({1:N0} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green

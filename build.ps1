# Builds PawSip for every supported Windows version and creates the installers.
#   powershell -ExecutionPolicy Bypass -File build.ps1                    # all editions
#   powershell -ExecutionPolicy Bypass -File build.ps1 -Editions x64      # just one
# Output: dist\PawSip-Setup-<version>-<edition>.exe and dist\SHA256SUMS.txt
param(
    [string]$Version = "1.0.1",
    [ValidateSet("x64", "arm64", "x86", "legacy")]
    [string[]]$Editions = @("x64", "arm64", "x86", "legacy"),
    [switch]$SkipInstaller
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root "src\PawSip\PawSip.csproj"
$publishRoot = Join-Path $root "publish"
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

$running = Get-Process PawSip -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($publishRoot, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "PawSip is running from $publishRoot - exit it from the tray and build again." }

foreach ($edition in $Editions) {
    $out = Join-Path $publishRoot $edition
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    if ($edition -eq "legacy") {
        # Windows 7 SP1 / 8 / 8.1: .NET Framework 4.8, which Windows already has (or gets from Windows Update).
        Write-Host "==> Publishing PawSip $Version for Windows 7 / 8 (.NET Framework 4.8)" -ForegroundColor Cyan
        & $dotnet publish $project -c Release -f net48 -o $out -p:DebugType=none -p:Version=$Version
    }
    else {
        # Windows 10 / 11: self-contained, nothing else to install.
        Write-Host "==> Publishing PawSip $Version for Windows 10 / 11 ($edition)" -ForegroundColor Cyan
        & $dotnet publish $project -c Release -f net10.0-windows -r "win-$edition" --self-contained true -o $out -p:DebugType=none -p:Version=$Version
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $edition" }

    if (-not $SkipInstaller) {
        Write-Host "==> Compiling installer ($edition)" -ForegroundColor Cyan
        & $iscc /Q "/DAppVersion=$Version" "/DEdition=$edition" (Join-Path $root "installer\PawSip.iss")
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $edition" }
    }
}

if (-not $SkipInstaller) {
    $sums = Get-ChildItem (Join-Path $root "dist") -Filter "PawSip-Setup-$Version-*.exe" | Sort-Object Name | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
    }
    Set-Content -Path (Join-Path $root "dist\SHA256SUMS.txt") -Value $sums -Encoding ASCII
    Get-ChildItem (Join-Path $root "dist") -Filter "PawSip-Setup-$Version-*.exe" | Sort-Object Name | ForEach-Object {
        Write-Host ("Done: {0} ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor Green
    }
}

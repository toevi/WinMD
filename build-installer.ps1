# build-installer.ps1 — buduje WinMD-Setup.exe
# Użycie: .\build-installer.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root    = $PSScriptRoot
$tf      = "net10.0-windows10.0.19041.0"
$rid     = "win-x64"
$publish = "$root\bin\Release\$tf\$rid\publish"

Write-Host "==> dotnet publish (Release)..." -ForegroundColor Cyan
dotnet publish "$root\WinMD.csproj" `
    -c Release -f $tf -r $rid `
    --self-contained false `
    -p:PublishReadyToRun=true `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish nie powiodlo sie" }

# Szukamy ISCC.exe
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host ""
    Write-Host "UWAGA: Inno Setup nie znaleziony." -ForegroundColor Yellow
    Write-Host "Pobierz z: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "Po instalacji uruchom skrypt ponownie."
    exit 1
}

Write-Host "==> Kompilacja instalatora ($iscc)..." -ForegroundColor Cyan
& $iscc "$root\installer\WinMD.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC.exe nie powiodlo sie" }

Write-Host ""
Write-Host "Gotowe: $root\installer\WinMD-Setup.exe" -ForegroundColor Green

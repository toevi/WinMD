# build-installer.ps1 — buduje NIEPODPISANY instalator WinMD-Setup.exe.
#
# Dla kazdego, kto chce zbudowac wlasny instalator z kodu. Robi:
#   1. dotnet publish (Release, win-x64, self-contained=false),
#   2. kompiluje instalator przez Inno Setup (ISCC) -> installer\WinMD-Setup.exe.
#
# Wynik jest NIEPODPISANY (Authenticode). Oficjalne, podpisane wydania robi autor
# osobnym, prywatnym skryptem ze swoim certyfikatem — ten plik tego nie zawiera.
#
# Wymagania: .NET SDK 10, Inno Setup 6 (https://jrsoftware.org/isdl.php).
# Uzycie:  pwsh .\build-installer.ps1

$ErrorActionPreference = 'Stop'

$Root      = $PSScriptRoot
$Framework = 'net10.0-windows10.0.19041.0'
$Csproj    = Join-Path $Root 'WinMD.csproj'
$IssFile   = Join-Path $Root 'installer\WinMD.iss'

function Find-ISCC {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw 'Nie znaleziono ISCC.exe — zainstaluj Inno Setup 6: https://jrsoftware.org/isdl.php'
}

Write-Host '=== dotnet publish (Release) ===' -ForegroundColor Cyan
dotnet publish $Csproj -c Release -f $Framework -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish zwrocil kod $LASTEXITCODE" }

$ISCC = Find-ISCC
Write-Host "`n=== ISCC (kompilacja, niepodpisane) ===" -ForegroundColor Cyan
Write-Host "ISCC: $ISCC"
& $ISCC $IssFile
if ($LASTEXITCODE -ne 0) { throw "ISCC zwrocil kod $LASTEXITCODE" }

Write-Host "`nGotowe (niepodpisany): $(Join-Path $Root 'installer\WinMD-Setup.exe')" -ForegroundColor Green

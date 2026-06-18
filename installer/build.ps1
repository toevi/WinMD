# Buduje podpisany instalator WinMD.
#
# Co robi:
#   1. publikuje aplikację (Release, win-x64, self-contained=false),
#   2. podpisuje WinMD.exe podpisem Authenticode (cert z magazynu, po odcisku palca),
#   3. kompiluje instalator przez ISCC, który podpisuje WinMD-Setup.exe oraz deinstalator.
#
# Certyfikat (CN=tmfgroup) musi być zainstalowany w magazynie (CurrentUser\My lub
# LocalMachine\My). Plik Certyfikat-tmfgroup.pfx leży w katalogu repo na wszelki wypadek.
#
# Użycie:  pwsh installer\build.ps1            (z dowolnego katalogu)

$ErrorActionPreference = 'Stop'

# ─── Stałe ───────────────────────────────────────────────────────────────────
$Thumbprint   = '73BB4C564E8A159034F854A6840CC04E5F77614C'  # CN=tmfgroup
$TimestampUrl = 'http://timestamp.digicert.com'

$RepoRoot   = Split-Path $PSScriptRoot -Parent
$Framework  = 'net10.0-windows10.0.19041.0'
$PublishDir = Join-Path $RepoRoot "bin\Release\$Framework\win-x64\publish"
$Csproj     = Join-Path $RepoRoot 'WinMD.csproj'
$IssFile    = Join-Path $PSScriptRoot 'WinMD.iss'

# ─── Lokalizacja narzędzi ──────────────────────────────────────────────────────
function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools",
        "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools"
    )
    foreach ($r in $roots) {
        if (Test-Path $r) {
            $hit = Get-ChildItem $r -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
                   Where-Object { $_.FullName -match '\\x64\\' } |
                   Sort-Object FullName -Descending | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    throw 'Nie znaleziono signtool.exe (Windows SDK / SDK BuildTools).'
}

function Find-ISCC {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw 'Nie znaleziono ISCC.exe (Inno Setup 6).'
}

$SignTool = Find-SignTool
$ISCC     = Find-ISCC
Write-Host "signtool: $SignTool"
Write-Host "ISCC:     $ISCC"

# ─── 1. Publikacja ─────────────────────────────────────────────────────────────
Write-Host "`n=== dotnet publish ===" -ForegroundColor Cyan
dotnet publish $Csproj -c Release -f $Framework -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish zwrócił kod $LASTEXITCODE" }

# ─── 2. Podpisanie aplikacji ─────────────────────────────────────────────────────
$AppExe = Join-Path $PublishDir 'WinMD.exe'
if (-not (Test-Path $AppExe)) { throw "Nie znaleziono $AppExe" }

Write-Host "`n=== Podpisywanie WinMD.exe ===" -ForegroundColor Cyan
& $SignTool sign /sm /sha1 $Thumbprint /fd sha256 /tr $TimestampUrl /td sha256 $AppExe
if ($LASTEXITCODE -ne 0) { throw "Podpisanie WinMD.exe nie powiodło się (kod $LASTEXITCODE)" }

# ─── 3. Kompilacja podpisanego instalatora ──────────────────────────────────────
# Inno Setup używa narzędzia o nazwie "winmdsign" (dyrektywa SignTool w .iss).
# Ścieżka do signtool zawiera spacje, a PowerShell 5.1 źle cytuje takie argumenty
# przy przekazywaniu do ISCC. Dlatego generujemy wrapper sign.cmd (jego ścieżka nie
# ma spacji) i przekazujemy do ISCC tylko: <wrapper> $f  (gdzie $f to pliki od Inno).
$Wrapper = Join-Path $PSScriptRoot 'sign.cmd'
@"
@echo off
rem Wrapper podpisujący wywoływany przez Inno Setup (SignTool=winmdsign).
rem Generowany automatycznie przez build.ps1 — nie edytuj ręcznie.
rem %* = lista plików przekazana przez Inno (placeholder `$f).
"$SignTool" sign /sm /sha1 $Thumbprint /fd sha256 /tr $TimestampUrl /td sha256 %*
"@ | Set-Content -Path $Wrapper -Encoding ascii

Write-Host "`n=== ISCC (kompilacja + podpis instalatora) ===" -ForegroundColor Cyan
& $ISCC "/Swinmdsign=$Wrapper `$f" $IssFile
if ($LASTEXITCODE -ne 0) { throw "ISCC zwrócił kod $LASTEXITCODE" }

Write-Host "`nGotowe. Podpisany instalator: $(Join-Path $PSScriptRoot 'WinMD-Setup.exe')" -ForegroundColor Green

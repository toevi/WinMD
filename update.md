# Workflow — nowa wersja WinMD

## Kroki przy każdym release

1. **Kod** — zmieniasz, kommiujesz, `git push`
2. **Instalator** — budujesz nowy `WinMD-Setup.exe`
3. **GitHub Release** — tworzysz release `v1.1.0` i wgrywasz nowy instalator jako asset
4. **winget** — składasz nowy PR do `microsoft/winget-pkgs` z manifestem `1.1.0` (nowy URL + nowy SHA256)

Kroki 1–3 robisz sam. Krok 4 może zrobić Claude — wystarczy powiedzieć:
> „zaktualizuj winget do v1.1.0"
i gotowe w minutę.

## Czego NIE musisz robić

- Nie aktualizujesz starego PR do winget — każda wersja to **nowy PR** z nowym podkatalogiem `manifests/t/toevi/WinMD/1.1.0/`
- Stary release na GitHubie zostaje — użytkownicy na starszej wersji Windowsa mogą go potrzebować

## Pamiętaj przy każdej wersji

W `WinMD.csproj` zmień `<Version>1.0.0</Version>` na nową wersję (np. `1.1.0`), żeby wersja w `.exe` zgadzała się z wersją w winget.

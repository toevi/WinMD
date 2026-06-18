# WinMD

Edytor i przeglądarka plików Markdown dla Windows — lekki, szybki, bez instalacji MSIX.

## Funkcje

- **Edycja** z paskiem formatowania (pogrubienie, kursywa, nagłówki, listy, linki, kod)
- **Podgląd** HTML renderowany przez Markdig z motywem GitHub (jasny / ciemny)
- **Eksport do PDF** przez WebView2
- **Znajdź / zamień** w edytorze i znajdź w podglądzie (wstrzyknięty JS)
- **Undo / redo** z koalescencją naciśnięć klawiatury (600 ms debounce)
- **Skojarzenie pliku** — otwieranie `.md` z Eksploratora przez dwuklik
- **Skróty klawiszowe**: Ctrl+N/O/S, Ctrl+P (PDF), Ctrl+B/I, Ctrl+E (tryb), Ctrl+F/H, F3/Shift+F3, F1

## Wymagania

- Windows 10 w. 1903 (19041) lub nowszy (x64 lub ARM64)
- [Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) — instalator pobiera go automatycznie

## Kompilacja

Wymagania:
- .NET 10 SDK
- Workload `microsoft-windows-sdk-net`: `dotnet workload install microsoft-windows-sdk-net`

```powershell
# Budowanie
dotnet build -c Debug -f net10.0-windows10.0.19041.0

# Budowanie i uruchamianie
dotnet build -c Debug -t:Run -f net10.0-windows10.0.19041.0

# Publikacja (self-contained, win-x64)
dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64
```

## Architektura

Aplikacja WinUI 3 (Windows App SDK 1.8), unpackaged `.exe`, wzorzec MVVM.

| Warstwa | Opis |
|---------|------|
| `MainWindow.xaml` | Główne okno — pasek narzędziowy i panel edytora/podglądu |
| `HelpWindow.xaml` | Okno pomocy z listą skrótów |
| `ViewModels/EditorViewModel.cs` | Jedyny ViewModel — cały stan aplikacji |
| `Services/MarkdownService.cs` | Konwersja Markdown → HTML (Markdig + szablon CSS) |
| `Services/WindowsFileService.cs` | WinRT picker + System.IO |
| `Services/WindowsPdfExporter.cs` | Eksport PDF przez WebView2 |
| `Models/MarkdownDocument.cs` | Nośnik danych dokumentu |
| `Platforms/Windows/FileAssociation.cs` | Rejestracja skojarzenia `.md` w HKCU |

## Stos technologiczny

- [Windows App SDK 1.8](https://github.com/microsoft/WindowsAppSDK) — WinUI 3, WebView2
- [CommunityToolkit.Mvvm 8.4](https://github.com/CommunityToolkit/dotnet) — `ObservableObject`, `RelayCommand`
- [Markdig 1.3](https://github.com/xoofx/markdig) — parser Markdown

## Licencja

[MIT](LICENSE)

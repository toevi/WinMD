# WinMD

A lightweight Markdown editor and viewer for Windows — no MSIX, no Store, just a plain `.exe`.

## Features

- **Editor** with a formatting toolbar (bold, italic, headings, lists, links, code)
- **Live preview** rendered by Markdig with a GitHub-style stylesheet (light / dark theme)
- **PDF export** via WebView2
- **Find / Replace** in the editor; find in the rendered preview (injected JS)
- **Undo / Redo** with keystroke coalescing (600 ms debounce groups a typing burst into one history step)
- **File association** — open `.md` files by double-clicking in Explorer
- **Keyboard shortcuts**: Ctrl+N/O/S, Ctrl+P (PDF), Ctrl+B/I, Ctrl+E (toggle editor/preview), Ctrl+F/H, F3/Shift+F3, F1 (help)

## Requirements

- Windows 10 version 1903 (19041) or later — x64 or ARM64
- [Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) — the installer downloads it automatically if missing

## Installation

Download **WinMD-Setup.exe** from the [latest release](https://github.com/toevi/WinMD/releases/latest) and run it.

## Building from source

Prerequisites: .NET 10 SDK and the Windows SDK workload:

```powershell
dotnet workload install microsoft-windows-sdk-net
```

```powershell
# Debug build
dotnet build -c Debug -f net10.0-windows10.0.19041.0

# Build and run
dotnet build -c Debug -t:Run -f net10.0-windows10.0.19041.0

# Release publish (self-contained, win-x64)
dotnet publish -c Release -f net10.0-windows10.0.19041.0 -r win-x64
```

## Architecture

WinUI 3 app (Windows App SDK 1.8), unpackaged `.exe`, MVVM pattern.

| Layer | Description |
|-------|-------------|
| `MainWindow.xaml` | Main window — toolbar and editor/preview panel |
| `HelpWindow.xaml` | Help window with keyboard shortcut reference |
| `ViewModels/EditorViewModel.cs` | Single ViewModel — all application state |
| `Services/MarkdownService.cs` | Markdown → HTML conversion (Markdig + CSS template) |
| `Services/WindowsFileService.cs` | WinRT pickers + System.IO |
| `Services/WindowsPdfExporter.cs` | PDF export via WebView2 |
| `Models/MarkdownDocument.cs` | Document data carrier |
| `Platforms/Windows/FileAssociation.cs` | `.md` file association registered in HKCU |

## Tech stack

- [Windows App SDK 1.8](https://github.com/microsoft/WindowsAppSDK) — WinUI 3, WebView2
- [CommunityToolkit.Mvvm 8.4](https://github.com/CommunityToolkit/dotnet) — `ObservableObject`, `RelayCommand`
- [Markdig 1.3](https://github.com/xoofx/markdig) — Markdown parser

## License

[MIT](LICENSE)

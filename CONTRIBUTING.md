# Contributing to WinMD

Thanks for your interest in WinMD — a lightweight Markdown editor and viewer for Windows
(WinUI 3 / Windows App SDK, unpackaged `.exe`).

## Prerequisites

- **Windows 10 (19041)** or later — x64 or ARM64
- **.NET SDK 10** with the Windows workload
- **Windows App SDK 1.8** (restored automatically as a NuGet package for building)
- *(optional, only for building an installer)* **Inno Setup 6** — https://jrsoftware.org/isdl.php

## Build & run

```powershell
# Build
dotnet build -c Debug -f net10.0-windows10.0.19041.0

# Build + run
dotnet build -c Debug -t:Run -f net10.0-windows10.0.19041.0
```

There is no test project and no linter configured.

### Building your own installer (optional)

```powershell
pwsh .\build-installer.ps1
```

This produces an **unsigned** `installer\WinMD-Setup.exe`. Official releases are signed by the
maintainer with a private certificate that is **not** part of this repository.

## Project layout (quick map)

- `App.xaml.cs` / `Program.cs` — startup and manual composition root (no DI container)
- `MainWindow.xaml(.cs)` — the single app window; also implements `IUiService`
- `ViewModels/EditorViewModel.cs` — single source of truth (document, formatting, find/replace, undo/redo, theme)
- `Services/` — `MarkdownService` (Markdig → HTML), file/PDF/UI/settings abstractions
- `Platforms/Windows/` — `WindowsFileService`, `WindowsPdfExporter`, file association, unsaved-changes guard
- `Models/` — `MarkdownDocument`, `MarkdownTip`

## Coding conventions

- Nullable reference types and implicit usings are enabled.
- The codebase mixes Polish and English in code/comments — **match the surrounding file**.
- Keep changes focused; follow the style of the file you are editing.

## Submitting changes

1. Fork the repo and create a topic branch off `master`.
2. Make your change and confirm it builds (`dotnet build -c Debug -f net10.0-windows10.0.19041.0`).
3. Open a Pull Request with a clear description of **what** and **why**.
4. Keep PRs small and self-contained where possible.

## Reporting bugs / requesting features

Use the issue templates (Bug report / Feature request). For **security** issues, do **not** open a
public issue — see [SECURITY.md](SECURITY.md).

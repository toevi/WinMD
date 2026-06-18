# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Zasady współpracy

- **Komunikujemy się wyłącznie po polsku.**
- Masz **pełną autonomię** — działaj samodzielnie, nie pytaj o pozwolenie na każdy krok.
- Kompiluj, sprawdzaj i uruchamiaj samodzielnie, kiedy tego potrzebujesz (`dotnet build -c Debug -f net10.0-windows10.0.19041.0`).

## What this is

WinMD is a **.NET MAUI Markdown editor/reader targeting Windows only** (`net10.0-windows10.0.19041.0`, unpackaged desktop `.exe`). It opens, edits, previews, and saves `.md` files. MVVM with CommunityToolkit.Mvvm; Markdown rendering via Markdig; file access via the Windows file system (WinRT pickers + `System.IO.File`).

> Aplikacja nazywa się **WinMD** (`ApplicationId` `com.tmfgroup.winmd`, namespace `WinMD`, wyjściowy plik `WinMD.exe`). Powstała jako apka Android-first o nazwie „AndroidMD" i została przemianowana po przejściu na wyłącznie Windows. Wersja na Androida żyje w osobnym repozytorium. Uwaga: katalog repo to nadal `AndroidMDzWindows` (nie zmienialiśmy nazwy folderu).

## Build & run

PowerShell on Windows. Code/comments are a mix of Polish and English — match the surrounding file.

- Plain build: `dotnet build -c Debug -f net10.0-windows10.0.19041.0`
- Build + run: `dotnet build -c Debug -t:Run -f net10.0-windows10.0.19041.0`
- App id: `com.tmfgroup.winmd`
- `WindowsPackageType=None` — aplikacja jest **unpackaged** (zwykły `.exe`, bez MSIX).
- There is **no test project** and no linter configured.
- Instalator: `installer/WinMD.iss` (Inno Setup).

### Critical: MauiVersion is pinned

`WinMD.csproj` forces `<MauiVersion>10.0.60</MauiVersion>`. This is required by CommunityToolkit.Maui 14.2.0 (needs >= 10.0.60); the default workload resolves 10.0.20, which causes an NU1605 downgrade conflict. Do not lower or remove this.

## Architecture

The whole app is built around **one shared `EditorViewModel` singleton** (registered in `MauiProgram.cs`) that backs the UI. Understanding this VM is most of understanding the app.

- **Shell / navigation** (`AppShell.xaml`): a single `<Tab>` with two `ShellContent`s — `EditPage` (route `edit`) and `PreviewPage` (route `preview`). The Shell tab bar is hidden (`Shell.SetTabBarIsVisible(false)`); the view is switched by two buttons at the top of the page. `HelpPage` is registered as route `help` in `AppShell.xaml.cs` and pushed from the `⋮` menu.
- **Podgląd jako nakładka:** on the desktop the preview is shown as an **overlay inside `EditPage`** (`EditorViewModel.IsPreviewMode`, the `GoToEdit`/`GoToPreview` commands toggle it) — the editor stays laid out underneath, which avoids buggy star-row re-arrange on switch. The separate `PreviewPage` `ShellContent` is vestigial (kept, but not navigated to).
- **`EditorViewModel`** (`ViewModels/`): single source of truth. Holds the `MarkdownDocument`, `EditorText`, dirty-state tracking (`IsDirty` compares `EditorText` to `_savedContent`), the formatting bar (`ApplyFormatCommand` with `bold`/`italic`/`code`/`h1`/`h2`/`quote`/`ul`/`ol`/`link`), find/replace, light/dark theme (`IsDarkTheme`, persisted in `Preferences`), and a **custom undo/redo stack** with keystroke coalescing (600 ms debounce timer groups a typing burst into one history step; formatting actions commit immediately).
- **Preview rendering**: `EditorViewModel.RefreshPreview()` runs `MarkdownService.ToHtml` and pushes the result into a `WebView` via `HtmlWebViewSource`. Called when the preview overlay/tab is shown and after loading a file.
- **`MarkdownService`** (`Services/`): wraps Markdig (`UseAdvancedExtensions` + soft-break-as-hard-break) and injects the fragment into a self-contained HTML5 template with an inline GitHub-style stylesheet (no external assets — works offline in WebView). The template uses `%%ROOT%%` (light/dark CSS palette), `%%TOC%%`, and `%%BODY%%` placeholders. Both **light and dark** themes are supported.
- **`MarkdownDocument`** (`Models/`): plain data carrier. `Identifier` is the full file path on disk; `IsPersisted` (non-empty Identifier) distinguishes a saved file from a new unsaved doc, which drives whether Save creates vs. overwrites.

### File access (WinRT pickers + System.IO)

- `IFileService` (`Services/IFileService.cs`) is the abstraction; `WindowsFileService` (`Platforms/Windows/WindowsFileService.cs`) is the only implementation.
- Open/Create use WinRT `FileOpenPicker` / `FileSavePicker` (bound to the window HWND via `InitializeWithWindow`); read/write/rename go directly through `System.IO.File` on the stored path. Save uses UTF-8 **without BOM**.
- **Open from outside** (file association / double-click in Explorer): `Platforms/Windows/FileAssociation.cs` registers the `.md` ProgID under `HKCU` (no admin, idempotent). The launched `.exe` gets the path as a command-line arg; `Platforms/Windows/App.xaml.cs` reads `Environment.GetCommandLineArgs()` and calls `EditorViewModel.OpenFromExternalAsync(path)`.

### Keyboard shortcuts

Wired in `Views/EditPage.xaml.cs` via WinUI `KeyboardAccelerator`s attached to the page's platform view (`SetupKeyboardShortcutsOnce`, on `Loaded`). They map to `EditorViewModel` commands; Find shortcuts are **context-aware** (editor vs. preview): Ctrl+N/O/S, Ctrl+P (PDF), Ctrl+B/I (format), Ctrl+E (toggle Edit/Preview), Ctrl+F (find), Ctrl+H (replace), F3 / Shift+F3 (next/prev match), Esc (close find), F1 (help). The Help page lists them (`BuildShortcutsHtml`).

When focus is **inside the preview WebView2**, host accelerators don't fire, so Ctrl+F / F3 / Esc are bridged: `EnsurePreviewWebInitAsync` disables Edge's own accelerator keys (`AreBrowserAcceleratorKeysEnabled=false`), injects a `keydown` listener (`PreviewKeyScript`) that `postMessage`s to native, and handles `WebMessageReceived`.

### Preview find (WebView2)

The preview (overlay in `EditPage`) has its own find bar (🔍) operating on the **rendered HTML**, not the `.md` source. WebView2 1.0.3179.45's managed SDK has no programmatic `CoreWebView2.Find`, so it's done by injecting JS (`PreviewFindScript` → `window.__wmdFind`) via `ExecuteScriptAsync`: wraps matches in `<mark>`, highlights/scrolls the active one, returns `{count, active}` for the counter. See `Docs/prompt.md`.

### Window lifecycle & unsaved-changes guard

- `App.xaml.cs` maximizes the window on start and forces a full native re-layout on every resize (MAUI doesn't always recompute the editor's star-row arrange).
- Closing the window (the „X") is intercepted by `Platforms/Windows/UnsavedChangesGuard.Attach` (wired in `MauiProgram.cs` via lifecycle events): it checks `EditorViewModel.IsDirty` and prompts before exiting.

## History

This was originally an Android-first MAUI app; the Android target and the entire `Platforms/Android` tree were removed once the app went Windows-only. If you find lingering Android-flavored comments or naming, treat them as historical, not as a second target to support.

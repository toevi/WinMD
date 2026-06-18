using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinMD.Models;
using WinMD.Services;
using Windows.Storage.Pickers;

namespace WinMD.Platforms.Windows;

/// <summary>
/// Implementacja <see cref="IFileService"/> dla Windows (desktop, unpackaged).
/// Pliki identyfikowane są pełną ścieżką w systemie plików (<see cref="MarkdownDocument.Identifier"/>).
/// Wybór plików odbywa się przez systemowe pickery WinRT; odczyt/zapis przez <see cref="System.IO.File"/>.
/// </summary>
public sealed class WindowsFileService : IFileService
{
    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<MarkdownDocument?> OpenAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".markdown");
            InitializeWithWindow(picker);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return null; // anulowano

            return await ReadDocumentAsync(file.Path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] OpenAsync failed: {ex}");
            return null;
        }
    }

    public async Task<MarkdownDocument?> OpenFromUriAsync(string uri)
    {
        try
        {
            // Na Windows „uri" to po prostu ścieżka pliku przekazana przez skojarzenie .md.
            var path = uri;
            if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(path, UriKind.Absolute, out var parsed))
            {
                path = parsed.LocalPath;
            }

            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[WindowsFileService] OpenFromUriAsync: plik nie istnieje: {path}");
                return null;
            }

            return await ReadDocumentAsync(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] OpenFromUriAsync failed: {ex}");
            return null;
        }
    }

    public async Task<bool> SaveAsync(MarkdownDocument document)
    {
        try
        {
            if (string.IsNullOrEmpty(document.Identifier))
                return false;

            await File.WriteAllTextAsync(document.Identifier, document.Content ?? string.Empty, Utf8NoBom);
            return true;
        }
        catch (Exception ex)
        {
            // Np. plik tylko-do-odczytu → VM zaproponuje „Zapisz jako".
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] SaveAsync failed: {ex}");
            return false;
        }
    }

    public async Task<MarkdownDocument?> CreateAsync(string suggestedName, string content)
    {
        try
        {
            var name = EnsureExtension(suggestedName, ".md");
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(name),
            };
            picker.FileTypeChoices.Add("Markdown", new List<string> { ".md", ".markdown" });
            InitializeWithWindow(picker);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return null; // anulowano

            await File.WriteAllTextAsync(file.Path, content ?? string.Empty, Utf8NoBom);

            return new MarkdownDocument
            {
                Identifier = file.Path,
                FileName = file.Name,
                Content = content ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] CreateAsync failed: {ex}");
            return null;
        }
    }

    public async Task<bool> ExportTextAsync(string suggestedName, string mimeType, string content)
    {
        try
        {
            var ext = Path.GetExtension(suggestedName);
            if (string.IsNullOrEmpty(ext))
                ext = ".txt";

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName),
            };
            picker.FileTypeChoices.Add(ext.TrimStart('.').ToUpperInvariant(), new List<string> { ext });
            InitializeWithWindow(picker);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return false; // anulowano

            await File.WriteAllTextAsync(file.Path, content ?? string.Empty, Utf8NoBom);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] ExportTextAsync failed: {ex}");
            return false;
        }
    }

    public Task<bool> RenameAsync(MarkdownDocument document, string newName)
    {
        try
        {
            if (string.IsNullOrEmpty(document.Identifier))
                return Task.FromResult(false);

            var dir = Path.GetDirectoryName(document.Identifier);
            if (dir is null)
                return Task.FromResult(false);

            var targetName = EnsureExtension(newName, ".md");
            var targetPath = Path.Combine(dir, targetName);

            File.Move(document.Identifier, targetPath, overwrite: false);

            document.Identifier = targetPath;
            document.FileName = targetName;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsFileService] RenameAsync failed: {ex}");
            return Task.FromResult(false);
        }
    }

    private static async Task<MarkdownDocument?> ReadDocumentAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path);
        return new MarkdownDocument
        {
            Identifier = path,
            FileName = Path.GetFileName(path),
            Content = content
        };
    }

    private static string EnsureExtension(string name, string ext)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Untitled" + ext;

        return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? name
            : name + ext;
    }

    /// <summary>Pickery WinRT w aplikacji desktop wymagają powiązania z uchwytem okna (HWND).</summary>
    private static void InitializeWithWindow(object picker)
    {
        if (WinMD.App.MainWindow is { } window)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}

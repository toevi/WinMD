using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WinMD.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;

namespace WinMD.Platforms.Windows;

/// <summary>
/// Implementacja <see cref="IPdfExporter"/> dla Windows oparta o WebView2.
/// „Export PDF" pyta o lokalizację (FileSavePicker) i renderuje plik po cichu przez PrintToPdf —
/// świadomie bez systemowego dialogu druku, który w aplikacji unpackaged otwiera się poza ekranem
/// (jest przyczepiony do off-screen WebView2 w procesie msedgewebview2.exe). Udostępnianie PDF
/// korzysta z tego samego renderu do pliku tymczasowego.
/// </summary>
public sealed class WindowsPdfExporter : IPdfExporter
{
    public async Task<bool?> ExportAsync(string html, string jobName)
    {
        WebView2? webView = null;
        Popup? popup = null;
        try
        {
            var name = string.IsNullOrWhiteSpace(jobName) ? "document" : jobName;

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = name,
            };
            picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
            InitializeWithWindow(picker);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return null; // użytkownik anulował — to nie błąd

            (webView, popup) = CreateOffScreenWebView();
            var core = await LoadHtmlAsync(webView, html);
            if (core is null)
                return false;

            return await core.PrintToPdfAsync(file.Path, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] ExportAsync failed: {ex}");
            return false;
        }
        finally
        {
            Detach(popup, webView);
        }
    }

    public async Task<string?> RenderToFileAsync(string html, string fileName)
    {
        WebView2? webView = null;
        Popup? popup = null;
        try
        {
            (webView, popup) = CreateOffScreenWebView();
            var core = await LoadHtmlAsync(webView, html);
            if (core is null)
                return null;

            var path = Path.Combine(Path.GetTempPath(), fileName);
            var ok = await core.PrintToPdfAsync(path, null);
            return ok ? path : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] RenderToFileAsync failed: {ex}");
            return null;
        }
        finally
        {
            Detach(popup, webView);
        }
    }

    private static (WebView2 webView, Popup popup) CreateOffScreenWebView()
    {
        // ~A4 @96dpi; poza ekranem (nie miga), ale w drzewie wizualnym (potrzebny HWND).
        var webView = new WebView2 { Width = 794, Height = 1123 };
        var popup = new Popup
        {
            Child = webView,
            HorizontalOffset = -100000,
            VerticalOffset = -100000,
        };

        if (WinMD.App.MainWindow?.Content is FrameworkElement root)
        {
            popup.XamlRoot = root.XamlRoot;
        }

        popup.IsOpen = true;
        return (webView, popup);
    }

    private static async Task<CoreWebView2?> LoadHtmlAsync(WebView2 webView, string html)
    {
        await webView.EnsureCoreWebView2Async();
        var core = webView.CoreWebView2;
        if (core is null)
            return null;

        var tcs = new TaskCompletionSource<bool>();
        void OnCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            core.NavigationCompleted -= OnCompleted;
            tcs.TrySetResult(e.IsSuccess);
        }

        core.NavigationCompleted += OnCompleted;
        core.NavigateToString(html);
        await tcs.Task;
        return core;
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

    private static void Detach(Popup? popup, WebView2? webView)
    {
        try
        {
            if (popup is not null)
                popup.IsOpen = false;
            webView?.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] Detach failed: {ex}");
        }
    }
}

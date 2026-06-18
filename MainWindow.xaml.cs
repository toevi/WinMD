using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using WinMD.Services;
using WinMD.ViewModels;
using Windows.System;

namespace WinMD;

public sealed partial class MainWindow : Window, IUiService
{
    private EditorViewModel _vm = null!;
    private bool _suppressSelectionSync;

    public MainWindow()
    {
        InitializeComponent();

        // Ikona w belce okna (i na pasku zadań).
        AppWindow.SetIcon(System.IO.Path.Combine(
            System.AppContext.BaseDirectory, "icon.ico"));

        // Start zmaksymalizowany — naturalny tryb edytora na desktopie.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    /// <summary>Podpina ViewModel (DataContext), zdarzenia, motyw, skróty i pierwszy render podglądu.</summary>
    public void Initialize(EditorViewModel vm)
    {
        _vm = vm;
        Root.DataContext = vm;

        vm.SelectionRequested += OnSelectionRequested;
        vm.FocusFindRequested += OnFocusFindRequested;
        vm.PreviewHtmlChanged += OnPreviewHtmlChanged;
        vm.PropertyChanged += OnVmPropertyChanged;

        MarkdownEditor.SelectionChanged += OnEditorSelectionChanged;

        // Domyślnie WinUI maluje zaznaczenie prawie niewidocznym kolorem, gdy TextBox nie ma
        // fokusu — przez to trafienie wyszukiwania jest niewidoczne podczas pisania w polu
        // szukania. Ustawiamy wyraźny bursztyn (jak <mark> w podglądzie).
        var matchHighlight = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xD5, 0x4F));
        MarkdownEditor.SelectionHighlightColorWhenNotFocused = matchHighlight;

        ApplyTheme(vm.IsDarkTheme);
        Title = "WinMD";
        SetupKeyboardShortcuts();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.Title))
            Title = _vm.Title;
        else if (e.PropertyName == nameof(EditorViewModel.IsPreviewMode) && !_vm.IsPreviewMode)
            ClosePreviewFind();
    }

    // ───────────────────────── Edytor: tekst i zaznaczenie ─────────────────────────

    private void OnMarkdownEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        // W WinUI3 klasyczny {Binding TwoWay} na TextBox.Text aktualizuje source dopiero
        // po LostFocus — przepychamy ręcznie na każde TextChanged.
        if (_vm is null || MarkdownEditor.Text == _vm.EditorText) return;
        _vm.EditorText = MarkdownEditor.Text;
    }

    // ───────────────────────── Pasek formatowania ─────────────────────────

    private void OnFormatClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string kind })
            ApplyFormat(kind);
    }

    /// <summary>
    /// Stosuje formatowanie czytając ŻYWĄ pozycję kursora/zaznaczenia prosto z TextBox,
    /// zamiast ufać stanowi VM (który bywa nadpisany zerem przez spóźnione SelectionChanged).
    /// </summary>
    private void ApplyFormat(string kind)
    {
        _vm.SelectionStart = MarkdownEditor.SelectionStart;
        _vm.SelectionLength = MarkdownEditor.SelectionLength;
        _vm.ApplyFormatCommand.Execute(kind);
    }

    // ───────────────────────── Edytor: zaznaczenie ─────────────────────────

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectionSync)
            return;
        _vm.SelectionStart = MarkdownEditor.SelectionStart;
        _vm.SelectionLength = MarkdownEditor.SelectionLength;
    }

    private void OnSelectionRequested(int start, int length)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Suppress od razu — ustawienie Text też resetuje kursor i odpala SelectionChanged.
            _suppressSelectionSync = true;

            if (MarkdownEditor.Text != _vm.EditorText)
                MarkdownEditor.Text = _vm.EditorText;

            int textLength = MarkdownEditor.Text?.Length ?? 0;
            int safeStart = Math.Clamp(start, 0, textLength);
            int safeLength = Math.Clamp(length, 0, textLength - safeStart);
            MarkdownEditor.Select(safeStart, safeLength);

            _suppressSelectionSync = false;

            // Ręcznie synchronizujemy — SelectionChanged był wytłumiony przez cały blok.
            _vm.SelectionStart = safeStart;
            _vm.SelectionLength = safeLength;

            // Wyszukiwanie: TextBox przewija do zaznaczenia i pokazuje podświetlenie tylko
            // gdy ma fokus.
            if (_vm.IsFindVisible && safeLength > 0)
            {
                if (_focusEditorAfterSelect)
                {
                    // Nawigacja (strzałki/F3/Enter) — focus edytora i tam zostajemy.
                    _focusEditorAfterSelect = false;
                    MarkdownEditor.Focus(FocusState.Programmatic);
                }
                else
                {
                    // Live przy pisaniu — focus edytora przewija/podświetla, potem oddajemy
                    // fokus polu szukania (przewinięcie zostaje), żeby można było dalej pisać.
                    MarkdownEditor.Focus(FocusState.Programmatic);
                    DispatcherQueue.TryEnqueue(() => FindBox.Focus(FocusState.Programmatic));
                }
            }
        });
    }

    private void OnToggleReplace(object sender, RoutedEventArgs e)
    {
        if (_vm.IsReplaceVisible)
            _vm.IsReplaceVisible = false;
        else
            _vm.ShowReplaceCommand.Execute(null);
    }

    /// <summary>Ustawiana przed nawigacją po trafieniach — każe sfocusować edytor po zaznaczeniu.</summary>
    private bool _focusEditorAfterSelect;

    private void OnFindNext(object sender, RoutedEventArgs e) => NavigateFind(true);
    private void OnFindPrevious(object sender, RoutedEventArgs e) => NavigateFind(false);

    private void NavigateFind(bool forward)
    {
        _focusEditorAfterSelect = true;
        if (forward)
            _vm.FindNextCommand.Execute(null);
        else
            _vm.FindPreviousCommand.Execute(null);
    }

    private void OnFindBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            NavigateFind(!IsShiftDown());
        }
    }

    private static bool IsShiftDown() =>
        Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnFocusFindRequested() => DispatcherQueue.TryEnqueue(() => FindBox.Focus(FocusState.Programmatic));

    // ───────────────────────── Podgląd (WebView2) ─────────────────────────

    private bool _previewInitialized;

    private async void OnPreviewHtmlChanged(string html)
    {
        var core = await EnsurePreviewCoreAsync();
        core?.NavigateToString(html ?? string.Empty);
    }

    private async Task<CoreWebView2?> EnsurePreviewCoreAsync()
    {
        try { await PreviewWeb.EnsureCoreWebView2Async(); }
        catch { return null; }

        var core = PreviewWeb.CoreWebView2;
        if (!_previewInitialized)
        {
            _previewInitialized = true;
            // WebView2 przechwytuje Ctrl+F, F3, Esc — wyłączamy skróty Edge'a i
            // wstrzykujemy bridge który postMessage-uje je do hosta.
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.WebMessageReceived += OnPreviewWebMessageReceived;
            core.NavigationCompleted += async (_, e) =>
            {
                if (e.IsSuccess)
                    await core.ExecuteScriptAsync(PreviewKeyBridgeScript);
            };
        }
        return core;
    }

    // Bridge: WebView2 keydown → postMessage → host (skróty klawiszowe podglądu)
    private void OnPreviewWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.TryGetWebMessageAsString())
            {
                case "ctrl+f":  OpenPreviewFind();                   break;
                case "f3":      _ = PreviewFindStepAsync(true);      break;
                case "shift+f3":_ = PreviewFindStepAsync(false);     break;
                case "escape":
                    if (PreviewFindBar.Visibility == Visibility.Visible)
                        ClosePreviewFind();
                    break;
            }
        });
    }

    private const string PreviewKeyBridgeScript = @"
(function(){
  if (window.__wmdKeyBridge) return;
  window.__wmdKeyBridge = true;
  window.addEventListener('keydown', function(e) {
    if (e.ctrlKey && e.key === 'f') {
      window.chrome.webview.postMessage('ctrl+f'); e.preventDefault();
    } else if (e.key === 'F3' && !e.shiftKey) {
      window.chrome.webview.postMessage('f3');      e.preventDefault();
    } else if (e.key === 'F3' && e.shiftKey) {
      window.chrome.webview.postMessage('shift+f3'); e.preventDefault();
    } else if (e.key === 'Escape') {
      window.chrome.webview.postMessage('escape');  e.preventDefault();
    }
  }, true);
})();
";

    // ───────────────────────── Implementacja IUiService ─────────────────────────

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = accept,
            CloseButtonText = cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = ok,
            XamlRoot = Root.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    public async Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue)
    {
        var input = new TextBox { Text = initialValue, SelectionStart = initialValue?.Length ?? 0 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = accept,
            CloseButtonText = cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    public async Task<string?> ChooseAsync(string title, string cancel, params string[] options)
    {
        var list = new ListView { ItemsSource = options, SelectionMode = ListViewSelectionMode.Single, SelectedIndex = 0 };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = list,
            PrimaryButtonText = "OK",
            CloseButtonText = cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is string s ? s : null;
    }

    public void Toast(string message)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (Root.XamlRoot is null)
                return;

            var border = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PrimaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                Child = new TextBlock { Text = message, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White) },
            };
            var popup = new Microsoft.UI.Xaml.Controls.Primitives.Popup
            {
                XamlRoot = Root.XamlRoot,
                Child = border,
            };
            border.Loaded += (_, _) =>
            {
                popup.HorizontalOffset = (Root.XamlRoot.Size.Width - border.ActualWidth) / 2;
                popup.VerticalOffset = Root.XamlRoot.Size.Height - border.ActualHeight - 56;
            };
            popup.IsOpen = true;
            await Task.Delay(1800);
            popup.IsOpen = false;
        });
    }

    public void ApplyTheme(bool dark)
    {
        if (Root is FrameworkElement fe)
            fe.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void GoToHelp()
    {
        var help = new HelpWindow(_vm.IsDarkTheme);
        help.Activate();
    }

    // ───────────────────────── O programie ─────────────────────────

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var brandColor = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PrimaryBrush"];

        var panel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, MinWidth = 280 };

        // Ikona aplikacji — PNG 512px dekodowany do 144px (ostry na ekranach hi-DPI), wyświetlany 72px
        try
        {
            var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "appicon.png");
            if (System.IO.File.Exists(iconPath))
            {
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage
                {
                    DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    DecodePixelWidth = 72,
                    DecodePixelHeight = 72,
                    UriSource = new Uri(iconPath),
                };
                panel.Children.Add(new Image
                {
                    Source = bmp,
                    Width = 72,
                    Height = 72,
                    Margin = new Thickness(0, 4, 0, 8),
                });
            }
        }
        catch { /* brak ikony — pomijamy */ }

        panel.Children.Add(new TextBlock
        {
            Text = "WinMD",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Markdown editor and reader for Windows",
            FontSize = 13,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Version 1.0.0",
            FontSize = 12,
            Opacity = 0.55,
            Margin = new Thickness(0, 2, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["FormatButtonBorderBrush"],
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        var dev = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, FontSize = 14 };
        dev.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Developer  ", Foreground = brandColor, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        dev.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Tomek Masłowski" });
        panel.Children.Add(dev);

        panel.Children.Add(new TextBlock
        {
            Text = "© 2026  ·  All rights reserved",
            FontSize = 12,
            Opacity = 0.55,
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var dialog = new ContentDialog
        {
            Content = panel,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot,
            RequestedTheme = _vm.IsDarkTheme ? ElementTheme.Dark : ElementTheme.Light,
        };
        await dialog.ShowAsync();
    }

    public Task ShareFileAsync(string path, string title)
    {
        // Na desktopie „udostępnij" = pokaż gotowy plik w Eksploratorze (zaznaczony), skąd użytkownik
        // może go wysłać/skopiować. Prostsze i pewniejsze niż UI udostępniania w aplikacji unpackaged.
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            Toast("Saved: " + System.IO.Path.GetFileName(path));
        }
        catch
        {
            Toast("Saved to " + path);
        }
        return Task.CompletedTask;
    }

    public ITimerLite CreateTimer(TimeSpan interval, Action onTick)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => onTick();
        return new TimerLite(timer);
    }

    private sealed class TimerLite(Microsoft.UI.Dispatching.DispatcherQueueTimer timer) : ITimerLite
    {
        public void Start() => timer.Start();
        public void Stop() => timer.Stop();
    }

    // ───────────────────────── Skróty klawiszowe ─────────────────────────

    private void SetupKeyboardShortcuts()
    {
        void Add(VirtualKey key, VirtualKeyModifiers mods, Action action)
        {
            var acc = new KeyboardAccelerator { Key = key, Modifiers = mods };
            acc.Invoked += (_, args) => { args.Handled = true; action(); };
            Root.KeyboardAccelerators.Add(acc);
        }

        const VirtualKeyModifiers ctrl = VirtualKeyModifiers.Control;
        const VirtualKeyModifiers shift = VirtualKeyModifiers.Shift;
        const VirtualKeyModifiers none = VirtualKeyModifiers.None;

        Add(VirtualKey.N, ctrl, () => _vm.NewCommand.Execute(null));
        Add(VirtualKey.O, ctrl, () => _vm.OpenCommand.Execute(null));
        Add(VirtualKey.S, ctrl, () => _vm.SaveCommand.Execute(null));
        Add(VirtualKey.P, ctrl, () => _vm.ExportPdfCommand.Execute(null));
        Add(VirtualKey.B, ctrl, () => { if (!_vm.IsPreviewMode) ApplyFormat("bold"); });
        Add(VirtualKey.I, ctrl, () => { if (!_vm.IsPreviewMode) ApplyFormat("italic"); });
        Add(VirtualKey.E, ctrl, () => { if (_vm.IsPreviewMode) _vm.GoToEditCommand.Execute(null); else _vm.GoToPreviewCommand.Execute(null); });
        Add(VirtualKey.F, ctrl, () => { if (_vm.IsPreviewMode) OpenPreviewFind(); else _vm.ShowFindCommand.Execute(null); });
        Add(VirtualKey.H, ctrl, () => { if (!_vm.IsPreviewMode) _vm.ShowReplaceCommand.Execute(null); });
        Add(VirtualKey.F3, none, () => FindStep(true));
        Add(VirtualKey.F3, shift, () => FindStep(false));
        Add(VirtualKey.Escape, none, OnEscape);
        Add(VirtualKey.F1, none, () => _vm.GoToHelpCommand.Execute(null));
    }

    private void FindStep(bool forward)
    {
        if (_vm.IsPreviewMode)
            _ = PreviewFindStepAsync(forward);
        else
            NavigateFind(forward);
    }

    private void OnEscape()
    {
        if (_vm.IsPreviewMode)
        {
            if (PreviewFindBar.Visibility == Visibility.Visible)
                ClosePreviewFind();
        }
        else if (_vm.IsFindVisible)
        {
            _vm.CloseFindCommand.Execute(null);
        }
    }

    private void OnFindButtonClick(object sender, RoutedEventArgs e)
    {
        if (_vm.IsPreviewMode)
            OpenPreviewFind();
        else
            _vm.ShowFindCommand.Execute(null);
    }

    // ───────────────────────── Find w podglądzie (JS na WebView2) ─────────────────────────

    private void OpenPreviewFind()
    {
        PreviewFindBar.Visibility = Visibility.Visible;
        PreviewFindBox.Focus(FocusState.Programmatic);
        _ = RunPreviewFindAsync();
    }

    private void ClosePreviewFind()
    {
        PreviewFindBar.Visibility = Visibility.Collapsed;
        PreviewFindBox.Text = string.Empty;
        PreviewFindCount.Text = string.Empty;
        _ = ClearPreviewFindAsync();
    }

    private async void OnPreviewFindTextChanged(object sender, TextChangedEventArgs e) => await RunPreviewFindAsync();
    private async void OnPreviewFindNext(object sender, RoutedEventArgs e) => await PreviewFindStepAsync(true);
    private async void OnPreviewFindPrevious(object sender, RoutedEventArgs e) => await PreviewFindStepAsync(false);
    private void OnPreviewCloseFind(object sender, RoutedEventArgs e) => ClosePreviewFind();

    private async Task RunPreviewFindAsync()
    {
        var core = await EnsurePreviewCoreAsync();
        if (core is null)
            return;
        var term = PreviewFindBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(term))
        {
            await core.ExecuteScriptAsync(PreviewFindScript + "window.__wmdFind.clear();");
            PreviewFindCount.Text = string.Empty;
            return;
        }
        var json = await core.ExecuteScriptAsync(
            PreviewFindScript + "window.__wmdFind.search(" + System.Text.Json.JsonSerializer.Serialize(term) + ");");
        UpdatePreviewCount(json);
    }

    private async Task PreviewFindStepAsync(bool forward)
    {
        var core = await EnsurePreviewCoreAsync();
        if (core is null)
            return;
        var json = await core.ExecuteScriptAsync(
            PreviewFindScript + "window.__wmdFind." + (forward ? "next" : "prev") + "();");
        UpdatePreviewCount(json);
    }

    private async Task ClearPreviewFindAsync()
    {
        var core = await EnsurePreviewCoreAsync();
        if (core is null)
            return;
        await core.ExecuteScriptAsync(PreviewFindScript + "window.__wmdFind.clear();");
    }

    private void UpdatePreviewCount(string? scriptResult)
    {
        if (string.IsNullOrEmpty(scriptResult) || scriptResult == "null")
        {
            PreviewFindCount.Text = "No results";
            return;
        }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(scriptResult);
            int count = doc.RootElement.GetProperty("count").GetInt32();
            int active = doc.RootElement.GetProperty("active").GetInt32();
            PreviewFindCount.Text = count > 0 ? $"{active + 1}/{count}" : "No results";
        }
        catch { PreviewFindCount.Text = string.Empty; }
    }

    private const string PreviewFindScript = @"
(function(){
  if (window.__wmdFind) return;
  var state = { marks: [], active: -1 };
  function injectStyle(){
    if (document.getElementById('wmd-find-style')) return;
    var s = document.createElement('style');
    s.id = 'wmd-find-style';
    s.textContent = 'mark.wmd-find{background:#ffd54f;color:#000;border-radius:2px;}mark.wmd-find.wmd-active{background:#ff9800;color:#000;}';
    (document.head || document.documentElement).appendChild(s);
  }
  function clear(){
    for (var i = 0; i < state.marks.length; i++){
      var m = state.marks[i]; var p = m.parentNode;
      if (p){ p.replaceChild(document.createTextNode(m.textContent), m); p.normalize(); }
    }
    state.marks = []; state.active = -1;
  }
  function setActive(i){
    if (state.marks.length === 0){ state.active = -1; return; }
    if (state.active >= 0 && state.marks[state.active]) state.marks[state.active].classList.remove('wmd-active');
    var n = state.marks.length; state.active = ((i % n) + n) % n;
    var m = state.marks[state.active]; m.classList.add('wmd-active');
    m.scrollIntoView({ block: 'center', inline: 'nearest' });
  }
  function search(term){
    clear();
    if (!term) return { count: 0, active: -1 };
    var needle = term.toLowerCase();
    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
      acceptNode: function(node){
        if (!node.nodeValue || !node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
        var t = node.parentNode ? node.parentNode.nodeName : '';
        if (t === 'SCRIPT' || t === 'STYLE' || t === 'MARK' || t === 'NOSCRIPT') return NodeFilter.FILTER_REJECT;
        return NodeFilter.FILTER_ACCEPT;
      }
    });
    var nodes = []; var nd;
    while (nd = walker.nextNode()) nodes.push(nd);
    for (var i = 0; i < nodes.length; i++){
      var node = nodes[i]; var text = node.nodeValue; var lower = text.toLowerCase();
      var idx = lower.indexOf(needle); if (idx < 0) continue;
      var frag = document.createDocumentFragment(); var last = 0;
      while (idx >= 0){
        if (idx > last) frag.appendChild(document.createTextNode(text.slice(last, idx)));
        var mk = document.createElement('mark'); mk.className = 'wmd-find';
        mk.textContent = text.slice(idx, idx + needle.length);
        frag.appendChild(mk); state.marks.push(mk);
        last = idx + needle.length; idx = lower.indexOf(needle, last);
      }
      if (last < text.length) frag.appendChild(document.createTextNode(text.slice(last)));
      node.parentNode.replaceChild(frag, node);
    }
    if (state.marks.length > 0) setActive(0);
    return { count: state.marks.length, active: state.active };
  }
  injectStyle();
  window.__wmdFind = {
    search: function(t){ return search(t); },
    next: function(){ setActive(state.active + 1); return { count: state.marks.length, active: state.active }; },
    prev: function(){ setActive(state.active - 1); return { count: state.marks.length, active: state.active }; },
    clear: function(){ clear(); return { count: 0, active: -1 }; }
  };
})();
";
}

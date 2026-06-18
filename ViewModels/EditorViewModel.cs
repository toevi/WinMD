using WinMD.Models;
using WinMD.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinMD.ViewModels;

/// <summary>
/// Shared logic for the editor and preview: document state, file operations,
/// edit history (undo/redo) and the Markdown formatting bar. UI-agnostic — all dialogs,
/// theme, toast, share and timer go through <see cref="IUiService"/>.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IMarkdownService _markdownService;
    private readonly IPdfExporter _pdfExporter;
    private readonly IUiService _ui;
    private readonly ISettings _settings;

    /// <summary>Content last written to disk — baseline for change detection.</summary>
    private string _savedContent = string.Empty;

    // --- Edit history (undo/redo) ---
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    /// <summary>Last text committed to history (equals EditorText while idle).</summary>
    private string _committedText = string.Empty;

    /// <summary>Baseline of the current typing burst, not yet committed to history.</summary>
    private string? _pendingBaseline;

    /// <summary>Prevents recording changes caused by undo/redo itself.</summary>
    private bool _applyingHistory;

    private ITimerLite? _coalesceTimer;

    /// <summary>
    /// Raised when the ViewModel deliberately wants to move the editor caret/selection
    /// (after formatting, loading a file or undo/redo). The view applies it directly to
    /// the control — the caret is otherwise tracked control→VM only (OneWayToSource).
    /// </summary>
    public event Action<int, int>? SelectionRequested;

    /// <summary>Raised when the Find window opens, so the view can focus the search field.</summary>
    public event Action? FocusFindRequested;

    /// <summary>Raised when the preview HTML changes, so the view can reload the WebView2.</summary>
    public event Action<string>? PreviewHtmlChanged;

    public EditorViewModel(IFileService fileService, IMarkdownService markdownService,
        IPdfExporter pdfExporter, IUiService ui, ISettings settings)
    {
        _fileService = fileService;
        _markdownService = markdownService;
        _pdfExporter = pdfExporter;
        _ui = ui;
        _settings = settings;
        _document = new MarkdownDocument();
        _isDarkTheme = _settings.GetBool(DarkThemeKey, false);
        UpdateTitle();
    }

    [ObservableProperty]
    private MarkdownDocument _document;

    [ObservableProperty]
    private string _editorText = string.Empty;

    /// <summary>Wyrenderowany HTML podglądu. Widok ładuje go do WebView2 (NavigateToString).</summary>
    [ObservableProperty]
    private string _previewHtml = string.Empty;

    [ObservableProperty]
    private string _title = "WinMD";

    [ObservableProperty]
    private int _characterCount;

    // Cursor position / selection in the editor (two-way bound to the Editor control).
    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionLength;

    /// <summary>Whether there are unsaved changes relative to the file on disk.</summary>
    public bool IsDirty => !string.Equals(EditorText, _savedContent, StringComparison.Ordinal);

    /// <summary>Bottom status bar text.</summary>
    public string StatusText => IsDirty
        ? "Unsaved changes"
        : Document.IsPersisted ? "Saved" : "New document";

    private bool HasPendingChange =>
        _pendingBaseline is not null && !string.Equals(_pendingBaseline, EditorText, StringComparison.Ordinal);

    public bool CanUndo => _undo.Count > 0 || HasPendingChange;

    public bool CanRedo => _redo.Count > 0;

    partial void OnEditorTextChanged(string value)
    {
        CharacterCount = value?.Length ?? 0;
        UpdateTitle();

        if (_applyingHistory)
            return;

        // Start of a new editing burst — remember the state before the change.
        _pendingBaseline ??= _committedText;

        // Debounce: consecutive keystrokes within a short window form one history step.
        CoalesceTimer.Stop();
        CoalesceTimer.Start();

        RefreshHistoryState();
        RefreshMatchesOnEdit();
    }

    private void UpdateTitle()
    {
        Title = (IsDirty ? "● " : string.Empty) + Document.FileName;
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>Renders the current Markdown into the preview HTML.</summary>
    public void RefreshPreview()
    {
        PreviewHtml = _markdownService.ToHtml(EditorText, IsDarkTheme);
        PreviewHtmlChanged?.Invoke(PreviewHtml);
    }

    // ---------------------------------------------------------------------
    // Theme (dark / light)
    // ---------------------------------------------------------------------

    private const string DarkThemeKey = "dark_theme";

    [ObservableProperty]
    private bool _isDarkTheme;

    /// <summary>Menu label reflecting the theme you switch *to*.</summary>
    public string ThemeMenuLabel => IsDarkTheme ? "Light theme" : "Dark theme";

    partial void OnIsDarkThemeChanged(bool value) => OnPropertyChanged(nameof(ThemeMenuLabel));

    /// <summary>Applies <paramref name="dark"/> as the app theme.</summary>
    public void ApplyTheme(bool dark) => _ui.ApplyTheme(dark);

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _settings.SetBool(DarkThemeKey, IsDarkTheme);
        ApplyTheme(IsDarkTheme);
        RefreshPreview(); // re-render the preview with the matching stylesheet
    }

    /// <summary>
    /// Asks the view to place the caret/selection at the given range and updates the
    /// VM's own selection state to match.
    /// </summary>
    private void RequestSelection(int start, int length)
    {
        SelectionStart = start;
        SelectionLength = length;
        SelectionRequested?.Invoke(start, length);
    }

    /// <summary>Resets the editor to a fresh, empty, unsaved document.</summary>
    public void ResetToNewDocument()
    {
        Document = new MarkdownDocument();
        EditorText = string.Empty;
        _savedContent = string.Empty;
        ResetHistory(string.Empty);
        RequestSelection(0, 0);
        UpdateTitle();
        RefreshPreview();
    }

    [RelayCommand]
    private void GoToHelp() => _ui.GoToHelp();

    /// <summary>Czy nakładka Podglądu przykrywa edytor.</summary>
    [ObservableProperty]
    private bool _isPreviewMode;

    /// <summary>Przełącza na Edycję (chowa nakładkę podglądu).</summary>
    [RelayCommand]
    private void GoToEdit()
    {
        IsPreviewMode = false;
    }

    /// <summary>Przełącza na Podgląd (pokazuje nakładkę podglądu nad edytorem).</summary>
    [RelayCommand]
    private void GoToPreview()
    {
        RefreshPreview();
        IsPreviewMode = true;
    }

    /// <summary>Renders the current Markdown to HTML and exports it as a PDF.</summary>
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        try
        {
            var html = _markdownService.ToHtml(EditorText);
            var jobName = System.IO.Path.GetFileNameWithoutExtension(Document.FileName);
            if (string.IsNullOrWhiteSpace(jobName))
                jobName = "document";

            var ok = await _pdfExporter.ExportAsync(html, jobName);
            if (!ok)
                await ShowErrorAsync("Could not export to PDF.", null);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not export to PDF.", ex);
        }
    }

    /// <summary>Renders the current document to HTML and exports it as a standalone .html file.</summary>
    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        try
        {
            var html = _markdownService.ToHtml(EditorText);
            var name = BaseFileName();
            var ok = await _fileService.ExportTextAsync(name + ".html", "text/html", html);
            if (ok)
                _ui.Toast("Exported HTML");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not export HTML.", ex);
        }
    }

    /// <summary>Shares the document — the user picks Markdown (.md) or PDF.</summary>
    [RelayCommand]
    private async Task ShareAsync()
    {
        try
        {
            var name = BaseFileName();

            string? choice = await _ui.ChooseAsync("Share as", "Cancel", "Markdown (.md)", "PDF");
            if (string.IsNullOrEmpty(choice) || choice == "Cancel")
                return;

            string? path;
            if (choice == "PDF")
            {
                var html = _markdownService.ToHtml(EditorText);
                var renderTask = _pdfExporter.RenderToFileAsync(html, name + ".pdf");
                // Guard: never let a stuck render hang the UI.
                if (await Task.WhenAny(renderTask, Task.Delay(20000)) != renderTask)
                {
                    await ShowErrorAsync("PDF export timed out.", null);
                    return;
                }

                path = await renderTask;
                if (path is null)
                {
                    await ShowErrorAsync("Could not create the PDF.", null);
                    return;
                }
            }
            else
            {
                path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name + ".md");
                await System.IO.File.WriteAllTextAsync(path, EditorText ?? string.Empty);
            }

            await _ui.ShareFileAsync(path, System.IO.Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not share.", ex);
        }
    }

    /// <summary>File name without extension, safe fallback "document".</summary>
    private string BaseFileName()
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(Document.FileName);
        return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }

    // ---------------------------------------------------------------------
    // Find / Find & Replace
    // ---------------------------------------------------------------------

    [ObservableProperty]
    private bool _isFindVisible;

    [ObservableProperty]
    private bool _isReplaceVisible;

    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWord;

    /// <summary>Start indices of all current matches in <see cref="EditorText"/>.</summary>
    private readonly List<int> _matches = new();

    /// <summary>Index into <see cref="_matches"/> of the currently highlighted match (-1 = none).</summary>
    private int _currentMatch = -1;

    /// <summary>Match counter text shown in the Find window ("2/5" / "No results" / "").</summary>
    public string MatchStatus =>
        string.IsNullOrEmpty(FindText) ? string.Empty
        : _matches.Count == 0 ? "No results"
        : $"{_currentMatch + 1}/{_matches.Count}";

    public bool HasMatches => _matches.Count > 0;

    partial void OnFindTextChanged(string value) => RecomputeMatches(jumpFromCaret: true);

    partial void OnMatchCaseChanged(bool value) => RecomputeMatches(jumpFromCaret: true);

    partial void OnWholeWordChanged(bool value) => RecomputeMatches(jumpFromCaret: true);

    /// <summary>
    /// Keeps the match list/counter in sync when the document is edited while the Find
    /// window is open — without moving the selection (so typing is not disturbed).
    /// </summary>
    private void RefreshMatchesOnEdit()
    {
        if (IsFindVisible)
            RecomputeMatches(jumpFromCaret: true, highlight: false);
    }

    [RelayCommand]
    private void ShowFind()
    {
        // The 🔍 button / Ctrl+F toggles the inline Find row: press again to hide it.
        if (IsFindVisible)
        {
            CloseFind();
            return;
        }

        SeedFindFromSelection();
        IsReplaceVisible = false;
        IsFindVisible = true;
        RecomputeMatches(jumpFromCaret: true);
        FocusFindRequested?.Invoke();
    }

    [RelayCommand]
    private void ShowReplace()
    {
        if (!IsFindVisible)
            SeedFindFromSelection();
        IsReplaceVisible = true;
        IsFindVisible = true;
        RecomputeMatches(jumpFromCaret: true);
        FocusFindRequested?.Invoke();
    }

    [RelayCommand]
    public void CloseFind()
    {
        IsFindVisible = false;
        IsReplaceVisible = false;
        _matches.Clear();
        _currentMatch = -1;
        OnPropertyChanged(nameof(MatchStatus));
        RefreshFindCommands();
    }

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void FindNext()
    {
        if (_matches.Count == 0)
            return;

        _currentMatch = (_currentMatch + 1) % _matches.Count;
        HighlightCurrentMatch();
    }

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void FindPrevious()
    {
        if (_matches.Count == 0)
            return;

        _currentMatch = (_currentMatch - 1 + _matches.Count) % _matches.Count;
        HighlightCurrentMatch();
    }

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void Replace()
    {
        if (_matches.Count == 0 || _currentMatch < 0)
            return;

        var text = EditorText ?? string.Empty;
        int start = _matches[_currentMatch];
        int len = FindText.Length;
        if (start + len > text.Length)
            return;

        EditorText = text[..start] + ReplaceText + text[(start + len)..];
        CommitPending(); // a replacement is its own single undo step

        // Recompute from the position right after the inserted replacement.
        RecomputeMatches(jumpFromCaret: false);
        SelectMatchAtOrAfter(start + ReplaceText.Length);
        RefreshPreview();
    }

    [RelayCommand(CanExecute = nameof(HasMatches))]
    private void ReplaceAll()
    {
        if (_matches.Count == 0)
            return;

        var text = EditorText ?? string.Empty;
        int len = FindText.Length;
        int count = _matches.Count;

        // Rebuild the text from the precomputed (ascending) match indices.
        var sb = new System.Text.StringBuilder(text.Length);
        int cursor = 0;
        foreach (int start in _matches)
        {
            sb.Append(text, cursor, start - cursor);
            sb.Append(ReplaceText);
            cursor = start + len;
        }
        sb.Append(text, cursor, text.Length - cursor);

        EditorText = sb.ToString();
        CommitPending();
        RecomputeMatches(jumpFromCaret: true);
        RefreshPreview();

        _ui.Toast($"Replaced {count}");
    }

    private void SeedFindFromSelection()
    {
        var text = EditorText ?? string.Empty;
        int start = Math.Clamp(SelectionStart, 0, text.Length);
        int len = Math.Clamp(SelectionLength, 0, text.Length - start);
        if (len > 0)
        {
            string selected = text.Substring(start, len);
            if (!selected.Contains('\n'))
                FindText = selected;
        }
    }

    /// <summary>Rescans the text for the current query and refreshes match state.</summary>
    private void RecomputeMatches(bool jumpFromCaret, bool highlight = true)
    {
        _matches.Clear();
        _currentMatch = -1;

        var text = EditorText ?? string.Empty;
        string needle = FindText ?? string.Empty;

        if (needle.Length > 0 && text.Length > 0)
        {
            var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int i = 0;
            while (i <= text.Length - needle.Length)
            {
                int idx = text.IndexOf(needle, i, comparison);
                if (idx < 0)
                    break;

                if (!WholeWord || IsWholeWordMatch(text, idx, needle.Length))
                    _matches.Add(idx);

                i = idx + 1; // allow overlapping starts; keeps it simple and correct
            }
        }

        if (_matches.Count > 0)
        {
            int anchor = jumpFromCaret ? SelectionStart : 0;
            _currentMatch = FirstMatchAtOrAfter(anchor);
            if (highlight)
            {
                HighlightCurrentMatch();
                return;
            }
        }

        OnPropertyChanged(nameof(MatchStatus));
        RefreshFindCommands();
    }

    private int FirstMatchAtOrAfter(int position)
    {
        for (int k = 0; k < _matches.Count; k++)
        {
            if (_matches[k] >= position)
                return k;
        }
        return 0; // wrap to the first match
    }

    private void SelectMatchAtOrAfter(int position)
    {
        if (_matches.Count == 0)
            return;
        _currentMatch = FirstMatchAtOrAfter(position);
        HighlightCurrentMatch();
    }

    private void HighlightCurrentMatch()
    {
        if (_currentMatch >= 0 && _currentMatch < _matches.Count)
        {
            RequestSelection(_matches[_currentMatch], FindText.Length);
        }

        OnPropertyChanged(nameof(MatchStatus));
        RefreshFindCommands();
    }

    private static bool IsWholeWordMatch(string text, int start, int length)
    {
        bool leftOk = start == 0 || !IsWordChar(text[start - 1]);
        int end = start + length;
        bool rightOk = end >= text.Length || !IsWordChar(text[end]);
        return leftOk && rightOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private void RefreshFindCommands()
    {
        OnPropertyChanged(nameof(HasMatches));
        FindNextCommand.NotifyCanExecuteChanged();
        FindPreviousCommand.NotifyCanExecuteChanged();
        ReplaceCommand.NotifyCanExecuteChanged();
        ReplaceAllCommand.NotifyCanExecuteChanged();
    }

    // ---------------------------------------------------------------------
    // Edit history (undo / redo)
    // ---------------------------------------------------------------------

    private ITimerLite CoalesceTimer =>
        _coalesceTimer ??= _ui.CreateTimer(TimeSpan.FromMilliseconds(600), CommitPending);

    /// <summary>Commits the current typing burst as a single history step.</summary>
    private void CommitPending()
    {
        _coalesceTimer?.Stop();

        if (_pendingBaseline is null)
            return;

        if (!string.Equals(_pendingBaseline, EditorText, StringComparison.Ordinal))
        {
            _undo.Push(_pendingBaseline);
            _redo.Clear();
        }

        _committedText = EditorText;
        _pendingBaseline = null;
        RefreshHistoryState();
    }

    /// <summary>Clears all history and sets a new baseline (e.g. after loading a file).</summary>
    private void ResetHistory(string text)
    {
        _coalesceTimer?.Stop();
        _undo.Clear();
        _redo.Clear();
        _committedText = text;
        _pendingBaseline = null;
        RefreshHistoryState();
    }

    private void ApplyHistory(string text)
    {
        _applyingHistory = true;
        EditorText = text;
        _committedText = text;
        _pendingBaseline = null;
        _coalesceTimer?.Stop();
        _applyingHistory = false;

        // Place the cursor at the end of the restored text.
        RequestSelection(text.Length, 0);

        RefreshHistoryState();
        RefreshMatchesOnEdit(); // keep the Find counter accurate after undo/redo
    }

    private void RefreshHistoryState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        CommitPending();

        if (_undo.Count == 0)
            return;

        _redo.Push(_committedText);
        ApplyHistory(_undo.Pop());
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        CommitPending();

        if (_redo.Count == 0)
            return;

        _undo.Push(_committedText);
        ApplyHistory(_redo.Pop());
    }

    // ---------------------------------------------------------------------
    // File operations
    // ---------------------------------------------------------------------

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        try
        {
            var doc = await _fileService.OpenAsync();
            if (doc is null)
                return;

            ApplyOpenedDocument(doc);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open the file.", ex);
        }
    }

    /// <summary>
    /// Wczytuje plik otwarty z zewnątrz (systemowe „Otwórz za pomocą" / dwuklik w Eksploratorze).
    /// Ścieżka pochodzi z argumentów wiersza poleceń (skojarzenie plików .md).
    /// </summary>
    public async Task OpenFromExternalAsync(string uri)
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        try
        {
            var doc = await _fileService.OpenFromUriAsync(uri);
            if (doc is null)
            {
                await ShowErrorAsync("Could not open the file.", null);
                return;
            }

            ApplyOpenedDocument(doc);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open the file.", ex);
        }
    }

    /// <summary>Przyjmuje świeżo wczytany dokument jako bieżący i resetuje stan edycji.</summary>
    private void ApplyOpenedDocument(MarkdownDocument doc)
    {
        Document = doc;
        EditorText = doc.Content;
        _savedContent = doc.Content;
        ResetHistory(doc.Content);
        RequestSelection(0, 0); // caret at the start of the freshly loaded file
        UpdateTitle();
        RefreshPreview();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            Document.Content = EditorText;

            bool ok;
            if (!Document.IsPersisted)
            {
                var created = await _fileService.CreateAsync(Document.FileName, EditorText);
                if (created is null)
                    return; // cancelled

                Document = created;
                ok = true;
            }
            else
            {
                ok = await _fileService.SaveAsync(Document);
                if (!ok)
                {
                    // Zapis w miejscu się nie powiódł (np. plik tylko do odczytu) — zaproponuj Save As.
                    bool saveAs = await _ui.ConfirmAsync(
                        "Read-only file",
                        "This file can't be saved in place. Save as a new file?",
                        "Save as", "Cancel");
                    if (!saveAs)
                        return;

                    var created = await _fileService.CreateAsync(Document.FileName, EditorText);
                    if (created is null)
                        return; // anulowano picker

                    Document = created;
                    ok = true;
                }
            }

            if (ok)
            {
                _savedContent = EditorText;
                UpdateTitle();
                _ui.Toast("Saved");
            }
            else
            {
                await ShowErrorAsync("Could not save the file.", null);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not save the file.", ex);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardIfDirtyAsync())
            return;

        ResetToNewDocument();
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (!Document.IsPersisted)
        {
            _ui.Toast("Save the file first.");
            return;
        }

        var newName = await _ui.PromptAsync(
            "Rename", "New file name:", "OK", "Cancel", Document.FileName);

        if (string.IsNullOrWhiteSpace(newName))
            return;

        try
        {
            var ok = await _fileService.RenameAsync(Document, newName.Trim());
            if (ok)
            {
                UpdateTitle();
                _ui.Toast("Renamed");
            }
            else
            {
                await ShowErrorAsync("Could not rename the file.", null);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not rename the file.", ex);
        }
    }

    // ---------------------------------------------------------------------
    // Markdown formatting bar
    // ---------------------------------------------------------------------

    [RelayCommand]
    private void ApplyFormat(string kind)
    {
        var text = EditorText ?? string.Empty;
        int start = Math.Clamp(SelectionStart, 0, text.Length);
        int len = Math.Clamp(SelectionLength, 0, text.Length - start);
        string selected = text.Substring(start, len);

        switch (kind)
        {
            case "bold":
                Wrap("**");
                break;
            case "italic":
                Wrap("*");
                break;
            case "code":
                Wrap("`");
                break;
            case "h1":
                LinePrefix("# ");
                break;
            case "h2":
                LinePrefix("## ");
                break;
            case "quote":
                LinePrefix("> ");
                break;
            case "ul":
                LinePrefix("- ");
                break;
            case "ol":
                LinePrefix("1. ");
                break;
            case "link":
                InsertLink();
                break;
            case "codeblock":
                InsertCodeBlock();
                break;
            case "pipe":
                InsertPipe();
                break;
            case "tabledash":
                InsertRaw("---");
                break;
            case "tablesep":
                InsertLineTemplate("| --- | --- |");
                break;
            case "table":
                InsertLineTemplate("| Column 1 | Column 2 |\n| --- | --- |\n| cell | cell |");
                break;
        }

        // Each formatting action is its own immediate history step.
        CommitPending();

        void Wrap(string marker)
        {
            string inner = selected.Length > 0 ? selected : "text";
            string replacement = marker + inner + marker;
            EditorText = text[..start] + replacement + text[(start + len)..];
            RequestSelection(start + marker.Length, inner.Length);
        }

        void LinePrefix(string prefix)
        {
            int lineStart = LineStartBefore(text, start);
            EditorText = text[..lineStart] + prefix + text[lineStart..];
            RequestSelection(start + prefix.Length, len);
        }

        void InsertLink()
        {
            string label = selected.Length > 0 ? selected : "text";
            string replacement = $"[{label}](url)";
            EditorText = text[..start] + replacement + text[(start + len)..];
            RequestSelection(start + replacement.Length - 4, 3);
        }

        void InsertCodeBlock()
        {
            string inner = selected.Length > 0 ? selected : string.Empty;
            // Blok kodu musi zaczynać się od nowej linii
            string pre  = (start > 0 && !IsLineBreak(text[start - 1])) ? "\n" : string.Empty;
            string body = "```\n" + inner + (inner.Length > 0 && !inner.EndsWith('\n') ? "\n" : string.Empty) + "```";
            EditorText = text[..start] + pre + body + text[(start + len)..];
            // Kursor wewnątrz bloku, za "```\n"
            RequestSelection(start + pre.Length + 4, inner.Length);
        }

        void InsertPipe()
        {
            // Otacza zaznaczenie: | zaznaczenie | albo wstawia | przy kursorze
            string inner = selected.Length > 0 ? $" {selected.Trim()} " : " ";
            string replacement = $"|{inner}|";
            EditorText = text[..start] + replacement + text[(start + len)..];
            RequestSelection(start + 1, inner.Length);
        }

        void InsertRaw(string raw)
        {
            EditorText = text[..start] + raw + text[(start + len)..];
            RequestSelection(start + raw.Length, 0);
        }

        void InsertLineTemplate(string template)
        {
            // Upewniamy się, że szablon zaczyna się od nowej linii
            string pre = (start > 0 && !IsLineBreak(text[start - 1])) ? "\n" : string.Empty;
            string block = pre + template;
            EditorText = text[..start] + block + text[(start + len)..];
            RequestSelection(start + block.Length, 0);
        }
    }

    /// <summary>
    /// Indeks początku linii zawierającej <paramref name="position"/>. WinUI3 TextBox używa
    /// '\r' jako separatora linii (Enter wstawia '\r', nie '\n'), więc szukamy obu znaków.
    /// </summary>
    private static int LineStartBefore(string text, int position)
    {
        for (int i = position - 1; i >= 0; i--)
        {
            if (IsLineBreak(text[i]))
                return i + 1;
        }
        return 0;
    }

    private static bool IsLineBreak(char c) => c == '\n' || c == '\r';

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<bool> ConfirmDiscardIfDirtyAsync()
    {
        if (!IsDirty)
            return true;

        return await _ui.ConfirmAsync(
            "Unsaved changes", "Discard unsaved changes?", "Discard", "Cancel");
    }

    private async Task ShowErrorAsync(string message, Exception? ex)
    {
        if (ex is not null)
            System.Diagnostics.Debug.WriteLine($"[WinMD] {message} :: {ex}");

        await _ui.AlertAsync("Error", message, "OK");
    }
}

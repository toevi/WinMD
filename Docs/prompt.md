# Jak dodać szukanie + podświetlanie w oknie Podglądu (wersja Windows)

> Dokument-instrukcja dla Claude'a pracującego nad **windowsową** wersją AndroidMD.
> Ta wersja jest prawie klonem wersji Android. Zadanie: przenieść funkcję
> **wyszukiwania z podświetlaniem trafień do okna Podglądu** (Preview). Pasek
> szukania w oknie edycji już istnieje — chodzi o ten sam mechanizm, ale działający
> nad **wyrenderowanym podglądem HTML**, nie nad tekstem źródłowym.

## Cel

W zakładce/oknie Podglądu użytkownik klika ikonę 🔍, pojawia się pasek szukania
(pole tekstowe + licznik „n/total" + przyciski ‹ › ✕). Wpisywanie tekstu na żywo
podświetla trafienia w wyrenderowanym dokumencie, ‹ › skaczą między trafieniami,
licznik pokazuje „aktywne/wszystkie" (albo „No results"), a ✕ zamyka pasek
i czyści podświetlenia.

**Ważne:** szukamy po tym, co użytkownik **widzi** w podglądzie (tekst wyrenderowany
z Markdown), a nie po surowym źródle `.md`. Dlatego nie da się tu użyć matchera
tekstowego z ViewModelu (jak w oknie edycji) — trzeba szukać wewnątrz kontrolki
przeglądarki.

## Jak to jest zrobione na Androidzie (źródło do skopiowania zachowania)

Pliki: `Views/PreviewPage.xaml` i `Views/PreviewPage.xaml.cs`.

XAML — pasek szukania (`x:Name="FindBar"`, domyślnie `IsVisible="False"`) z:
- `Entry x:Name="FindEntry"` (`TextChanged`, `Completed`),
- `Label x:Name="FindCount"` (licznik),
- przyciski `‹` (poprzednie), `›` (następne), `✕` (zamknij),
- w toolbarze `ToolbarItem Text="🔍"` z `Clicked="OnToggleFind"`.

Code-behind — logika niezależna od platformy:
- `OnToggleFind` — pokazuje/chowa pasek, fokus na pole, wywołuje `RunFind()`.
- `OnFindTextChanged` → `RunFind()` (szukanie na żywo).
- `OnFindNext` / `OnFindPrevious` — skok do następnego/poprzedniego.
- `OnCloseFind` / `CloseFindBar` — chowa pasek, czyści pole, licznik i podświetlenia.
- `RunFind` — czyści poprzednie trafienia i uruchamia wyszukiwanie dla zapytania.

Część **specyficzna dla Androida** (to trzeba zastąpić odpowiednikiem WebView2):
korzysta z natywnego `Android.Webkit.WebView`:
- `webView.FindAllAsync(query)` — znajdź wszystkie,
- `webView.FindNext(true/false)` — następne / poprzednie,
- `webView.ClearMatches()` — wyczyść,
- `webView.SetFindListener(...)` + `IFindListener.OnFindResultReceived(activeMatchOrdinal, numberOfMatches, isDoneCounting)` — z tego aktualizujemy licznik:
  `"{activeMatchOrdinal + 1}/{numberOfMatches}"` albo `"No results"`.

Podświetlanie i przewijanie do trafienia robi **sama natywna przeglądarka** —
my tylko wołamy te API.

## Co zrobić na Windows (WebView2)

Na Windows platformową kontrolką MAUI `WebView` jest **WebView2** (Chromium/Edge).
Pobierasz ją analogicznie do Androida:

```csharp
#if WINDOWS
private Microsoft.Web.WebView2.Core.CoreWebView2? GetCore() =>
    (PreviewWeb.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.WebView2)?.CoreWebView2;
#endif
```

WebView2 **nie ma** `FindAllAsync` jak Android. Są dwie drogi — wybierz pierwszą,
jeśli wersja SDK na to pozwala:

### Opcja A (zalecana) — natywne Find API WebView2

Nowsze WebView2 SDK (Microsoft.Web.WebView2 ≥ 1.0.2792) ma wbudowane Find,
które jest niemal 1:1 odpowiednikiem Androida:

- `core.Find.StartAsync(options)` — start wyszukiwania (z `CoreWebView2FindOptions`:
  `FindTerm`, `IsCaseSensitive`, `ShouldHighlightAllMatches`),
- `core.Find.FindNextAsync()` / `core.Find.FindPreviousAsync()`,
- `core.Find.StopAsync()` — czyści podświetlenia,
- właściwości `core.Find.MatchCount`, `core.Find.ActiveMatchIndex`,
- zdarzenia `MatchCountChanged`, `ActiveMatchIndexChanged` — z nich aktualizuj
  licznik (dokładnie jak `OnFindResultReceived` na Androidzie). Pamiętaj, że
  `ActiveMatchIndex` bywa 0-based — licznik to `ActiveMatchIndex + 1`/`MatchCount`,
  a `MatchCount == 0` → „No results".

Podświetlanie i scroll-do-trafienia robi WebView2 samo — tak jak natywny WebView
na Androidzie. To najczystsza droga.

### Opcja B (fallback) — wstrzyknięcie JavaScriptu

Jeśli SDK jest starsze i nie ma `core.Find`, użyj `core.ExecuteScriptAsync(js)`:
- skrypt przechodzi po tekście, owija trafienia w `<mark>` (lub używa
  `window.find()`), zlicza je i zwraca liczbę oraz indeks aktywnego,
- ‹ › zmieniają aktywny `<mark>` (np. innym kolorem) i robią
  `scrollIntoView({block:'center'})`,
- czyszczenie = usunięcie wszystkich `<mark>`.
Wynik (liczba/indeks) zwraca `ExecuteScriptAsync` jako JSON — z tego aktualizujesz
licznik. Mniej elegancko niż A, ale działa wszędzie.

## Wskazówki implementacyjne

- **Logikę paska (XAML + handlery OnToggle/OnFind*/Close) skopiuj z Androida bez
  zmian** — różni się tylko warstwa `#if WINDOWS` wołająca WebView2 zamiast
  `Android.Webkit.WebView`. Trzymaj się tego samego podziału co `PreviewPage.xaml.cs`
  (wspólne handlery + region `#if`).
- Pasek szukania w Podglądzie wyglądem i zachowaniem ma być **identyczny** jak ten
  w oknie edycji (to samo rozmieszczenie pól i przycisków, ten sam licznik).
- Wyczyść podświetlenia przy zamknięciu paska **i** przy opuszczeniu okna Podglądu
  (na Androidzie robi to `OnDisappearing` → `CloseFindBar`). Na Windows zrób
  analogicznie (np. przy zmianie widoku/utracie fokusu).
- WebView2 inicjalizuje się asynchronicznie — `CoreWebView2` może być `null` zanim
  `EnsureCoreWebView2Async()` się skończy. Zabezpiecz `GetCore()` na `null`
  (jak Android zabezpiecza `GetWebView()`).
- Skróty klawiszowe: na Windows wypada podpiąć **Ctrl+F** = otwórz szukanie,
  **F3 / Shift+F3** = następne/poprzednie, **Esc** = zamknij (na Androidzie robi to
  `MainActivity.DispatchKeyEvent`; na Windows użyj odpowiednika obsługi klawiatury).

## Definicja ukończenia

- 🔍 w Podglądzie otwiera pasek szukania identyczny jak w edycji.
- Pisanie na żywo podświetla trafienia w wyrenderowanym dokumencie.
- ‹ › skaczą między trafieniami i przewijają do aktywnego.
- Licznik pokazuje „aktywne/wszystkie" lub „No results".
- ✕ oraz wyjście z Podglądu czyszczą podświetlenia.

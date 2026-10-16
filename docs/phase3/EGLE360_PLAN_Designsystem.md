# EGLE360 PLAN – Designsystem (Phase 3)

Stand: 16.10.2026

## Quelle der Wahrheit

Alle Farben, Schriftgrößen, Abstände und Radien stehen in `design/tokens.json`. Die Werte entsprechen exakt den Tabellen aus Abschnitt 28 des Master-Prompts. Daraus erzeugt `tools/generate_theme.py`:

- `src/Egle360.Desktop/Themes/Brand.xaml`: Logo-, Marken- und Navigationsfarben, Typografie, Abstände, Radien
- `src/Egle360.Desktop/Themes/Colors.Light.xaml` und `Colors.Dark.xaml`: Farben je Modus mit identischen Schlüsseln
- `design/tokens.css`: dieselben Werte als CSS-Variablen für den späteren Web-Client und den 3D-Viewer

Die Tests in `tests/Egle360.Design.Tests` prüfen bei jedem Build, dass `tokens.json` mit den Tabellen des Master-Prompts übereinstimmt und die erzeugten Dateien dazu passen. Die CI bricht ab, wenn jemand eine erzeugte Datei von Hand ändert.

## Ergänzte Werte

Der Master-Prompt legt diese Fälle nicht fest. Es wurden keine neuen Farben erfunden, sondern vorhandene wiederverwendet:

| Schlüssel | Wert | Begründung |
|---|---|---|
| Light Surface2 | #F3F5F7 | Für den Light Mode ist nur eine Flächenfarbe vorgegeben; verwendet wird die Hintergrundfarbe. |
| NavText | #FFFFFF | Die Navigation ist in beiden Modi dunkel; Weiß ist die Logofarbe des Schriftzugs. |
| NavTextSecondary | #9EACBA | Nebentext des Dark Mode, weil die Navigation dunkel ist. |
| OnAccent Light | #FFFFFF | Schrift auf Akzentflächen, Kontrast 4,56:1. |
| OnAccent Dark | #0B121A | Weiß auf #3395E8 erreicht nur 3,18:1; die Arbeitsflächenfarbe erreicht 5,92:1. |

## Kontrastregeln (WCAG 2.1 AA)

Die Werte sind automatisch getestet (`ContrastTests`).

- Text und Nebentext erfüllen auf allen Flächen beider Modi mindestens 4,5:1. Der Nebentext im Light Mode auf dem Hintergrund liegt mit 4,50:1 genau an der Grenze.
- Akzentfarbe als Text: im Dark Mode überall erlaubt, im Light Mode nur auf weißen Flächen (4,56:1). Auf dem Light-Hintergrund #F3F5F7 erreicht sie nur 4,17:1.
- Statusfarben im Light Mode (Erfolg 3,45:1, Warnung 2,19:1, Kritisch 4,18:1 auf Weiß) sind nicht als Textfarbe erlaubt. Sie kennzeichnen Status als Punkt, Symbol oder Balken; die Beschriftung steht in Textfarbe. Der Statuspunkt erhält einen feinen Rand in Textfarbe, damit auch Warnung (Gelb) auf Weiß erkennbar bleibt.
- Navigation: Auf der ausgewählten Fläche #135E9F wird nur Weiß verwendet (6,71:1). Der Nebentext #9EACBA würde dort nur 2,90:1 erreichen.

## Logo

- `EGLE360_Master_Light_Flat` hat einen transparenten Hintergrund und einen weißen Schriftzug. Es steht in der Navigation, die in beiden Modi dunkel ist.
- `EGLE360_Horizontal_Light` hat einen weißen, nicht transparenten Hintergrund und wird nur auf weißen Flächen gezeigt.
- Beide Dateien werden unverändert aus `brand/` eingebunden. Sie wurden nicht freigestellt, umgefärbt oder skaliert gespeichert.
- Hinweis: Die Master-Datei ist 1498 × 1050 Pixel groß. In der Navigation (56 Pixel Höhe) ist das ausreichend. Für große Darstellungen, etwa einen Startbildschirm in 4K, wäre eine höher aufgelöste oder Vektorfassung besser.

## Navigation

- Breite 232 Pixel, eingeklappt 56 Pixel; Umschalten mit F4 (wie die Navigationsleiste der Referenz) oder der Schaltfläche unten. Der Zustand wird gespeichert.
- Zustände: normal (#172532), Maus darüber (#1D3040), ausgewählt (#135E9F mit weißer Markierung links), Tastaturfokus (weißer Rahmen).
- Hauptbereich oben, Einstellungen unten. Ein Modul erscheint erst in der Navigation, wenn es funktioniert. In dieser Phase sind das „Designsystem“ und „Einstellungen“.

## Farbmodus

- Auswahl „Wie Windows“, „Hell“ oder „Dunkel“ unter Einstellungen. Gespeichert in `%APPDATA%\EGLE360\PLAN\ui-settings.json`.
- „Wie Windows“ liest die Windows-Einstellung für Apps und folgt Änderungen während der Laufzeit.
- Umschaltung ohne Neustart: Alle Oberflächenfarben sind als `DynamicResource` gebunden.

## Typografie und Raster

- Schrift: Segoe UI Variable bzw. Segoe UI (Windows-Systemschrift, keine zusätzliche Lizenz nötig), Monospace: Cascadia Mono bzw. Consolas.
- Größen: 11 Beschriftung, 13 Fließtext, 15 Untertitel, 20 Titel, 28 Display.
- Abstände im 4-Pixel-Raster: 4, 8, 12, 16, 24, 32. Radien: 4, 6, 8.

## Prüfung

- `dotnet test`: Farbwerte gegen Master-Prompt, erzeugte Dateien gegen `tokens.json`, Kontraste, Einstellungen, Navigation.
- CI auf Windows: Build mit Warnungen als Fehler, Tests, dann startet die Anwendung mit `--screenshots` und rendert jede Seite in beiden Modi als PNG. Die Bilder liegen als Artefakt am CI-Lauf.

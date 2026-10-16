# Abgleich Funktionsmatrix mit installiertem Bluebeam Revu 21

Stand: 16.10.2026. Abgleich der Funktionsmatrix (`EGLE360_PLAN_Funktionsmatrix.csv`, 272 Einträge) mit der lokal installierten Bluebeam-Revu-Version auf Elias' Rechner.

Die Matrix selbst wurde nicht verändert; sie gehört dem Thread "EGLE360 PLAN Phase 1 und 2". Diese Datei listet nur Abweichungen und Vorschläge zur Übernahme.

## Quellen und Vorgehen

Gelesen wurden nur Daten-, Konfigurations- und Vorlagendateien sowie Versionsangaben. Es wurden keine Programmdateien dekompiliert oder analysiert, nichts verändert und kein Code, keine Grafiken und keine Werkzeuginhalte übernommen.

| Ort | Gelesen |
|---|---|
| `C:\Program Files (x86)\Bluebeam Software\Bluebeam Revu\21\Revu` | Versionsressource von `Revu32.exe`, `*.config`, `VisualElementsManifest.xml`, Dateinamen |
| `C:\ProgramData\Bluebeam Software\Bluebeam Revu\21` | `FeatureFlags.xml`, Druckertreiber-JobOptions, Plug-in-Einstellungen, Dokumentvorlagen, Stempel, Wörterbücher |
| `%APPDATA%\Bluebeam Software\Revu\21` | `KeyboardShortcuts.xml`, Profile (`*.bpx`), Werkzeugsätze (`*.btx`), Linienstile (`*.blx`), Schraffuren (`*.bhx`), Lesezeichen- und Gewerkvorlagen, Tutorial-Beschreibungen (`Workflows/*/workflow.json`) |

Wichtig: Die Installation enthält keine lokale Hilfe (keine CHM-, HTML- oder PDF-Handbücher). Die Hilfe ist bei Revu 21 online. Deshalb lassen sich mehrere ungeklärte Einträge aus den lokalen Dateien nicht abschließend klären.

## Installierte Version

| Angabe | Wert | Beleg |
|---|---|---|
| Produkt | Bluebeam Revu 21 | Ordnerstruktur, Tutorial-Texte ("Revu 21") |
| Dateiversion | 21.11.0.23287 | Versionsressource von `Revu32.exe`, `Bluebeam.Revu.dll`, `Stapler32.exe`, `Bluebeam MCP Server.exe` |
| Lizenzmodell | Subscription | Tutorial "Bluebeam Cloud": "In Ihrer Subscription enthaltene Web- und Mobillösungen" |
| Paket (Basics/Core/Complete) | nicht ermittelbar | Bei Revu 21 hängt der Funktionsumfang an der Anmeldung. Die Lizenzdatei `Features\Safe.enx` wurde bewusst nicht ausgewertet. Die Kachel-Grafik "extreme" im Startmenü-Manifest ist kein belastbarer Paketnachweis. |
| Sprache | Deutsch | Vorlagen, Stempel und Werkzeugsätze in `de` |
| Laufzeit | .NET Framework 4.8 (32 Bit), zusätzlich `net8`-Ordner, WebView2 | `Revu32.exe.config`, Ordnerstruktur |

## Abweichungen bei Tastaturkürzeln

Grundlage ist die Standard-Kürzelliste `KeyboardShortcuts.xml` (255 Zuordnungen; Dateidatum entspricht den übrigen mitgelieferten Dateien, also vermutlich nicht angepasst). Alle übrigen Kürzel der Matrix stimmen mit der installierten Version überein.

| ID | Matrix | Installiert (21.11) | Vorschlag |
|---|---|---|---|
| PDF-DOC-004 Alle speichern | Strg+F2 | Umschalt+F2 (Strg+F2 ist "Markups exportieren") | Kürzel auf Umschalt+F2 korrigieren |
| MKP-EDIT-009 Reihenfolge | Strg+J / Strg+Umschalt+J / Strg+[ / Strg+Umschalt+[ | Nach vorne Strg+], Ganz nach vorne Strg+Umschalt+], Nach hinten Strg+[, Ganz nach hinten Strg+Umschalt+[. Strg+Umschalt+J ist "Globale Skripte", Strg+J ist nicht belegt | Kürzel auf Strg+] / Strg+Umschalt+] / Strg+[ / Strg+Umschalt+[ korrigieren |
| MKP-EDIT-016 Objektfang | Strg+Umschalt+F8 / F7 / F9 | Inhaltsfang Strg+Umschalt+F8, Markup-Fang Strg+Umschalt+F7, Rasterfang Strg+Umschalt+F9. F7 allein ist Rechtschreibprüfung, F9 allein Menüleiste | Schreibweise ausschreiben, damit F7/F9 nicht als eigene Kürzel gelesen werden |
| MKP-EDIT-019 Versatz (Offset) | O | Kein Standardkürzel O (Umschalt+O ist Lasso) | Kürzel entfernen oder als "nicht belegt" kennzeichnen |
| MKP-TOOL-027 Medienerfassung | Strg+Alt+I | Strg+Alt+I heißt intern "Bild von Kamera" | Passt; Bezeichnung ggf. präzisieren |
| COL-002 Projekte | Alt+K | Alt+K ist belegt (intern "ShowBfxTab"); dass dies das Studio-Projekte-Panel ist, ist eine Vermutung | Offen lassen |

## In Revu 21.11 vorhanden, in der Matrix nicht erfasst

Diese Funktionen sind in Kürzelliste, Profilen, Vorlagen oder Einstellungsdateien der installierten Version belegt, haben aber keinen eigenen Matrix-Eintrag. Ob sie aufgenommen werden, ist eine fachliche Entscheidung.

| Funktion | Beleg | Möglicher Bereich |
|---|---|---|
| Magic Fill (Messung) | Kürzel Umschalt+Alt+M (`Measure.MagicFill`) | QTO, neben QTO-016 |
| Markups neu aufbauen | Strg+Alt+A (`Markup.Rebuild`) | MKP-EDIT |
| Zuletzt geschlossene Dateien wieder öffnen | Strg+Alt+O | PDF-VIEW |
| Nächste/vorige Datei im selben Ordner öffnen | Alt+Bild ab / Alt+Bild auf | PDF-VIEW |
| Web-Registerkarte im Programm | Strg+T (`View.WebTabNew`) | PDF-VIEW |
| Zoomfenster/Lupe ein-aus | Umschalt+Z (`Toggle.Zoom`) | PDF-VIEW-005 |
| Ansichten umschalten, Fenster ausgleichen | Strg+1 (`View.Switch`), Umschalt+F12 (`View.Balance`) | PDF-VIEW-002 |
| Navigationsleiste und Panels ein-aus | F4, Umschalt+F4 | PDF-VIEW-012 |
| Datei-Manager-Panel | Alt+A, Dock-Layout in den Profilen | PDF-VIEW-012 |
| Flaggen-Panel | Dock-Tab "Flags" in den Profilen | MKP-TOOL-026 |
| Vorheriges/nächstes Markup ansteuern | Tab / Umschalt+Tab | MKP-LIST |
| Startseite | F12 | PDF-VIEW |
| Gleiche Größe, Breite, Höhe; auf Seite zentrieren | Profil-Befehle `Align.Size/Width/Height/CenterDocument` | MKP-EDIT-007 |
| Kontrollpunkte hinzufügen, entfernen, umwandeln | Profil-Werkzeugleiste "ControlPoint" | MKP-TOOL-010 |
| Linienstärken ausblenden | Profil-Befehl `View.DisableLineWeights` | PDF-VIEW |
| Inhalt ausschneiden, Inhalt radieren | Profil-Befehle `Edit.CutContent`, `Edit.EraseContent` | PDF-DOC-024 |
| Maßstabsgetreue Skizze für Rechteck, Ellipse, Polygon, Polylinie | Profil-Befehle `*SketchToScale` | MKP-TOOL-035 (bestätigt, Umfang genauer) |
| 360-Grad-Fotos | Tutorial "Markup-Tools": "360°-Fotos hinzufügen"; Bibliothek `Bluebeam.Common.SphericalPhotos` (nur Dateiname) | MKP-TOOL-027 erweitern |
| Profil-Skripte (z. B. "Vereinfachen und per E-Mail versenden") | Record `Script` in allen Profilen | AUTO / AI-004 |
| Dokumentvorlagen (Fax, Baustellenbericht, Rechnung, Memo, Mängelliste, RFI, Submittal, teils als Formular) | `ProgramData\...\Revu\Templates\de` | PDF-FORM |
| 13 deutsche Standardstempel | `Stamps\de` | MKP-TOOL-023 |
| Wörterbücher in 15 Sprachen, inkl. de_DE | `Revu\Dictionaries` | MKP-TOOL-037 |
| Gewerk-/Disziplin-Kategorien mit Kürzeln | `Categories\Construction.xml` | PLAN-002 |
| Lesezeichen-Vorlage "Baudokumente" | Profil-Record `BookmarkTemplates` | PDF-DOC-019 |
| Dateieigenschaften-Zuordnung aus CAD | `Pushbutton PDF\Samples\FileProperties\Sample.xml` | PDF-DOC-022, PDF-OCR-005 |
| PDF-Erstellung aus Outlook, Word, PowerPoint, Excel, AutoCAD, Revit, SolidWorks; Druckertreiber-Profile für Inventor, MicroStation, NX, Pro/ENGINEER, DWF | `Brewery\V45\Settings` | API-004, PDF-OCR-004/005 |
| Microsoft-Purview-Vertraulichkeitsbezeichnungen | nur Dateinamen `Bluebeam.MIP.dll`, `Microsoft.InformationProtection.dll` (Vermutung) | PDF-SEC |
| GoCanvas-Anbindung | Feature-Flag `RevuGoCanvasIntegration` (aktiv) | COL |
| Studio: Papierkorb, Revisionsverlauf, Bedrohungserkennung | Feature-Flags `RevuStudioTrashCan`, `RevuStudioRevisionHistoryWpf`, `RevuStudioThreatDetection` (aktiv) | COL |
| Länderspezifische "Smart Search" (u. a. Deutschland) | Feature-Flags `Revu*SmartSearch` (aktiv); Funktion aus den Dateien nicht erkennbar | offen |

## Bestätigungen für Max-Einträge

| ID | Befund |
|---|---|
| AI-001 MCP-Schnittstelle | In 21.11 ist ein `Bluebeam MCP Server.exe` mitinstalliert (gleiche Version 21.11.0.23287). Ob er im gebuchten Paket freigeschaltet ist, ist aus den Dateien nicht erkennbar. |
| CMP-004 Automatische Ausrichtung | Bibliothek `Bluebeam.Autoalignment` vorhanden (nur Dateiname, Vermutung). |
| PDF-OCR-006 3D-PDF aus IFC/U3D | Bibliotheken `Bluebeam.IFC`, `Bluebeam.IFC.U3D` und `Bluebeam.3D.Converter.exe` vorhanden (nur Dateinamen). |

## Die 14 ungeklärten Einträge

| ID | Funktion | Ergebnis | Begründung |
|---|---|---|---|
| PDF-VIEW-013 | Profile / Arbeitsbereiche | **Geklärt: vorhanden** | Profile sind XML-Dateien (`RevuProfile`, `*.bpx`) mit Spalten der Markup-Liste, Werkzeugleisten, Panel-Anordnung, eingebundenen Werkzeugsätzen, Status-Modellen, Lesezeichen-Vorlagen und Skripten. Mitgeliefert: Revu, Revu Advanced, Quantity Takeoff, Essentials, Vollbild. Der Pfad ist in den Benutzereinstellungen hinterlegt. Import/Export auf Dateibasis ist daraus ableitbar. |
| TOOL-003 | Import/Export Werkzeugsets | **Technisch geklärt, rechtlich offen** | BTX ist XML (`BluebeamRevuToolSet`). Titel, Ressourcen und Werkzeugdaten sind zlib-komprimiert und hex-kodiert; das Feld `Raw` enthält ein PDF-Annotationswörterbuch. Linienstile (`*.blx`) und Schraffuren (`*.bhx`) sind ähnlich aufgebaut. Ein Import ist technisch machbar, ohne Programmdateien zu analysieren. Ob er rechtlich zulässig ist, muss weiterhin geprüft werden. |
| MKP-EDIT-005 | Duplizieren | **Teilweise geklärt** | Es gibt keinen eigenen Befehl "Duplizieren" in der Kürzelliste. Vorhanden ist "Vervielfältigen" (`Edit.Multiply`, Strg+M). Strg+D ist Dokumenteigenschaften. |
| MKP-EDIT-023 | Magic Wand | **Teilweise geklärt** | "Magic Wand" kommt in den lokalen Dateien nicht vor. Vorhanden ist "Magic Fill" (Umschalt+Alt+M). Ob beides dasselbe meint, ist nicht belegt. |
| CMP-003 | Ausrichtung beim Vergleich | **Teilweise geklärt** | Bibliothek `Bluebeam.Autoalignment` vorhanden (nur Dateiname). Manuelle Ausrichtung über Referenzpunkte ist lokal nicht beschrieben. |
| PDF-FORM-003 | Formulardaten import/export | **Teilweise geklärt** | Ein eigenes Symbol für FDF-Dateien (`RevuFDF.ico`) deutet auf FDF-Unterstützung hin (Vermutung). XFDF, XML und CSV sind lokal nicht belegt. |
| PDF-VIEW-009 | Mehrmonitorbetrieb | **Offen** | Panels sind andockbar (Links, Rechts, Unten). Lösbare Fenster auf weiteren Monitoren sind in den Profilen nicht belegt. |
| PDF-VIEW-003 | Synchronisierte Ansicht | **Offen** | Geteilte Ansicht ist belegt (Strg+2, Strg+H, Strg+I), eine Synchronisierung nicht. |
| PDF-DOC-036 | PDF/A | **Offen** | In den Druckertreiber-Profilen keine PDF/A-Einstellung (Standard: PDF 1.4). Weitere Belege fehlen lokal. |
| PDF-SEC-005 | Stapel-Schwärzung | **Offen** | Nur Einzelschwärzung belegt (Umschalt+R, Umschalt+K, Umschalt+A). |
| MKP-EDIT-018 | CAD-Konstruktionshilfen | **Offen** | Belegt sind nur Fangfunktionen und Kontrollpunkt-Bearbeitung, keine Parallel-/Senkrecht-Hilfen. |
| MKP-LIST-007 | Mehrfachbearbeitung | **Offen** | Lokal nicht belegt. |
| TOOL-004 | Favoriten und Suche | **Offen** | Feature-Flag "neuer Werkzeugkasten" ist aktiv; Suche und Favoriten sind lokal nicht belegt. |
| AUTO-002 | Stapelnummerierung | **Offen** | Kopf- und Fußzeilen sind belegt (`Document.HeadersAndFooters`), Bates-Nummerierung nicht. |

Für die offenen Einträge wäre der nächste Schritt ein kurzer Test in der laufenden Anwendung oder die Online-Hilfe zu Revu 21.

## Hinweis zu Werkzeugsätzen

Mitgeliefert sind 12 Werkzeugsätze (u. a. Beton, Elektrik, Mechanik, Brandschutz und Sicherheit, Raumabschlüsse, Prüfung durch Architekten/Ingenieur/Auftragnehmer). Sie enthalten vor allem Zähl-, Flächen- und Längenmessungen sowie Textmarkups. Das stützt TOOL-001 und TOOL-006; für TOOL-005 (gewerkspezifische Vorlagen mit LV-Bezug) bleibt EGLE360 eine Erweiterung, da die Bluebeam-Sätze keinen LV-Bezug haben. Die Lineal-Einheit steht in den Benutzereinstellungen auf Zoll.

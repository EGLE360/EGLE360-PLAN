# EGLE360 PLAN – Technische Architektur (Phase 2)

Stand: 16.10.2026 · Status: Entwurf zur Freigabe

Dieses Dokument legt die technische Grundlage für EGLE360 PLAN fest. Jede Entscheidung ist als kurzer Architekturentscheid (ADR) mit Begründung zu Lizenzierung, Sicherheit, Leistung und Wartbarkeit festgehalten. Die Funktionsanforderungen stehen in der Funktionsmatrix aus Phase 1 (`phase1/EGLE360_PLAN_Funktionsmatrix.xlsx`), auf deren IDs hier verwiesen wird.

## 1. Ausgangslage und Leitlinien

Aus der Referenzanalyse ergeben sich einige Punkte, die die Architektur prägen:

- Der Kern von Bluebeam ist ein Windows-Desktop-Programm mit PDF-Annotationen als Datenträger. Markups, Messungen und Mengen hängen an der PDF-Datei. EGLE360 PLAN übernimmt das Prinzip, dass jedes Markup als standardkonforme PDF-Annotation gespeichert wird, ergänzt es aber um eine Projektdatenbank. Damit bleibt die PDF-Datei in anderen Programmen lesbar, und Mengen, Status und Verknüpfungen sind trotzdem auswertbar.
- Bluebeam hat keinen eigenen IFC-Viewer im engeren Sinn (nur 3D-PDF). BIM, Kollisionsprüfung, 4D, 5D und BCF sind EGLE360-Erweiterungen und werden von Grund auf geplant.
- Die KI-Funktionen von Bluebeam Max (Smart Review, Smart Overlay) laufen in der Cloud, sind im Preview-Status und laut offizieller FAQ nur für imperiale Maße ausgelegt. Für Österreich und Deutschland sind metrische Pläne und ÖNORM-/DIN-Konventionen Pflicht. Das ist ein konkreter Ansatzpunkt für EGLE360.
- Bluebeam Max bindet KI über MCP an (Claude als Erstintegration). EGLE360 PLAN bietet ebenfalls einen MCP-Server an, damit ein Assistent die Software kontrolliert bedienen kann.

Leitlinien:

1. Genauigkeit vor Funktionsbreite. Mess- und Mengenberechnungen laufen in genau einer, getesteten Rechenbibliothek, die Desktop und Server gemeinsam nutzen.
2. Offene Standards als Datenbasis: PDF (ISO 32000), PDF/A, IFC (ISO 16739), BCF, ÖNORM A 2063 und GAEB DA XML für den LV-Austausch.
3. Keine Bibliothek mit starkem Copyleft (GPL, AGPL) im ausgelieferten Produkt, wenn EGLE360 PLAN proprietär vertrieben wird. LGPL nur bei dynamischer Bindung oder als getrennter Prozess.
4. Datenhaltung in der EU, Verschlüsselung im Transport und im Ruhezustand.
5. Das Produkt entsteht in Ausbaustufen. Jede Stufe ist für sich nutzbar und getestet.

## 2. Systemüberblick

```
+------------------------- Windows-Desktop (EGLE360 PLAN) -------------------------+
|  WPF-Shell (.NET 10)                                                              |
|   ├─ PDF-Arbeitsfläche  : PDFium-Rendering -> Kachel-Cache -> SkiaSharp/GPU       |
|   ├─ Markup-/Mess-Kern  : EGLE360.Core (C#, gemeinsam mit Server)                |
|   ├─ 3D-Ansicht         : WebView2 + three.js + web-ifc / Fragmente              |
|   ├─ Lokale Datenbank   : SQLite (Offline-Cache, Synchronisationsjournal)        |
|   └─ Lokale Dienste     : OCR (Tesseract), IFC-Verarbeitung (Python-Worker)      |
+----------------------------------------|------------------------------------------+
                                         | HTTPS (REST) + WebSocket (SignalR)
+----------------------------------------v------------------------------------------+
|  EGLE360 Server (ASP.NET Core, .NET 10)                                           |
|   ├─ REST-API (OpenAPI), BCF-API, MCP-Server, AVA-Konnektor                       |
|   ├─ Echtzeit-Hub (SignalR) für Sitzungen und Benachrichtigungen                 |
|   ├─ Auftragswarteschlange (PostgreSQL-basiert) für Stapel-, OCR-, KI-, BIM-Jobs |
|   ├─ PostgreSQL 17 (Projektdaten, Markups, Mengen, Audit)                         |
|   └─ Objektspeicher S3-kompatibel (PDF, IFC, Revisionen, Medien)                  |
|  Worker                                                                          |
|   ├─ BIM-Worker (Python, IfcOpenShell): Geometrie, Mengen, Kollision, Vergleich  |
|   └─ KI-Worker (Python): OCR, Symbolerkennung, Planprüfung, Planvergleich        |
+-----------------------------------------------------------------------------------+
```

## 3. Architekturentscheide

### ADR-01 Desktop: C# mit .NET 10 und WPF

Entscheidung: Die Desktop-Anwendung wird in C# auf .NET 10 (LTS, Support bis November 2028) mit WPF gebaut.

Begründung:
- WPF ist ausgereift, für komplexe Fachanwendungen mit vielen Panels, Datenrastern und Mehrmonitorbetrieb erprobt und wird von Microsoft weiter gepflegt. Für Docking gibt es freie Bibliotheken (z. B. AvalonDock, MS-PL).
- WinUI 3 wäre moderner, hat aber nach unserem Kenntnisstand weiterhin Lücken bei Mehrfensterbetrieb, Designer-Unterstützung und Drittanbieter-Steuerelementen. Für ein Werkzeug, das auf mehreren Monitoren mit abgedockten Panels läuft, ist das ein Risiko.
- Lizenz: .NET und WPF sind MIT-lizenziert.
- Leistung: Die Plandarstellung läuft nicht über WPF-Vektorgrafik, sondern über einen eigenen GPU-Kachelrenderer (ADR-03). WPF trägt nur Oberfläche und Panels.
- Wartbarkeit: Eine Sprache (C#) für Desktop, Server und Rechenkern.

Verworfen: Electron/Tauri als reine Web-Oberfläche. Vorteil wäre ein gemeinsamer Code für Web und Desktop; Nachteile sind höherer Speicherbedarf bei großen Plansätzen und schwächere Integration mit Windows-Druck, Zertifikatsspeicher, Smartcards und Office.

### ADR-02 Gemeinsamer Rechenkern EGLE360.Core

Entscheidung: Geometrie, Maßstäbe, Messungen, Mengenregeln, Markup-Modell und Formelspalten liegen in einer plattformneutralen .NET-Bibliothek ohne UI-Abhängigkeit. Desktop und Server verwenden dieselbe Bibliothek.

Begründung: Eine Menge darf nicht auf dem Desktop anders berechnet werden als im Bericht des Servers. Ein einziger Rechenkern ist die Voraussetzung für nachweislich korrekte und reproduzierbare Mengen (Matrix QTO, QTO-SCL). Rechenwerte werden intern in Dezimalarithmetik bzw. doppelter Genauigkeit mit definierter Rundung erst bei der Ausgabe geführt. Jede Rechenregel bekommt Referenzfälle mit analytisch bekanntem Ergebnis.

Der Web-Client (spätere Stufe) zeigt Messungen an, die eigentliche Berechnung bestätigt der Server mit demselben Kern.

### ADR-03 PDF-Engine: PDFium + QPDF + eigene Annotationsschicht

Entscheidung: 
- Rendering und Textextraktion: PDFium (BSD-3-Clause/Apache-2.0), angebunden über eine schlanke eigene P/Invoke-Schicht.
- Strukturelle Operationen (Zusammenführen, Aufteilen, Seiten ersetzen, Verschlüsselung, Linearisierung, Reparatur): QPDF (Apache-2.0).
- Annotationen, Messdaten, Ebenen und inkrementelles Speichern: eigene Schicht in EGLE360.Core, die standardkonforme Annotationen schreibt (inkl. Messwörterbuch /Measure nach ISO 32000) und Zusatzdaten in einem eigenen Namensraum ablegt.
- Signaturen: PAdES über BouncyCastle (MIT), Validierung gegen EU-DSS als Testreferenz.
- PDF/A-Prüfung: veraPDF als getrennter Prozess (MPL-2.0/GPL-3.0 dual, Nutzung unter MPL).
- OCR: Tesseract 5 (Apache-2.0) lokal, mit deutschem Sprachmodell.

Begründung:
- Lizenz: Alle Komponenten erlauben proprietären Vertrieb. MuPDF und iText wurden ausgeschlossen, weil sie ohne kostenpflichtige Lizenz unter AGPL stehen.
- Leistung: PDFium ist die Engine von Chrome und rendert große Vektorpläne schnell. Die Darstellung läuft gekachelt mit mehreren Auflösungsstufen und Cache; Kacheln werden asynchron gerendert und über SkiaSharp (MIT) auf der GPU zusammengesetzt.
- Sicherheit: PDFium wird laufend gegen Fuzzing gehärtet. PDF-Verarbeitung aus unbekannten Quellen läuft auf dem Server in isolierten Worker-Prozessen.
- Unbeabsichtigte Änderungen: Speichern erfolgt standardmäßig inkrementell, der Originalteil der Datei bleibt bytegleich (Matrix PDF-DOC-003).

Risiko und Alternative: Digitale Signaturen, dauerhafte Schwärzung, PDF/A-Erzeugung und Textbearbeitung sind mit freien Bausteinen aufwendig. Ein kommerzielles SDK (z. B. Apryse, Foxit, Nutrient) würde diese Teile verkürzen, verursacht aber laufende Lizenzkosten pro Nutzer oder Server und eine Herstellerabhängigkeit. Empfehlung: mit dem freien Stack starten und nach Phase 4 einen festen Prüfpunkt einplanen. Wenn Schwärzung oder Signaturen dort die Akzeptanztests nicht erreichen, wird für diese Teile ein SDK lizenziert. Diese Entscheidung braucht eine Freigabe durch EGLE360 (siehe Abschnitt 7).

### ADR-04 BIM-Engine: IfcOpenShell im Worker, Darstellung mit three.js

Entscheidung:
- IFC-Verarbeitung (Einlesen, Geometrie, Mengen, Kollision, Modellvergleich): IfcOpenShell 0.8 (LGPL-3.0) in einem Python-Worker, als getrennter Prozess auf Desktop und Server.
- Darstellung: Die Geometrie wird in ein kompaktes, kachelbares Format (glTF bzw. Fragment-Dateien) überführt und in einer WebView2-Ansicht mit three.js (MIT) gerendert. Für schnelles clientseitiges Laden kann web-ifc (MPL-2.0) bzw. die Bibliotheken von That Open Company (MIT) eingesetzt werden.
- Kollisionsprüfung: Breitphase mit räumlichem Index (BVH), Feinphase mit exakter Dreiecksnetz-Prüfung; IfcOpenShell bringt dafür IfcClash mit, das als Grundlage geprüft wird.
- BCF: eigene Implementierung von BCF 2.1 und 3.0 (XML-Schemas von buildingSMART, offen), BCF-API 3.0 im Server.

Begründung:
- IfcOpenShell unterstützt IFC2x3, IFC4 und IFC4.3, ist der verbreitetste offene IFC-Kern und hat eine aktive Entwicklergemeinschaft. LGPL ist bei getrennter Prozessausführung unproblematisch; Änderungen an IfcOpenShell selbst müssten veröffentlicht werden.
- Der Viewer auf Web-Technik läuft identisch im Desktop, im Browser und später mobil. Das spart eine zweite 3D-Implementierung.
- Leistung: Große Modelle werden serverseitig vorverarbeitet (Geometrie, Instanzierung, LOD), der Client lädt nur das, was sichtbar ist.
- Verworfen: xBIM (CDDL, Geometriekern weniger aktiv gepflegt) und kommerzielle Kerne wie ODA IFC SDK (Mitgliedsbeitrag), die Option bleibt offen, falls Kompatibilitätstests mit IfcOpenShell scheitern.

Mengenherkunft: Jede BIM-Menge trägt eine Herkunft (IFC-Qto, geometrisch berechnet, manuell korrigiert). Die drei Arten liegen in getrennten Feldern und werden nie stillschweigend vermischt (Matrix BIM-QTO-003).

### ADR-05 Backend: ASP.NET Core, PostgreSQL, S3-Objektspeicher

Entscheidung:
- ASP.NET Core (.NET 10) als modularer Monolith mit klar getrennten Modulen (Projekte, Dokumente, Markups, Mengen, BIM, Aufgaben, KI, AVA). Ein Aufteilen in Microservices erst, wenn Last oder Teamgröße es verlangen.
- PostgreSQL 17 als einzige relationale Datenbank, inkl. JSONB für benutzerdefinierte Eigenschaften und Volltextsuche.
- Objektspeicher S3-kompatibel für Dateien. Dateien sind unveränderlich und über SHA-256 adressiert; jede Revision ist eine neue Datei.
- Auftragswarteschlange auf PostgreSQL-Basis (z. B. Tabelle mit `SKIP LOCKED`), damit keine zusätzliche Infrastruktur nötig ist.
- Echtzeit: SignalR (MIT) über WebSocket.

Begründung:
- Lizenz: alle MIT/PostgreSQL-Lizenz. MinIO wurde wegen AGPL nicht als Standard gewählt; Betrieb wahlweise auf Azure Blob, AWS S3 oder einem S3-kompatiblen Speicher eines EU-Anbieters.
- Sicherheit: Verschlüsselung im Ruhezustand durch Datenbank und Speicherdienst, TLS 1.3 im Transport, Zugriff auf Dateien nur über kurzlebige signierte URLs.
- Wartbarkeit: Ein Datenbanksystem, ein Laufzeitsystem, eine Sprache für Server und Rechenkern.

### ADR-06 Zusammenarbeit und Offline-Betrieb

Entscheidung: Markups werden als einzelne Objekte mit Versionsnummer synchronisiert, nicht als ganze PDF-Datei. Der Server vergibt eine fortlaufende Reihenfolge für alle Änderungen eines Dokuments (Operationsjournal). Konflikte entstehen nur, wenn zwei Personen dasselbe Markup gleichzeitig ändern. Dann gewinnt die zeitlich spätere Änderung auf Feldebene, die überschriebene Fassung bleibt in der Historie und wird angezeigt.

Offline arbeitet der Desktop gegen seine lokale SQLite-Datenbank und spielt das Journal bei Wiederverbindung ein (Matrix COL-004).

Begründung: Markup-weise Synchronisation braucht wenig Bandbreite, ist nachvollziehbar und vermeidet das Zusammenführen ganzer PDF-Dateien. CRDT-Verfahren wären für Freitext in Kommentaren denkbar, sind aber für den Start nicht nötig.

### ADR-07 KI-System

Entscheidung:
- Eigener KI-Worker in Python, getrennt von der Anwendung.
- OCR: Tesseract lokal; für schwierige Scans optional PaddleOCR (Apache-2.0).
- Symbol- und Bauteilerkennung in Plänen: eigene Modelle auf Basis von Apache-2.0-lizenzierten Frameworks (z. B. MMDetection oder Detectron2), trainiert auf eigenen, rechtlich sauberen Planbeständen. Ultralytics YOLO wird wegen AGPL nicht verwendet.
- Planprüfung zunächst regel- und vektorbasiert: Plannummern, Indizes, Tür- und Raumkennzeichnungen werden aus dem Vektortext gelesen und mit Listen abgeglichen. Das ist genauer und nachvollziehbarer als reine Bilderkennung und liefert für jeden Befund die Fundstelle.
- Sprachmodell für EGLE360 AI: über eine austauschbare Schnittstelle. Standard ist ein großes Sprachmodell über API mit EU-Datenverarbeitung und vertraglichem Ausschluss von Training mit Kundendaten; lokale Modelle als Option für Kunden mit strengen Vorgaben.
- MCP-Server: EGLE360 PLAN stellt Werkzeuge über das Model Context Protocol bereit. Lesende Werkzeuge sind frei, schreibende brauchen eine Bestätigung im Programm.

Begründung: Die KI soll ihre Quellen angeben und unsichere Ergebnisse kennzeichnen (Master-Prompt Abschnitt 10, 11, 22). Ein regelbasierter Kern mit KI-Unterstützung erfüllt das besser als ein reines Modell. Daten verlassen die EU nicht ohne Zustimmung des Kunden.

### ADR-08 Identität und Sicherheit

Entscheidung:
- Anmeldung über OpenID Connect. Standard-Identitätsanbieter Keycloak (Apache-2.0) im eigenen Betrieb; Microsoft Entra ID als Alternative für Kunden mit Microsoft 365. MFA wird über den Identitätsanbieter erzwungen.
- Rechte: Rollen auf Organisationsebene, Projektebene und Dokumentebene; Prüfung ausschließlich serverseitig.
- Audit-Log als nur anhängbare Tabelle mit Hash-Verkettung, damit nachträgliche Änderungen erkennbar sind.
- DSGVO: Datenhaltung in einer EU-Region, Auftragsverarbeitungsvertrag, Lösch- und Auskunftsfunktionen, Protokollierung von Zugriffen auf personenbezogene Daten.
- Backup: tägliche Vollsicherung plus fortlaufende WAL-Archivierung (Point-in-Time-Recovery), Objektspeicher versioniert, Wiederherstellung quartalsweise getestet.

### ADR-09 Schnittstelle zu EGLE360 AVA

Entscheidung: Die Übergabe läuft über eine versionierte REST-Schnittstelle. Jede Mengenübergabe trägt eine eindeutige Übergabe-ID und einen Prüfwert. Die AVA-Seite lehnt eine bereits verbuchte Übergabe-ID ab (Idempotenz), damit keine Menge doppelt übernommen wird. Zusätzlich werden ÖNORM A 2063 (Österreich) und GAEB DA XML (Deutschland) für den Austausch mit Drittprogrammen vorgesehen.

Offen: Eine Spezifikation der bestehenden EGLE360-AVA-Schnittstelle liegt noch nicht vor.

### ADR-10 Repository, Build und Tests

Entscheidung:
- Ein Repository (`EGLE360/EGLE360-PLAN`) mit folgender Struktur:

```
src/
  Egle360.Core/          Rechenkern, Markup-Modell, Mengenregeln
  Egle360.Pdf/           PDFium-/QPDF-Anbindung, Annotationen, Signaturen
  Egle360.Desktop/       WPF-Anwendung
  Egle360.Server/        ASP.NET Core
  Egle360.Viewer3D/      Web-Viewer (TypeScript, three.js)
workers/
  bim/                   Python, IfcOpenShell
  ai/                    Python, OCR, Erkennung, Prüfung
db/                      Migrationen
tests/                   Unit-, Integrations-, Rendering-, Geometrietests
testdata/                Referenzpläne, IFC-Testmodelle, erwartete Ergebnisse
docs/                    Architektur, Funktionsmatrix, Handbuch
brand/                   Logodateien (unverändert)
```

- CI über GitHub Actions mit Windows-Runner für den Desktop-Build und Linux für Server und Worker.
- Jede Berechnung bekommt Referenztests mit nachvollziehbaren Sollwerten (Master-Prompt Abschnitt 34).
- Installationspaket: MSIX oder MSI (WiX Toolset, MS-RL), signiert mit Code-Signing-Zertifikat.

## 4. Datenmodell (Kurzfassung)

Das vollständige Schema steht in `EGLE360_PLAN_Datenbankschema.sql`. Kernpunkte:

- Alle Primärschlüssel sind UUIDv7 (zeitlich sortierbar, global eindeutig, auch offline erzeugbar).
- Dokumente sind unveränderlich versioniert: `document` ist die logische Einheit (ein Plan), `document_version` eine konkrete Datei mit Hash.
- Markups gehören zu einer Dokumentversion und tragen eine eigene Revisionsnummer für die Synchronisation. Beim Planaustausch werden Markups in die neue Version übertragen und die Herkunft gespeichert.
- Mengen (`quantity`) haben ein Pflichtfeld `source` (pdf_measure, ifc_qto, ifc_geometry, manual) und einen Freigabestatus. Übergaben an die AVA (`ava_transfer`) sind eindeutig und referenzieren die übergebenen Mengen.
- Benutzerdefinierte Eigenschaften liegen als JSONB, damit Kunden eigene Spalten anlegen können, ohne das Schema zu ändern.
- Änderungsprotokoll (`audit_log`) für alle schreibenden Vorgänge.

## 5. Leistungsziele (erste Fassung)

| Szenario | Ziel auf Referenzrechner |
|---|---|
| A0-Vektorplan öffnen, erste Darstellung | unter 2 s |
| Schwenken und Zoomen | mindestens 60 Bilder/s |
| Plansatz mit 500 Blättern, Miniaturen | progressiv, erste Seiten unter 1 s |
| Markup-Liste mit 10.000 Einträgen filtern | unter 200 ms |
| IFC-Modell 500 MB öffnen (vorverarbeitet) | unter 15 s, Navigation mit 30 Bildern/s |
| Echtzeit-Änderung bei zweitem Teilnehmer sichtbar | unter 1 s |

Referenzrechner und Testdaten werden in Phase 12 verbindlich festgelegt.

## 6. Ausbaustufen

Ein Produkt im Umfang des Master-Prompts ist ein mehrjähriges Vorhaben für ein Entwicklungsteam. Die Reihenfolge folgt Abschnitt 35 des Master-Prompts; vorgeschlagen ist, die erste auslieferbare Version auf die MVP-Zeilen der Funktionsmatrix zu beschränken:

1. Stufe A (Phasen 3 bis 5): Designsystem, PDF-Grundsystem, Markups, Markup-Liste, 2D-Mengenermittlung mit Maßstäben, Planverwaltung, Excel-Export. Ergebnis: Ein Kalkulant kann Pläne öffnen, markieren, messen und Mengen exportieren.
2. Stufe B (Phase 6): Planvergleich, Overlay, Planaustausch, Stapelverarbeitung.
3. Stufe C (Phasen 7 und 8): IFC-Viewer, BIM-Mengen, Kollisionsprüfung, BCF.
4. Stufe D (Phasen 9 bis 11): 4D, 5D mit AVA, EGLE360 AI, Echtzeit-Zusammenarbeit.

## 7. Offene Entscheidungen für EGLE360

1. Kommerzielles PDF-SDK als Rückfalloption für Signaturen und Schwärzung (ADR-03): Budget ja oder nein.
2. Hosting-Anbieter und Region in der EU (ADR-05).
3. Spezifikation der EGLE360-AVA-Schnittstelle (ADR-09).
4. Lizenzmodell des Produkts (proprietär oder teilweise offen), weil davon die Bibliotheksauswahl abhängt.
5. Vektorfassungen (SVG) der Logos für das Designsystem; die gelieferten Dateien sind Rasterbilder.

## 8. Quellen

- Bluebeam Support, Funktionen nach Subscription-Paket: https://support.bluebeam.com/de/revu/subscription/subscription-features.html
- Bluebeam Support, Revu 21 subscription features comparison: https://support.bluebeam.com/articles/revu-21-subscription-features
- Bluebeam Max: https://www.bluebeam.com/bluebeam-max/
- Smart Overlay und Smart Review FAQ: https://support.bluebeam.com/revu/resources/smart-overlay-review-faq.html
- Weitere Quellen siehe Tabellenblatt „Quellen“ der Funktionsmatrix.

Lizenzangaben zu Drittbibliotheken entsprechen dem Kenntnisstand bei Erstellung und sind vor der Einbindung jeweils an der aktuellen Lizenzdatei der Bibliothek zu prüfen.

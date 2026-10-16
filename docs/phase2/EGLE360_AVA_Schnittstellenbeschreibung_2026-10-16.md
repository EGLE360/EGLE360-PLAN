# EGLE360 AVA: Schnittstellenbeschreibung

Stand: 16.10.2026. Grundlage ist der Code auf dem Branch `phase-5-lv` (Commit 0ad8f16, PR #2, noch nicht in `main`), der Entwurf [04_schnittstellen.md](04_schnittstellen.md) aus Phase 2, die Recherche [04a_normen_formate_recherche.md](04a_normen_formate_recherche.md) und der ORCA-Abgleich in [abgleich/](abgleich/00_uebersicht.md). Quellen-URLs zu Norm- und Formatangaben stehen in der Recherche und werden hier nicht wiederholt.

Die Beschreibung trennt streng zwischen dem, was heute im Code steht (Teil A), und dem, was geplant ist (Teil B). „Vorhanden“ heißt: im Code umgesetzt und durch automatische Tests abgedeckt. Es heißt nicht, dass ein Format gegen ein offizielles Prüfwerkzeug zertifiziert ist.

## Übersicht

| Schnittstelle | Format, Version | Richtung | Stand |
|---|---|---|---|
| Berichte als PDF | PDF 1.x aus Chromium (kein PDF/A) | Export | vorhanden |
| Berichte als Word | DOCX (Office Open XML) | Export | vorhanden |
| Tabellen als Excel | XLSX (Office Open XML) | Export | vorhanden, 4 Tabellenarten |
| E-Mail an Bieter | SMTP, .eml-Entwürfe (MIME) | Ausgang | vorhanden |
| Kostengliederung aus Text | Tab, Semikolon oder Leerzeichen getrennt | Import | vorhanden |
| Datensicherung | SQLite-Datei | Export und Import | vorhanden |
| Interne Dienstschnittstelle | RPC über Electron-IPC, JSON | intern | vorhanden |
| GAEB DA XML | 3.3, zusätzlich 3.2 | Import und Export | geplant, Stufe 1 |
| GAEB 90 | – | Import (Export prüfen) | geplant, Stufe 2 |
| ÖNORM A 2063 (ONLV, ONRE, ONLB) | A 2063-1:2021-03-15 | Import und Export | geplant, Stufe 1, Norm fehlt |
| Vergabeplattformen | über GAEB X83/X84 | Export und Import | geplant |
| Excel-Import | XLSX | Import | geplant, Stufe 1 |
| CSV, JSON, Projektpaket | eigenes Schema | Import und Export | geplant |
| PDF/A | PDF/A-2b oder -3b | Export | geplant, Stufe 2 |
| XRechnung, ZUGFeRD | 3.0.x / 2.x (EN 16931) | Import | geplant, Stufe 2 |
| ebInterface, Peppol BIS | 6.x / 3.0 | Import | geplant, Stufe 3 |
| IFC | 2x3, 4, 4.3 | Import | geplant, Stufe 3 |
| DATANORM, BMEcat | 4/5, 2005 | Import | geplant, Stufe 3 |
| vCard | 3.0 / 4.0 | Import und Export | geplant, Stufe 2 |
| Öffentliche API für Fremdprogramme | HTTPS/JSON | beide | geplant, später |
| Serverbetrieb | HTTPS, WebSocket, PostgreSQL | intern | geplant, nicht begonnen |

Stufe 1 entsteht in Phase 7 zuerst, Stufe 2 danach, Stufe 3 nach Bedarf. Die Reihenfolge kann Elias ändern.

---

# Teil A: Was heute vorhanden ist

## A1. Berichte als PDF

- Acht Berichtsarten: LV (auch als Blankett ohne Preise), Mengenermittlung, Preisspiegel, Vergabevorschlag, Rechnung (Rechnungsprüfung mit Aufmaß), Nachtrag, Kostenkontrolle, Abrechnungsübersicht.
- Technik: Die Anwendung erzeugt die Berichte als HTML (`packages/app-services/src/services/berichte.ts`). Die Desktop-Anwendung rendert sie in einem unsichtbaren Fenster und speichert sie mit der Chromium-Druckfunktion `printToPDF` als PDF, A4 hoch oder quer.
- Kopf mit Projekt, Stand, Absender und Logo. Logo und Absender sind unter Verwaltung > Berichte einstellbar, sonst erscheint das EGLE360-Logo. Fuß mit Titel und „Seite x von y“.
- Druckvorschau in einem eigenen Fenster mit dem eingebauten PDF-Betrachter. Die Vorschau ist nicht bearbeitbar. Unter der Testumgebung (Linux ohne Bildschirm) lässt sich ihr Aussehen nicht prüfen.
- Kein PDF/A. Chromium erzeugt kein PDF/A.

## A2. Berichte als Word (DOCX)

- Jeder der acht Berichte lässt sich als Word-Dokument speichern.
- Eigener Schreiber ohne Fremdbibliothek (`export/docx.ts`). Er übersetzt die bekannten HTML-Elemente der Berichte in WordprocessingML: Überschriften, Absätze mit fett, kursiv und unterstrichen, Tabellen mit Kopfzeilen und verbundenen Zellen, rechtsbündige Zahlen, Logo als eingebettetes Bild, Fußzeile mit Seitenzahlen.
- Spaltenbreiten werden geschätzt. Das Word-Dokument ist dem PDF ähnlich, aber nicht gleich.

## A3. Tabellen als Excel (XLSX)

- Vier Tabellenarten: LV, Preisspiegel (Blätter Reihung und Positionen), Kostenkontrolle (wahlweise aus einer Momentaufnahme), Abrechnungsübersicht.
- Eigener Schreiber ohne Fremdbibliothek (`export/xlsx.ts`): Text und Zahlen mit festen Nachkommastellen, fette Summenzeilen, fixierte Kopfzeile, Spaltenbreiten.
- Bewusst ohne Formeln. Die Werte sind genau die gerundeten Werte, die das Programm anzeigt.
- Die Exporte laufen über dieselben Dienste wie die Bildschirmansicht, es gelten dieselben Berechtigungen.
- Geprüft wurden die Dateien mit openpyxl und LibreOffice, nicht mit Microsoft Excel.
- Ein Excel-Import fehlt (siehe B4).

## A4. E-Mail-Versand der Angebotsanforderung

- Versand an die eingeladenen Bieter einer Ausschreibung, jede Mail einzeln an genau einen Empfänger, damit sich die Bieter nicht gegenseitig sehen.
- Anhänge: das LV als PDF (aus A1) und weitere frei gewählte Dateien, zum Beispiel Pläne. Nur Dateien, die im Dateidialog gewählt wurden, dürfen gelesen und versendet werden.
- Zwei Wege:
  - direkt über SMTP (Bibliothek nodemailer 7.0.10), Verschlüsselung SSL/TLS, STARTTLS oder keine; Server, Port, Benutzer und Absender unter Verwaltung > E-Mail, mit Testversand,
  - als Entwurf: je Bieter eine `.eml`-Datei (MIME-Nachricht) in einem gewählten Ordner, die der Anwender in seinem Mailprogramm öffnet und selbst abschickt.
- Das SMTP-Passwort liegt nicht in der Datenbank, sondern verschlüsselt über die Schlüsselablage des Betriebssystems (Electron `safeStorage`).
- Jeder Versand und jeder Entwurf wird mit Empfänger, Betreff, Anhängen und Ergebnis protokolliert (Tabelle `mail_versand`). Ein fehlgeschlagener Empfänger hält die anderen nicht auf.
- Kein Empfang: Antworten und Angebote kommen nicht per Mail ins Programm.

## A5. Kostengliederung aus Text

- Kostengruppen einer Gliederung (DIN 276, ÖNORM B 1801-1 oder eigen) lassen sich aus eingefügtem Text übernehmen, eine Gruppe pro Zeile, Nummer und Bezeichnung getrennt durch Tabulator, Semikolon oder Leerzeichen. Damit lässt sich eine Liste aus Excel oder aus einer lizenzierten Quelle hineinkopieren.
- Die Hierarchie ergibt sich aus den Nummern (300, 310, 311 oder 2, 2.01, 2.01.03). Diese Regel stammt aus der Praxis und ist nicht mit ORCA abgeglichen.
- Ergänzen oder Ersetzen. Ersetzen ist gesperrt, sobald die Gruppen in Kostenständen oder LV-Positionen benutzt werden.
- Es gibt keinen Dateiimport (CSV, XLSX, GAEB X50) dafür. Das kommt mit B4 und B1.

## A6. Datensicherung und Wiederherstellung

- Sicherung der gesamten Datenbank als SQLite-Datei (`EGLE360-AVA-Sicherung-<Datum>.sqlite`) im laufenden Betrieb als konsistente Momentaufnahme.
- Vor der Wiederherstellung wird die Datei auf Unversehrtheit und Fremdschlüssel geprüft. Wiederherstellen ersetzt den ganzen Datenbestand und braucht eine Bestätigung.
- Nur mit dem Recht `admin.system`.
- Das ist kein Austauschformat: Es gibt keinen Export oder Import einzelner Projekte. Dafür ist das Projektpaket vorgesehen (B4).

## A7. Interne Dienstschnittstelle

Diese Schnittstelle verbindet Oberfläche und Anwendung. Sie ist nicht für Fremdprogramme gedacht.

- Die Oberfläche läuft abgeschottet (contextIsolation, sandbox, kein Node-Zugriff) und bekommt genau einen Aufrufweg: `window.egle360.api.call(methode, eingabe)`, über Electron-IPC auf dem Kanal `egle360:api`.
- Rund 195 Methoden in fachlichen Bereichen, benannt als `bereich.aktion`, zum Beispiel `lv.lesen`, `rechnung.freigeben`, `kostenkontrolle.lesen` (Liste in `packages/app-services/src/api.ts`).
- Jede Methode prüft ihre Eingabe mit einem Zod-Schema und prüft die Berechtigung. Schreibende Methoden laufen in einer Transaktion und schreiben ins Änderungsprotokoll.
- Antwort immer als `{ ok: true, value }` oder `{ ok: false, error: { code, message } }`. Fehlercodes: VALIDATION, NOT_FOUND, FORBIDDEN, UNAUTHENTICATED, CONFLICT, DUPLICATE, LOCKED, INVALID_STATE.
- Einige Methoden gibt es nur in der Desktop-Anwendung, weil sie Dateidialoge oder das Dateisystem brauchen: `bericht.pdf`, `bericht.docx`, `bericht.xlsx`, `bericht.vorschau`, `system.sichern`, `system.wiederherstellen`, `mail.testen`, `ausschreibung.mailSenden`, `ausschreibung.mailEntwuerfe`. Zwei Methoden (`ausschreibung.mailVorbereiten`, `ausschreibung.mailProtokollieren`) darf nur die Anwendung selbst aufrufen, damit das Versandprotokoll zeigt, was wirklich verschickt wurde.
- Die Geschäftslogik ist über Kysely für SQLite und PostgreSQL geschrieben und wird in den Tests auch gegen PostgreSQL (PGlite) geprüft. Ein Server, der diese Schnittstelle über HTTPS anbietet, existiert noch nicht.

## A8. Vorbereitete Felder ohne fertige Schnittstelle

- Einheiten haben ein Feld für den GAEB-Code.
- OZ-Maske mit Ebenen, Index und Schrittweite sowie die Positionsarten (Normal-, Grund-, Wahl-, Bedarfs-, Pauschalposition, Zulage, Stundenlohn) sind so angelegt, dass sie sich auf GAEB und ÖNORM A 2063 abbilden lassen.
- Mengenansätze speichern Formel und Ergebnis. Eine REB-Formelnummer fehlt noch; sie wird für X31/DA11 gebraucht (B1).

Mehr gibt es heute nicht: kein GAEB, kein ÖNORM A 2063, kein IFC, keine E-Rechnung, kein Import aus Dateien.

---

# Teil B: Was geplant ist

## B0. Grundprinzip für alle Fremdformate

Alle Importe und Exporte laufen über ein internes, verlustfreies Austauschmodell (`e360-exchange`). Jedes Fremdformat bekommt einen eigenen Leser und Schreiber, die nur zwischen Fremdformat und diesem Modell übersetzen.

Jeder Import prüft die Datei gegen das Schema (falls vorhanden), zeigt eine Vorschau mit Warnungen, schreibt erst nach Bestätigung in einer einzigen Transaktion und legt die Originaldatei unverändert ab. Jeder Export protokolliert, was das Zielformat nicht abbilden konnte.

Ein Format gilt erst als unterstützt, wenn Schemaprüfung, Referenzimport, Roundtrip (Import, Export, Import) und Summenabgleich automatisch grün sind.

## B1. GAEB (Deutschland)

| Format | Version | Richtung | Stufe |
|---|---|---|---|
| GAEB DA XML | 3.3 (2021-05; X31 2023-01) | Import und Export | 1 |
| GAEB DA XML | 3.2 | Import und Export | 1, weil einzelne Auftraggeber und auch ORCA beim Export noch 3.2 als Standard nutzen |
| GAEB DA XML | 3.4 | – | erst nach finaler Veröffentlichung (derzeit Beta) |
| GAEB 90 | – | Import, Export prüfen | 2; ORCA liest und schreibt es laut Hersteller weiterhin |
| GAEB 2000 | – | nur Import | 2, nur für Altbestände |
| GAEB DA11 (REB-Aufmaß) | – | Import und Export | 2 |

Austauschphasen in der geplanten Reihenfolge:

| Phase | Inhalt | Import | Export | Stufe |
|---|---|---|---|---|
| X83 | Angebotsaufforderung | ja | ja | 1, zuerst |
| X84 | Angebotsabgabe | ja | für Tests | 1, zuerst |
| X81 | Leistungsbeschreibung | ja | ja | 1 |
| X82 | Kostenanschlag | ja | ja | 1 |
| X85 | Nebenangebot | ja | – | 1 |
| X86 | Auftragserteilung, auch Nachträge | ja | ja | 1 |
| X31 | Mengenermittlung | ja | ja | 1 |
| X50 / X51 | Baukostenkatalog / Kostenermittlung | ja | X51 | 2 |
| X80 | LV-Katalog | ja | ja | 2 |
| X87 | Auftragsbestätigung | ja | – | 2 |
| X89 / X89B | Rechnung / Rechnungsbegründung | ja | ja | 2 |
| X83Z, X84Z, X86ZR, X86ZE | Zeitvertragsarbeiten | ja | ja | 3 |

X89 ist keine umsatzsteuerlich gültige E-Rechnung. Die steuerliche Rechnung kommt als XRechnung oder ZUGFeRD (B6), X89 und X31 dienen als Begründung.

Zum Export gehört ein Dialog mit Profilen je Austauschphase und einer Strukturprüfung gegen die OZ-Maske, so wie ORCA ihn anbietet.

## B2. ÖNORM A 2063 (Österreich)

| Format | Version | Richtung | Stufe |
|---|---|---|---|
| ONLV (LV, Angebot, Kostenschätzung) | A 2063-1:2021-03-15, Import auch 2009 und 2015 | Import und Export | 1 |
| ONRE (Abrechnung) | wie oben | Import und Export | 1 |
| ONLB (Leistungsbeschreibung), dazu LB-HB und LB-HT als Bibliothek | wie oben | Import | 1 bzw. 2 |
| A 2063-2 (BIM) | 2021-03-15 | später | 3 |
| frühere B 2062 / B 2063 | – | Import prüfen | 3, ORCA unterstützt sie noch |

Ohne den Normtext und das offizielle XML-Schema lässt sich diese Schnittstelle weder bauen noch prüfen. Eine öffentliche Bezugsquelle für das Schema wurde nicht gefunden.

## B3. Vergabeplattformen

- Plattformen nehmen das LV in der Regel als GAEB X83 entgegen, die Bieter geben X84 zurück. Geplant ist deshalb zuerst ein Plattformexport (X83 mit Strukturprüfung und Prüfprotokoll) und der Angebotsimport (X84), siehe B1. In Österreich entsprechend über ÖNORM A 2063.
- Eine direkte Anbindung an einzelne Plattformen (Hochladen aus dem Programm heraus) ist nicht geplant, solange keine konkrete Plattform gewünscht ist.
- Welche Plattformen Elias und seine Kunden nutzen und welche Anforderungen diese an GAEB-Dateien stellen, ist nicht recherchiert. Ob der Austauschstandard XVergabe dafür eine Rolle spielt, ist ebenfalls offen.
- Eine „digitale Angebotsanforderung“ (HTML-Datei mit Eingabefeldern, wie bei ORCA) ist eine mögliche Ergänzung, aber noch nicht eingeplant.

## B4. Tabellen- und eigene Formate

| Format | Version | Richtung | Inhalt | Stufe |
|---|---|---|---|---|
| XLSX | Office Open XML | Import | LV-Positionen, Mengen, Kostengruppen, mit Spaltenzuordnung und gespeicherten Profilen | 1 |
| XLSX | Office Open XML | Export erweitern | weitere Listen (Bieter, Aufmaß, Kostenermittlung) | 2 |
| CSV | RFC 4180, UTF-8 mit BOM, Trennzeichen wählbar | Import und Export | Listen, Stammdaten, Mengen | 1 |
| JSON | `e360-exchange`, versioniert | Import und Export | Projekt oder Teilbereiche | 1 |
| Projektpaket `.e360ava` | ZIP mit JSON, Manifest, Prüfsummen | Import und Export | ein Projekt mit Dokumenten, auch für den Wechsel Einzelplatz und Server | 1 |
| XML | `e360-exchange` | Export | wie JSON | 2, nur bei Bedarf |
| RTF | – | Export | Berichte | offen; ORCA bietet es an, Nutzen fraglich |

## B5. PDF/A

PDF/A-2b oder -3b für Archivausgaben, Stufe 2. Chromium kann es nicht, also ist eine Nachbearbeitung nötig. Das Werkzeug muss lizenzrechtlich passen (keine AGPL-Komponente). Prüfung mit veraPDF.

## B6. E-Rechnung

| Format | Version | Richtung | Stufe |
|---|---|---|---|
| XRechnung (UBL und CII) | 3.0.x; 4.0 ist angekündigt und nicht kompatibel | Import | 2 |
| ZUGFeRD / Factur-X | 2.x ab Profil EN 16931, XML-Teil maßgeblich | Import | 2 |
| ebInterface | 6.x | Import | 3 |
| Peppol BIS Billing | 3.0 | Import | 3 |

Eingehende Rechnungen werden gegen Auftrags-LV und Aufmaß abgeglichen. Ein Rechnungsausgang ist nicht vorgesehen, weil der Auftraggeber in der AVA Rechnungen prüft und nicht stellt.

## B7. IFC und BIM

- IFC 2x3, IFC 4 und IFC 4.3 (ISO 16739-1:2024), nur Import, Stufe 3.
- Ziel laut Elias: BIM soll so funktionieren wie in ORCA. ORCA bietet Mengenübernahme aus dem Modell, Aktualisieren der Mengen, Räume als Kostengliederung und eine IFC-Diagnose mit CSV-Export.
- Vorgesehene Bibliothek: web-ifc (MPL-2.0). Ob sie IFC 4.3 ausreichend liest, ist selbst zu testen.
- Für den Test werden echte IFC-Dateien aus Elias' Projekten gebraucht.
- Die Verknüpfung LV und Modell nach DIN SPEC 91350 (BIM-LV-Container) kommt danach.

## B8. Stamm- und Katalogdaten

| Format | Version | Richtung | Stufe |
|---|---|---|---|
| vCard | 3.0 / 4.0 | Import und Export | 2 |
| DATANORM | 4 / 5 | Import in Bibliothek und LV | 3 |
| BMEcat | 2005 | Import | 3, bei ORCA nur in der Enterprise-Edition |
| LB-HB / LB-HT | aktuelle Versionen | Import als ONLB | 2 |
| STLB-Bau | Webservice | Einbindung | offen, braucht Partnervertrag |

## B9. Öffentliche API und Serverbetrieb

- Servermodus: eigener EGLE360-AVA-Server mit PostgreSQL. Die Desktop-Clients sprechen per HTTPS (JSON) und WebSocket für Änderungsmeldungen mit ihm, mit denselben Methoden wie in A7. Noch nicht begonnen.
- Öffentliche API für Fremdprogramme, zum Beispiel zum Anlegen von Projekten (in ORCA nur Enterprise): spätere Stufe, auf denselben Methoden und Schemas.

## B10. Kostengliederungen

DIN 276 (2018-12) und ÖNORM B 1801-1 (2022-03) werden strukturell unterstützt. Nummern und Bezeichnungen werden nicht mitgeliefert, solange die Lizenzfrage offen ist. Anwender übernehmen sie heute per Text (A5), später auch per CSV, XLSX oder GAEB X50.

---

# Teil C: Offene Lizenz- und Beschaffungsfragen

| Nr. | Thema | Offene Frage | Betrifft | Ansprechpartner |
|---|---|---|---|---|
| 1 | ÖNORM A 2063-1 und -2 | Normtext kaufen (rund 515 € netto für beide Teile laut Recherche), Bezugsquelle und Lizenz des XML-Schemas klären | B2 | Austrian Standards |
| 2 | GAEB-XSD | Dürfen die Schemas mit der Software ausgeliefert werden? Bis dahin nur in Entwicklung und Test | B1 | GAEB / DIN |
| 3 | BVBS-Zertifizierung GAEB DA XML 3.3 | aktuelle Kosten und Prüfkatalog; Zertifizierung erst behaupten, wenn erteilt | B1 | BVBS |
| 4 | DIN 276, ÖNORM B 1801-1 | Dürfen Nummern und Bezeichnungen der Kostengruppen mitgeliefert werden? | B10 | DIN Media, Austrian Standards |
| 5 | REB-VB 23.003 | Formelkatalog und DA11-Spezifikation beschaffen | B1 (X31, DA11) | DIN Media / GAEB |
| 6 | LB-HB, LB-HT, LB-VI | Nutzungsbedingungen für Softwarehersteller; LB-VI Preis und Lizenz | B2, B8 | BMWET, FSV |
| 7 | STLB-Bau | Partnervertrag; Kunde braucht eigenes Abo | B8 | DIN Media / DBD |
| 8 | ebInterface-Schemas | Lizenz nicht ausdrücklich angegeben | B6 | WKO |
| 9 | Prüfwerkzeug A 2063 | Darf ein frei nutzbarer Online-Checker eines Drittanbieters für automatische Tests genutzt werden? | B2 | Anbieter |
| 10 | IFC-Testdaten | echte IFC-Dateien aus Projekten | B7 | Elias |
| 11 | Vergabeplattformen | Welche Plattformen sind gewünscht, welche Dateianforderungen haben sie? | B3 | Elias |

Punkte 1 und 10 sollten vor Phase 7 erledigt sein.

---

# Teil D: Abweichungen zum Entwurf vom 15.10.2026

- PDF und Word sind entgegen dem Entwurf schon vorhanden, Word sogar für alle Berichte (im Entwurf Stufe 2). Die Funktionsmatrix führt SN-11 und SN-12 noch als „offen“; das ist veraltet.
- Excel gibt es heute nur als Export, ohne Formeln. Der Import fehlt.
- E-Mail-Versand (SMTP und .eml) war im Entwurf nicht als Schnittstelle aufgeführt und ist dazugekommen.
- Die Kostengliederung lässt sich per Text übernehmen. Im Entwurf war dafür CSV, XLSX oder X50 vorgesehen.
- Die interne Schnittstelle ist einfacher als geplant: ein IPC-Kanal mit einer Methodenliste und Zod-Prüfung je Methode statt einer eigenen Vertragsschicht `rpc-contract`.
- Die Reihenfolge innerhalb von GAEB ist neu: zuerst X83 und X84, weil die Ausschreibung ohne sie nicht über Plattformen läuft.
- GAEB 90 rückt von „nur Import“ zu „Import, Export prüfen“, weil ORCA es weiterhin exportiert.

# Erzeugt die Funktionsmatrix EGLE360 PLAN (Phase 1) als XLSX und CSV.
import csv, sys
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment
from openpyxl.utils import get_column_letter

OUT = sys.argv[1]

SOURCES = {
    "Q1": ("Bluebeam Support: Funktionen nach Subscription-Paket (Revu 21, DE)", "https://support.bluebeam.com/de/revu/subscription/subscription-features.html"),
    "Q2": ("Bluebeam Support: Revu 21 subscription features comparison (inkl. Max)", "https://support.bluebeam.com/articles/revu-21-subscription-features"),
    "Q3": ("Bluebeam: Produktseite Bluebeam Max", "https://www.bluebeam.com/bluebeam-max/"),
    "Q4": ("Bluebeam Support: Keyboard shortcuts and mouse navigation", "https://support.bluebeam.com/revu/resources/keyboard-shortcuts.html"),
    "Q5": ("Bluebeam Support: Measure toolbar", "https://support.bluebeam.com/user-manual/toolbars/measure-toolbar.html"),
    "Q6": ("Bluebeam Support: Takeoffs and measurements", "https://support.bluebeam.com/user-manual/menus/tools/takeoffs-measurements.html"),
    "Q7": ("Bluebeam Support: Markups List", "https://support.bluebeam.com/user-manual/menus/window/markups-list.html"),
    "Q8": ("Bluebeam Support: Smart Overlay and Smart Review FAQs", "https://support.bluebeam.com/revu/resources/smart-overlay-review-faq.html"),
    "Q9": ("Bluebeam Support: Batch menu", "https://support.bluebeam.com/user-manual/menus/batch/batch-menu.html"),
    "Q10": ("Architosh (Sekundärquelle): AI-powered Bluebeam Max launches globally, Mai 2026", "https://architosh.com/2026/05/ai-powered-bluebeam-max-launches-globally/"),
    "MP": ("Master-Prompt EGLE360 PLAN (Projektthread, 16.10.2026)", "Projektthread"),
    "Q11": ("Abgleich mit lokal installiertem Bluebeam Revu 21.11.0.23287 (Daten-, Konfigurations- und Vorlagendateien)", "phase1/EGLE360_PLAN_Bluebeam-Abgleich_Revu21.md"),
}

VER = {
    "V": "Verifiziert (offizielle Bluebeam-Quelle)",
    "S": "Nur Sekundärquelle",
    "U": "Ungeklärt (nicht ausreichend dokumentiert)",
    "E": "EGLE360-Erweiterung (kein Referenzumfang)",
    "L": "Belegt durch lokale Installation Revu 21.11",
}
PRIO = {"M": "MVP", "H": "Hoch", "N": "Später"}

# Module: (Präfix, Bereich, Modul, Phase, [Zeilen])
# Zeile: (Funktion, Anforderung, Art, Paket, Kürzel, Quelle, Verif, Prio, Abhängigkeiten, Akzeptanzkriterium)
M = []

def mod(prefix, bereich, modul, phase, rows):
    M.append((prefix, bereich, modul, phase, rows))

mod("PDF-DOC", "PDF-Engine", "Dokumentfunktionen", 4, [
    ("PDF öffnen", "PDF 1.0 bis 2.0 öffnen, auch beschädigte Dateien mit Reparaturversuch", "Dokument", "Basics", "Strg+O", "Q4", "V", "M", "", "Testkorpus aus 200 realen Bauplänen öffnet ohne Fehler, Darstellung pixelgleich zur Referenz-Rendering-Engine (Toleranz siehe QS-Plan)"),
    ("PDF erstellen", "Neues PDF leer oder aus Bild/Office-Datei erzeugen", "Dokument", "Basics", "Strg+N", "Q1/Q4", "V", "M", "", "Erzeugte Datei besteht Validierung mit veraPDF bzw. qpdf --check"),
    ("PDF speichern / speichern unter", "Inkrementelles Speichern ohne Veränderung des Originalinhalts; Speichern unter mit vollständigem Neuschreiben", "Dokument", "Basics", "Strg+S / Strg+Umschalt+S", "Q4", "V", "M", "PDF-DOC-001", "Byte-Vergleich: Originalteil der Datei bleibt bei inkrementellem Speichern unverändert"),
    ("Alle speichern", "Alle geöffneten Dokumente speichern", "Dokument", "Basics", "Strg+F2", "Q4", "V", "H", "PDF-DOC-003", "Alle geänderten Registerkarten sind nach Aufruf gespeichert"),
    ("PDF zusammenführen", "Mehrere PDFs in definierter Reihenfolge zusammenführen, Lesezeichen übernehmen", "Dokument", "Basics", "", "Q1", "V", "M", "", "Seitenzahl Ergebnis = Summe der Eingaben; Lesezeichen und Links bleiben funktionsfähig"),
    ("PDF aufteilen", "Nach Seitenzahl, Lesezeichen oder Seitenbereichen aufteilen; Original bleibt unverändert", "Dokument", "Basics", "", "Q1/Q9", "V", "M", "", "Testfälle für alle drei Aufteilungsarten bestanden"),
    ("Seiten einfügen", "Seiten aus Datei oder Leerseiten einfügen", "Seite", "Basics", "Strg+Umschalt+I / Strg+Umschalt+N", "Q1/Q4", "V", "M", "", "Eingefügte Seiten an korrekter Position, Markups der Quellseiten optional übernommen"),
    ("Seiten löschen", "Einzelne oder mehrere Seiten löschen", "Seite", "Basics", "Strg+Umschalt+D", "Q1/Q4", "V", "M", "", "Gelöschte Seiten und zugehörige Markups entfernt, Verweise aktualisiert"),
    ("Seiten ersetzen", "Seiten durch Seiten aus anderer Datei ersetzen, Markups optional erhalten", "Seite", "Basics", "Strg+Umschalt+Y", "Q4", "V", "M", "", "Markups bleiben positionsgenau erhalten, wenn Option gewählt"),
    ("Seiten extrahieren", "Seiten als neue Datei extrahieren", "Seite", "Basics", "Strg+Umschalt+X", "Q1/Q4", "V", "M", "", "Extrahierte Datei enthält genau die gewählten Seiten"),
    ("Seiten verschieben", "Reihenfolge per Drag & Drop in Miniaturen ändern", "Seite", "Basics", "", "Q1", "V", "M", "PDF-DOC-012", "Neue Reihenfolge nach Speichern und Neuladen erhalten"),
    ("Seitenminiaturen", "Miniaturansicht aller Seiten als Panel", "Ansicht", "Basics", "Alt+T", "Q4", "V", "M", "", "Miniaturen für 500-Seiten-Dokument in unter 5 s sichtbar (progressiv)"),
    ("Seiten drehen", "Seiten dauerhaft in 90°-Schritten drehen, einzeln und stapelweise", "Seite", "Basics", "Strg+Umschalt+R", "Q1/Q4/Q9", "V", "M", "", "Drehung im Seitenobjekt gespeichert; Messungen bleiben korrekt"),
    ("Ansicht drehen", "Nur Ansicht drehen, ohne Dokumentänderung", "Ansicht", "Basics", "Strg+Umschalt+Plus/Minus", "Q4", "V", "H", "", "Datei bleibt unverändert"),
    ("Seiten zuschneiden", "Crop-Box setzen, einzeln und stapelweise", "Seite", "Basics", "Umschalt+Alt+O", "Q4/Q9", "V", "M", "", "Crop-Box korrekt gesetzt, Inhalt außerhalb nicht gelöscht"),
    ("Seitengröße ändern", "Seiten auf Format skalieren oder Arbeitsfläche erweitern (A0 bis A4, benutzerdefiniert)", "Seite", "Basics", "", "Q1/Q9", "V", "M", "", "Ergebnisformat entspricht exakt der Vorgabe in mm"),
    ("Seitenlabels", "Seitenbeschriftungen anlegen und bearbeiten, auch automatisch aus Inhalt (Plannummer)", "Seite", "Basics/Core", "", "Q1", "V", "M", "PDF-OCR-001", "Labels in Miniaturen, Lesezeichen und Markup-Liste sichtbar"),
    ("Lesezeichen", "Lesezeichen anlegen, bearbeiten, verschachteln; Inhaltsverzeichnis aus Lesezeichen erzeugen", "Navigation", "Basics", "Strg+B / Alt+B", "Q1/Q4", "V", "M", "", "Lesezeichen sind in Fremdviewern (Acrobat, PDF.js) funktionsfähig"),
    ("Lesezeichen automatisch erzeugen", "Lesezeichen aus Seiteninhalt (Region des Plankopfs) erzeugen", "Navigation", "Core", "", "Q1", "V", "H", "PDF-OCR-001", "Plannummern aus Testplansatz zu mindestens 98 % korrekt erkannt"),
    ("Hyperlinks", "Links auf Seiten, Dateien, URLs, Ansichten anlegen und bearbeiten; Links beim Bearbeiten erhalten", "Navigation", "Basics", "Umschalt+H / Alt+N", "Q1/Q4", "V", "M", "", "Links funktionieren nach Zusammenführen, Aufteilen und Ersetzen"),
    ("Dateianhänge", "Dateien einbetten und extrahieren", "Dokument", "Basics", "F", "Q1/Q4", "V", "H", "", "Eingebettete Datei byte-identisch extrahierbar"),
    ("Dokumenteigenschaften und Metadaten", "Titel, Autor, XMP-Metadaten anzeigen und bearbeiten", "Dialog", "Basics", "Strg+D", "Q4", "V", "M", "", "Metadaten in Fremdviewern sichtbar"),
    ("Kopf- und Fußzeilen", "Kopf- und Fußzeilen mit Variablen (Datum, Seitenzahl, Dateiname) hinzufügen, entfernen, auch stapelweise", "Dokument", "Basics", "", "Q1/Q9", "V", "H", "", "Variablen korrekt aufgelöst"),
    ("PDF-Inhalt bearbeiten", "Inhalt löschen, ausschneiden, Text ändern", "Dokument", "Basics", "", "Q1", "V", "H", "", "Geänderter Text wird mit eingebetteter oder Ersatzschrift korrekt dargestellt"),
    ("Dateigröße reduzieren", "Bilder neu komprimieren, Objekte bereinigen, auch stapelweise", "Dokument", "Basics", "", "Q1/Q9", "V", "H", "", "Dateigröße sinkt, Vektorinhalt bleibt unverändert"),
    ("PDF reparieren", "Beschädigte Strukturen reparieren", "Dokument", "Basics", "", "Q9", "V", "H", "", "Testdateien mit defekter XRef öffnen nach Reparatur fehlerfrei"),
    ("Farbverarbeitung", "Farben in Graustufen/Schwarzweiß umwandeln, Farben ersetzen", "Dokument", "Core", "", "Q1", "V", "N", "", "Farbumwandlung verändert keine Geometrie"),
    ("Entzerren (Deskew)", "Gescannte Seiten gerade ausrichten", "Seite", "Basics", "Strg+Alt+D", "Q4", "V", "H", "PDF-OCR-001", "Schräglage bis 10° wird auf unter 0,2° korrigiert"),
    ("Schnappschuss", "Bereich als Bild/Vektor kopieren, Seite als Schnappschuss", "Bearbeiten", "Basics", "G / Umschalt+G / Strg+Alt+C", "Q4", "V", "H", "", "Eingefügter Schnappschuss maßstabsgleich"),
    ("Dokumentenvergleich", "Siehe Modul Planvergleich (CMP)", "Verweis", "Core", "", "Q1", "V", "M", "CMP-001", "Siehe CMP"),
    ("Druck", "Drucken mit Kachelung, Skalierung, Druckvorschau, Großformat bis A0", "Ausgabe", "Basics", "Strg+P", "Q4", "V", "M", "", "Ausdruck auf Plotter maßstabsgetreu (Prüfmaß 1 m ± 1 mm bei 1:100)"),
    ("Stapeldruck", "Mehrere PDFs mit gleichen Einstellungen drucken", "Ausgabe", "Basics", "", "Q9", "V", "H", "PDF-DOC-031", "Alle Dateien gedruckt, Protokoll erzeugt"),
    ("Als komprimiertes PDF veröffentlichen", "Ausgabe als komprimiertes PDF", "Ausgabe", "Basics", "Strg+Umschalt+P", "Q4", "V", "N", "", "Ausgabe kleiner, Darstellung identisch"),
    ("E-Mail-Versand", "Dokument per E-Mail senden", "Ausgabe", "Basics", "Strg+E", "Q4", "V", "N", "", "Standard-Mailclient öffnet mit Anhang"),
    ("Seite einrichten (stapelweise)", "Zuschnitt und Seiteneinrichtung für mehrere Dateien", "Stapel", "Basics", "", "Q9", "V", "H", "PDF-DOC-015", "Ergebnis identisch zu Einzeloperation"),
    ("PDF/A", "PDF/A-1b/2b/3b erzeugen und prüfen", "Standard", "", "", "MP", "U", "H", "", "veraPDF-Validierung bestanden. Referenzumfang in Bluebeam nicht eindeutig dokumentiert"),
])

mod("PDF-VIEW", "PDF-Engine", "Ansicht und Arbeitsbereich", 4, [
    ("Registerkarten", "Mehrere Dokumente in Registerkarten, Wechsel per Tastatur", "Ansicht", "Basics", "Strg+Tab / Strg+Umschalt+Tab", "Q4", "V", "M", "", "20 Dokumente gleichzeitig offen ohne Leistungseinbruch"),
    ("Geteilte Ansicht", "Horizontal/vertikal teilen, umschalten, aufheben", "Ansicht", "Basics", "Strg+H / Strg+2 / Strg+I / Strg+Umschalt+2", "Q4", "V", "M", "", "Zwei Dokumente unabhängig navigierbar"),
    ("Synchronisierte Ansicht", "Zoom und Position zweier Ansichten koppeln", "Ansicht", "Basics", "", "MP", "U", "M", "PDF-VIEW-002", "Koppelung pixelgenau bei identischem Format. In der Referenz vorhanden, aber in den gesichteten Quellen nicht beschrieben"),
    ("Seitenmodi", "Einzelseite, fortlaufend, nebeneinander, fortlaufend nebeneinander", "Ansicht", "Basics", "Strg+4/5/6/7", "Q4", "V", "M", "", "Alle Modi korrekt"),
    ("Zoom", "Seite einpassen, Breite einpassen, tatsächliche Größe, Zoomwerkzeug, stufenlos bis mindestens 6400 %", "Ansicht", "Basics", "Strg+9 / Strg+0 / Strg+8 / Z / Plus / Minus", "Q4", "V", "M", "", "Bei 6400 % keine Artefakte bei Vektorinhalt"),
    ("Schwenken", "Schwenkwerkzeug, mittlere Maustaste", "Ansicht", "Basics", "Umschalt+V", "Q4", "V", "M", "", "Flüssig mit mindestens 60 fps bei A0-Plan"),
    ("Navigation vor/zurück", "Vorherige/nächste Ansicht und Seite, erste/letzte Seite", "Navigation", "Basics", "Alt+Links/Rechts, Strg+Links/Rechts, Pos1, Ende", "Q4", "V", "M", "", "Ansichtshistorie dokumentübergreifend"),
    ("Vollbild und Präsentation", "Vollbild, Präsentationsmodus", "Ansicht", "Basics", "F11 / Strg+Eingabe", "Q4", "V", "H", "", "Funktioniert auf jedem Monitor"),
    ("Mehrmonitorbetrieb", "Dokumente und Panels in eigene Fenster auf weitere Monitore lösen", "Ansicht", "Basics", "", "MP", "U", "M", "", "Layout wird pro Monitor-Konfiguration gespeichert. In gesichteten Quellen nicht beschrieben"),
    ("Lineale und Raster", "Lineale, Raster anzeigen", "Ansicht", "Basics", "Strg+R / Umschalt+F9", "Q4", "V", "H", "", "Lineal zeigt kalibrierte Einheiten"),
    ("Dimmer", "Planinhalt abdunkeln, um Markups hervorzuheben", "Ansicht", "Basics", "Strg+F5", "Q4", "V", "N", "", "Nur Darstellung, keine Dateiänderung"),
    ("Panels", "Andockbare Panels: Lesezeichen, Miniaturen, Eigenschaften, Ebenen, Bereiche, Messungen, Markups, Werkzeugkasten, Signaturen, Suche, Sets, Links, Formulare, 3D-Modellbaum", "Arbeitsbereich", "Basics", "Alt+B/T/P/Y/S/U/L/X/4/1/2/N/Q/3", "Q4", "V", "M", "", "Jedes Panel an- und abdockbar, Zustand persistent"),
    ("Profile / Arbeitsbereiche", "Speicherbare Arbeitsbereichs-Layouts, Import/Export", "Arbeitsbereich", "Basics", "", "MP", "U", "H", "PDF-VIEW-012", "Layout auf anderem Rechner identisch wiederherstellbar. Bluebeam-Profile in gesichteten Quellen nur indirekt erwähnt (Q5)"),
    ("Ebenen (OCG)", "PDF-Ebenen anzeigen, ein-/ausblenden, Markups Ebenen zuweisen", "Ansicht", "Basics", "Alt+Y", "Q1/Q4", "V", "M", "", "Ebenenzustand wird gespeichert und in Fremdviewern respektiert"),
    ("Textsuche", "Volltextsuche im Dokument und dateiübergreifend", "Suche", "Basics", "Strg+F / F3 / Umschalt+F3", "Q4", "V", "M", "PDF-OCR-001", "Suche über 500 Seiten in unter 3 s"),
    ("Symbolsuche (VisualSearch)", "Grafisch ähnliche Symbole im Plan finden und markieren", "Suche", "Core", "", "Q1", "V", "H", "", "Trefferquote auf Testplänen mindestens 95 % bei unter 2 % Fehltreffern"),
    ("Sets", "Mehrere PDFs als virtuelles Set öffnen, erstellen, ändern, Einstellungen", "Dokumentverwaltung", "Core", "Alt+2", "Q1", "V", "H", "", "Set mit 300 Plänen navigierbar wie ein Dokument"),
    ("Datei-Tags", "Dateien mit Tags versehen und sortieren", "Dokumentverwaltung", "Core", "", "Q1", "V", "H", "", "Tags filterbar und persistent"),
    ("Fenster immer im Vordergrund, Menüleiste, Statusleiste", "Fenstersteuerung", "Ansicht", "Basics", "Strg+F12 / F9 / F8", "Q4", "V", "N", "", "Einstellung persistent"),
    ("Kontextmenüs", "Kontextmenü für alle Objekte", "UI", "Basics", "Umschalt+F10", "Q4", "V", "M", "", "Jede Aktion des Kontextmenüs ist auch per Menü erreichbar"),
    ("Tastaturkürzel anpassen", "Tastenbelegung frei konfigurierbar, Import/Export", "UI", "Basics", "", "Q4", "V", "H", "", "Belegungskonflikte werden angezeigt"),
    ("Hardwarebeschleunigung", "GPU-gestütztes Rendering mit Kachel-Cache", "Technik", "", "", "MP", "E", "M", "", "A0-Vektorplan mit 1 Mio. Pfaden: erster Bildaufbau unter 2 s auf Referenzhardware"),
])

mod("PDF-SEC", "PDF-Engine", "Sicherheit, Signaturen, Schwärzung", 4, [
    ("Passwortschutz", "Öffnen- und Rechtepasswort, AES-256", "Sicherheit", "Basics", "Strg+L", "Q1/Q4", "V", "M", "", "Geschützte Datei in Acrobat korrekt geschützt"),
    ("Berechtigungen", "Drucken, Ändern, Kopieren, Kommentieren einschränken", "Sicherheit", "Basics", "Strg+L", "Q1", "V", "M", "PDF-SEC-001", "Rechte in Fremdviewern wirksam"),
    ("Stapel-Sicherheit", "Sicherheit auf mehrere Dateien anwenden", "Stapel", "Basics", "", "Q9", "V", "H", "PDF-SEC-001", "Protokoll pro Datei"),
    ("Dauerhafte Schwärzung", "Text und Bereiche zur Schwärzung markieren und unwiderruflich entfernen (inkl. Text, Bild, Vektor, Metadaten)", "Sicherheit", "Basics", "Umschalt+R / Umschalt+K / Umschalt+A", "Q1/Q4", "V", "M", "", "Geschwärzter Inhalt ist mit Textextraktion und Hex-Analyse nicht mehr auffindbar"),
    ("Stapel-Schwärzung", "Schwärzung nach Suchbegriff/Muster über mehrere Dateien", "Stapel", "", "", "MP", "U", "H", "PDF-SEC-004", "Alle Treffer geschwärzt, Protokoll. Stapel-Schwärzung in gesichteten Quellen nicht belegt"),
    ("Signaturfelder", "Signaturfelder hinzufügen", "Signatur", "Basics", "X", "Q1/Q4", "V", "M", "", "Feld in Acrobat signierbar"),
    ("Digitale Signatur", "PAdES-konforme Signatur mit digitaler ID (PFX, Windows-Zertifikatsspeicher, Smartcard); Zeitstempel", "Signatur", "Basics", "", "Q1", "V", "M", "PDF-SEC-006", "Signatur von Acrobat und EU-DSS-Validator als gültig erkannt"),
    ("Zertifizierung und Siegel", "Dokument zertifizieren, Siegel/Stempel anbringen", "Signatur", "Basics", "", "Q9", "V", "H", "PDF-SEC-007", "Zertifizierungsstufe korrekt"),
    ("Signaturen nachverfolgen", "Panel mit allen Signaturen und Gültigkeit", "Signatur", "Basics", "Alt+4", "Q1/Q4", "V", "M", "PDF-SEC-007", "Ungültige Signaturen nach Änderung erkannt"),
    ("Stapelsignatur und -siegel", "Signieren und Siegeln mehrerer PDFs", "Stapel", "Complete", "", "Q1/Q9", "V", "H", "PDF-SEC-007", "Alle Dateien gültig signiert"),
    ("Qualifizierte elektronische Signatur (eIDAS)", "Anbindung an QES-Dienste (z. B. A-Trust/Handy-Signatur bzw. ID Austria, D-Trust)", "Signatur", "", "", "MP", "E", "N", "PDF-SEC-007", "Für AT/DE-Markt relevant; Anbieter und rechtliche Anforderungen noch festzulegen"),
])

mod("PDF-OCR", "PDF-Engine", "OCR und Konvertierung", 4, [
    ("OCR", "Gescannte Seiten durchsuchbar machen (Deutsch, Englisch, weitere Sprachen), auch stapelweise", "Konvertierung", "Basics", "Strg+Umschalt+O", "Q1/Q4/Q9", "V", "M", "", "Zeichenfehlerrate unter 2 % auf deutschem Testkorpus (300 dpi)"),
    ("Export nach Office", "Gescannte PDFs nach Word/Excel/PowerPoint exportieren", "Export", "Basics", "", "Q1", "V", "N", "PDF-OCR-001", "Tabellen in Excel strukturiert"),
    ("Office nach PDF", "PDF aus Word/Excel/PowerPoint per Klick und stapelweise", "Import", "Basics", "", "Q1", "V", "H", "", "Lesezeichen aus Überschriften übernommen"),
    ("PDF-Drucker", "Virtueller Windows-Drucker zur PDF-Erzeugung aus beliebigen Programmen", "Plug-in", "Basics", "", "Q1", "V", "H", "", "Ausgabe aus Testprogrammen vektorbasiert"),
    ("CAD nach PDF (2D)", "2D-PDF aus AutoCAD/Revit per Klick und Stapel (über Plug-ins)", "Plug-in", "Core", "", "Q1", "V", "N", "", "Abhängig von Herstellerlizenzen; Umfang zu klären"),
    ("3D-PDF aus IFC/U3D", "3D-PDF aus IFC- oder U3D-Dateien erzeugen", "Konvertierung", "Core", "", "Q1", "V", "N", "BIM-CORE-001", "3D-PDF in Acrobat darstellbar"),
    ("AutoCAD-Hyperlinks, Plansätze, SHX-Schriften", "Hyperlinks aus DWG übertragen, Sheet Sets importieren, SHX-Text durchsuchbar machen", "Konvertierung", "Core", "", "Q1", "V", "N", "", "SHX-Text als Text auffindbar"),
    ("Revit-Räume in Bereiche", "Revit-Räume beim Export in Bereiche (Spaces) umwandeln", "Konvertierung", "Core", "", "Q1", "V", "N", "MKP-SPC-001", "Bereiche mit Raumnamen vorhanden"),
    ("Bildformate", "Import/Export PNG, JPEG, TIFF", "Import/Export", "Basics", "I / Umschalt+I", "Q4/MP", "V", "H", "", "Auflösung frei wählbar"),
])

mod("PDF-FORM", "PDF-Engine", "Formulare", 4, [
    ("Formularfelder erstellen", "Textfeld, Kontrollkästchen, Optionsfeld, Liste, Schaltfläche, Signaturfeld; Berechnungsfelder", "Formular", "Basics", "Strg+Umschalt+F / Alt+Q", "Q1/Q4", "V", "H", "", "Felder in Acrobat ausfüllbar, Berechnungen korrekt"),
    ("Formulare automatisch erzeugen", "Felder automatisch aus PDF erkennen, auch aus Scans", "Formular", "Basics", "", "Q1", "V", "N", "PDF-OCR-001", "Erkennungsquote mindestens 90 % auf Testformularen"),
    ("Formulardaten import/export", "FDF/XFDF/XML/CSV", "Import/Export", "", "", "MP", "U", "N", "PDF-FORM-001", "Rundlauf ohne Datenverlust. Referenzumfang nicht dokumentiert"),
    ("JavaScript-Konsole", "Skriptausführung für Formulare", "Formular", "Basics", "Alt+J", "Q4", "V", "N", "", "Sicherheitsrichtlinie: nur signierte/erlaubte Skripte"),
])

mod("MKP-TOOL", "Markups", "Markup-Werkzeuge", 4, [
    ("Linie", "Gerade Linie mit Endsymbolen", "Werkzeug", "Basics", "L", "Q4", "V", "M", "", "Linie speichert als PDF-Annotation /Line und ist in Acrobat sichtbar"),
    ("Pfeil", "Linie mit Pfeilspitze", "Werkzeug", "Basics", "A", "Q4", "V", "M", "", "wie MKP-TOOL-001"),
    ("Polylinie", "Mehrsegmentlinie", "Werkzeug", "Basics", "Umschalt+N", "Q4", "V", "M", "", "Punkte nachträglich editierbar"),
    ("Bogen", "Kreisbogen", "Werkzeug", "Basics", "Umschalt+C", "Q4", "V", "H", "", "Radius und Winkel editierbar"),
    ("Rechteck", "Rechteck, mit Umschalt Quadrat", "Werkzeug", "Basics", "R", "Q4/Q6", "V", "M", "", ""),
    ("Ellipse und Kreis", "Ellipse, mit Umschalt Kreis", "Werkzeug", "Basics", "E", "Q4", "V", "M", "", ""),
    ("Polygon", "Geschlossenes Polygon", "Werkzeug", "Basics", "Umschalt+P", "Q4", "V", "M", "", ""),
    ("Freihand (Stift)", "Freihandzeichnung, Stifteingabe", "Werkzeug", "Basics", "P", "Q4", "V", "M", "", "Stifteingabe mit Druckstufen auf Windows Ink"),
    ("Radierer", "Teile von Freihandzeichnungen radieren", "Werkzeug", "Basics", "Umschalt+E", "Q4", "V", "H", "MKP-TOOL-008", ""),
    ("Kurven", "Segmente in Bögen umwandeln, automatische Kurven", "Werkzeug", "Basics", "", "Q6", "V", "H", "", "Kontrollpunkte editierbar"),
    ("Revisionswolke", "Wolke um Bereich", "Werkzeug", "Basics", "C", "Q4", "V", "M", "", "Bogengröße einstellbar"),
    ("Wolke mit Legende (Cloud+)", "Wolke mit verbundenem Sprechblasentext", "Werkzeug", "Basics", "K", "Q4", "V", "M", "", ""),
    ("Textfeld", "Textfeld, automatische Größe", "Werkzeug", "Basics", "T / Alt+Z", "Q4", "V", "M", "", "Rich Text, Umlaute, Schriftarten eingebettet oder Fallback"),
    ("Schreibmaschine", "Text ohne Rahmen direkt auf Plan", "Werkzeug", "Basics", "W", "Q4", "V", "M", "", ""),
    ("Sprechblase (Callout)", "Text mit Führungslinie", "Werkzeug", "Basics", "Q", "Q4", "V", "M", "", ""),
    ("Notiz", "Notizsymbol mit Popup", "Werkzeug", "Basics", "N", "Q4", "V", "M", "", ""),
    ("Bemaßung", "Bemaßungslinie (nicht messend, beschriftend)", "Werkzeug", "Basics", "Umschalt+L", "Q4", "V", "H", "", ""),
    ("Hervorheben", "Text oder Bereich hervorheben", "Werkzeug", "Basics", "H", "Q4", "V", "M", "", ""),
    ("Unterstreichen", "Text unterstreichen", "Werkzeug", "Basics", "U", "Q4", "V", "M", "", ""),
    ("Durchstreichen", "Text durchstreichen", "Werkzeug", "Basics", "D", "Q4", "V", "M", "", ""),
    ("Wellenlinie", "Text wellig unterstreichen", "Werkzeug", "Basics", "Umschalt+U", "Q4", "V", "N", "", ""),
    ("Text überprüfen", "Textänderung vorschlagen (Einfügen/Ersetzen)", "Werkzeug", "Basics", "Umschalt+Alt+R", "Q4", "V", "N", "", ""),
    ("Stempel", "Standard-, benutzerdefinierte und dynamische Stempel (Variablen wie Datum, Benutzer); Wasserzeichen", "Werkzeug", "Basics", "S", "Q1/Q4", "V", "M", "", "Dynamische Felder korrekt aufgelöst"),
    ("Stapelstempel", "Stempel auf mehrere Dateien/Seiten anwenden", "Stapel", "Basics", "", "Q1/Q9", "V", "H", "MKP-TOOL-023", ""),
    ("Bild", "Bild als Markup einfügen, auch vom Scanner", "Werkzeug", "Basics", "I / Umschalt+I", "Q1/Q4", "V", "M", "", ""),
    ("Flagge", "Markierungsflagge", "Werkzeug", "Basics", "Umschalt+F", "Q4", "V", "N", "", ""),
    ("Medienerfassung (Capture)", "Foto, Video, Audio, 360°-Foto an Markups anhängen", "Werkzeug", "Core", "Strg+Alt+I", "Q1/Q4/Q7", "V", "H", "", "Basics in Referenz nur Fotos"),
    ("Kontrollkästchen", "Abhakbares Markup bzw. Checkmark-Spalte", "Werkzeug", "Basics", "", "Q7/MP", "V", "M", "", ""),
    ("Symbole", "Symbolbibliothek in Werkzeugsets", "Werkzeug", "Basics", "", "Q1", "V", "M", "TOOL-001", ""),
    ("Hyperlink-Markup", "Hyperlink einfügen und Bereich zuweisen", "Werkzeug", "Basics", "Umschalt+H", "Q1/Q4", "V", "H", "", ""),
    ("Lasso-Auswahl", "Freihand-Auswahl mehrerer Markups", "Auswahl", "Basics", "Umschalt+O", "Q4", "V", "M", "", ""),
    ("Markierungslegende", "Legende, die automatisch Markups auf der Seite zählt und zusammenfasst", "Werkzeug", "Core", "", "Q1", "V", "H", "MKP-LIST-001", "Legende aktualisiert sich bei Änderungen"),
    ("Füllmuster (Schraffuren)", "Benutzerdefinierte Schraffuren erstellen und anwenden", "Eigenschaft", "Core", "", "Q1", "V", "H", "", ""),
    ("Linienstile verwalten", "Linienstile hinzufügen, ändern, importieren, exportieren", "Eigenschaft", "Core", "", "Q1", "V", "H", "", ""),
    ("Maßstabsgetreue Skizze (Sketch to Scale)", "Kalibrierte Markups mit Längen-/Winkeleingabe zeichnen", "Werkzeug", "Core", "", "Q1", "V", "H", "QTO-SCL-001", "Gezeichnete Länge entspricht Eingabe ± 0,1 mm Papiermaß"),
    ("Markups übersetzen", "Markup-Texte in andere Sprachen übersetzen", "KI/Text", "Basics", "", "Q1", "V", "N", "", "Anbieter und Datenschutz klären"),
    ("Rechtschreibprüfung", "Rechtschreibprüfung für Markup-Texte (Deutsch AT/DE)", "Text", "Basics", "F7", "Q4", "V", "H", "", ""),
    ("Aktionen und Sequenzen", "Aktionen an Markups/Werkzeuge hängen; fortlaufende Nummerierung (Sequenzen)", "Automatisierung", "Basics", "Strg+Umschalt+E", "Q1/Q4", "V", "H", "", "Sequenz zählt korrekt weiter, auch nach Löschen"),
])

mod("MKP-EDIT", "Markups", "Markup-Bearbeitung", 4, [
    ("Auswählen, Mehrfachauswahl", "Klick, Rahmen, Strg-Klick, Alles auswählen", "Bearbeitung", "Basics", "V / Strg+A", "Q4", "V", "M", "", ""),
    ("Verschieben, Skalieren, Drehen", "Direktmanipulation und numerische Eingabe", "Bearbeitung", "Basics", "", "MP", "V", "M", "", ""),
    ("Spiegeln", "Horizontal und vertikal spiegeln", "Bearbeitung", "Basics", "Strg+Alt+H / Strg+Alt+V", "Q4", "V", "M", "", ""),
    ("Kopieren, Ausschneiden, Einfügen, an Position einfügen", "Zwischenablage, auch seiten- und dokumentübergreifend", "Bearbeitung", "Basics", "Strg+C/X/V, Strg+Umschalt+V", "Q4", "V", "M", "", "An Position einfügen ist koordinatengenau"),
    ("Duplizieren", "Markup duplizieren", "Bearbeitung", "Basics", "", "MP", "U", "M", "", "Kürzel in Q4 nicht belegt"),
    ("Gruppieren", "Gruppieren, Gruppierung aufheben, aus Gruppe entfernen", "Bearbeitung", "Basics", "Strg+G / Strg+Umschalt+G / Strg+Umschalt+Alt+G", "Q4", "V", "M", "", ""),
    ("Ausrichten", "Links, rechts, oben, unten, Mitte, Zentrum", "Bearbeitung", "Basics", "Strg+Alt+L/R/T/B/M/E", "Q1/Q4", "V", "M", "", ""),
    ("Verteilen", "Horizontal/vertikal gleichmäßig verteilen", "Bearbeitung", "Basics", "", "Q1", "V", "H", "", ""),
    ("Reihenfolge", "Nach vorne, ganz nach vorne, nach hinten, ganz nach hinten", "Bearbeitung", "Basics", "Strg+J / Strg+Umschalt+J / Strg+[ / Strg+Umschalt+[", "Q4", "V", "M", "", ""),
    ("Eigenschaften übertragen", "Formatpinsel", "Bearbeitung", "Basics", "Strg+Umschalt+C", "Q4", "V", "M", "", ""),
    ("Eigenschaften", "Farbe, Füllung, Deckkraft, Linienart, Linienstärke, Schrift, Schriftgröße", "Eigenschaft", "Basics", "Alt+P", "Q4", "V", "M", "", "Alle Eigenschaften in PDF-Annotation persistiert"),
    ("Sperren", "Markup gegen Änderungen sperren", "Bearbeitung", "Basics", "Strg+Umschalt+L", "Q4/Q7", "V", "M", "", ""),
    ("Rückgängig/Wiederherstellen", "Unbegrenzte Historie pro Dokument", "Bearbeitung", "Basics", "Strg+Z / Strg+Y", "Q4", "V", "M", "", "Mindestens 500 Schritte"),
    ("Markups glätten / zurückwandeln", "Markups in Inhaltsebene einbrennen, auch stapelweise; Rückwandlung", "Bearbeitung", "Basics", "Strg+Umschalt+M / Strg+Umschalt+U", "Q1/Q4/Q9", "V", "H", "", ""),
    ("Markups import/export", "PDF, XML, FDF; eigenes Austauschformat", "Import/Export", "Basics", "Strg+F3 / Strg+F2", "Q1/Q4/Q7", "V", "M", "", "Rundlauf ohne Datenverlust"),
    ("Objektfang", "Fang an Inhalt, Markup, Raster; Endpunkt, Mittelpunkt, Schnittpunkt", "CAD", "Basics", "Strg+Umschalt+F8 / F7 / F9", "Q4/MP", "V", "M", "", "Fangpunkte auf Vektorgeometrie exakt (Abweichung 0)"),
    ("Orthogonal/45°", "Umschalt beschränkt auf 0/45/90°", "CAD", "Basics", "Umschalt", "Q6", "V", "M", "", ""),
    ("Parallel, senkrecht, numerische Abstände und Winkel", "CAD-ähnliche Konstruktionshilfen", "CAD", "", "", "MP", "U", "H", "MKP-EDIT-016", "Referenzumfang in gesichteten Quellen nicht belegt"),
    ("Versatz (Offset)", "Parallele Versatzkopie", "CAD", "Basics", "O", "Q4", "V", "H", "", ""),
    ("Magic Markups: Umwandeln in", "Markup in anderen Typ umwandeln", "CAD", "Max", "", "Q2/Q3", "V", "H", "", "Geometrie bleibt exakt erhalten"),
    ("Magic Markups: Duplizieren als", "Markup als anderen Typ duplizieren", "CAD", "Max", "", "Q2/Q3", "V", "H", "", ""),
    ("Magic Markups: Versatzkopie (gleicher/anderer Typ)", "Offset kleiner/größer, auch als anderer Typ", "CAD", "Max", "", "Q2/Q3", "V", "H", "MKP-EDIT-019", "Versatz exakt im kalibrierten Maß"),
    ("Magic Wand", "Funktion auf Max-Seite nur als Videotitel genannt", "Unbekannt", "Max", "", "Q3", "U", "N", "", "Umfang ungeklärt, nicht spezifizierbar"),
])

mod("MKP-LIST", "Markups", "Markup-Liste", 5, [
    ("Markup-Datenbank", "Zentrale Liste aller Markups mit eindeutiger ID, Dokument, Seite, Autor, Datum, Typ, Status, Verantwortlicher, Priorität, Messwert, Einheit, Kosten", "Datenmodell", "Basics", "Alt+L", "Q1/Q7/MP", "V", "M", "", "Änderung in Liste und Plan sind in unter 100 ms synchron"),
    ("Standardspalten", "Betreff, Seitenlabel, Seitenindex, Sperre, Status, Häkchen, Autor, Datum, Erstellungsdatum, Farbe, X, Y, Zentrum, Breite, Höhe, Kommentare, Wandfläche, Länge, Fläche, Volumen, Anzahl, Messung, Beschriftung, Sequenz, Medien, Tiefe, Gefälle, Ebene, Legende, Bereich, Einheit, 3D-Ansicht", "Datenfeld", "Basics", "", "Q7", "V", "M", "", "Alle Spalten vorhanden und sortierbar"),
    ("Sortieren, Filtern, Gruppieren, Suchen", "Inkl. gespeicherter Filtersätze", "Funktion", "Basics", "", "Q1/Q7", "V", "M", "", "10.000 Markups: Filter unter 200 ms"),
    ("Benutzerdefinierte Spalten", "Text, Zahl, Datum, Auswahlliste, Ja/Nein, Formel", "Datenfeld", "Basics/Core", "", "Q1/Q7", "V", "M", "", "Spaltentypen der Referenz unter custom-columns.html noch im Detail zu prüfen"),
    ("Formelspalten", "Berechnete Spalten, z. B. Menge × Einheitspreis", "Berechnung", "Core", "", "Q1", "V", "M", "MKP-LIST-004", "Ergebnis gegen Referenztabelle geprüft"),
    ("Benutzerdefinierte Status", "Statusmodelle definieren, Status setzen", "Workflow", "Core", "", "Q1/Q7", "V", "M", "", ""),
    ("Mehrfachbearbeitung", "Eigenschaften mehrerer Markups gleichzeitig ändern", "Funktion", "", "", "MP", "U", "M", "", "Referenzumfang nicht belegt"),
    ("Zusammenfassung (Summary)", "Bericht als PDF, CSV, XML, auch dateiübergreifend und mit Filter/Sortierung", "Export", "Basics/Core/Complete", "", "Q1/Q7/Q9", "V", "M", "", "Summen identisch mit Liste"),
    ("Excel-Export", "XLSX-Export der Liste", "Export", "", "", "MP", "E", "M", "", "In Referenz über CSV und Quantity Link; nativer XLSX-Export ist EGLE360-Anforderung"),
    ("Medien-Zusammenfassung", "Capture-Bericht, Medienexport", "Export", "Core", "", "Q7", "V", "N", "MKP-TOOL-027", ""),
    ("Markups ausblenden", "Temporär ausblenden, nicht drucken", "Ansicht", "Basics", "", "Q7", "V", "H", "", ""),
    ("Häkchen-Checkliste", "Checkmark-Spalte als Checkliste", "Workflow", "Basics", "", "Q7", "V", "H", "", ""),
    ("Bereiche (Spaces)", "Räume/Zonen als Bereiche definieren; Markups automatisch Bereichen zuordnen und auswerten", "Struktur", "Core", "Alt+S", "Q1/Q4", "V", "M", "", "Zuordnung geometrisch korrekt bei verschachtelten Bereichen"),
    ("Automatische Zusammenfassungen", "Kennzahlen je Status, Autor, Gewerk", "Auswertung", "", "", "MP", "E", "H", "MKP-LIST-008", ""),
])

mod("QTO", "2D-Mengenermittlung", "Messwerkzeuge", 5, [
    ("Messwerkzeug (kombiniert)", "Mehrere Messungen in Folge, Panel Messungen", "Werkzeug", "Basics", "M / Alt+U", "Q4/Q5/Q6", "V", "M", "", ""),
    ("Länge", "Einzelstrecke", "Messung", "Basics", "Umschalt+Alt+L", "Q4/Q5", "V", "M", "QTO-SCL-001", "Referenzplan: Abweichung zu Sollmaß unter 0,05 % bei Vektorplan"),
    ("Mehrsegmentstrecke (Polylänge)", "Summe mehrerer Segmente", "Messung", "Core", "Umschalt+Alt+Q", "Q4/Q5", "V", "M", "QTO-SCL-001", "wie QTO-002"),
    ("Umfang", "Gesamtlänge offener/geschlossener Linienzug; Rücktaste entfernt Punkt, Eingabe beendet", "Messung", "Core", "Umschalt+Alt+P", "Q4/Q5/Q6", "V", "M", "QTO-SCL-001", ""),
    ("Fläche", "Rechteck oder Polygon, Bogensegmente", "Messung", "Basics", "Umschalt+Alt+A", "Q4/Q5/Q6", "V", "M", "QTO-SCL-001", "Fläche analytisch geprüft (Shoelace + Bogensegmente)"),
    ("Volumen", "Fläche × Tiefe", "Messung", "Core", "Umschalt+Alt+V", "Q4/Q5/Q6", "V", "M", "QTO-005", ""),
    ("Tiefe und Wandfläche", "Tiefe für Umfang/Fläche/Volumen; daraus Wandfläche", "Messung", "Core", "", "Q6/Q7", "V", "M", "QTO-004", "Wandfläche = Umfang × Höhe, geprüft"),
    ("Gefälle / Steigung", "Steigung/Gefälle in Messungen (Spalten Rise/Drop, Slope)", "Messung", "Core", "", "Q7", "V", "H", "", "Berechnungslogik der Referenz im Detail ungeklärt"),
    ("Polygon-Ausschnitt", "Polygonale Fläche aus Fläche/Volumen abziehen", "Messung", "Core", "", "Q5", "V", "M", "QTO-005", ""),
    ("Ellipsen-Ausschnitt", "Elliptische Fläche abziehen", "Messung", "Core", "", "Q5", "V", "M", "QTO-005", ""),
    ("Öffnungsabzüge nach Regelwerk", "Abzugsregeln nach ÖNORM-Werkvertragsnormen bzw. VOB/C (z. B. Übermessungsgrenzen) konfigurierbar", "Regelwerk", "", "", "MP", "E", "H", "QTO-009", "Regeln mit Fachexperten AT/DE abgestimmt und gegen Beispielrechnungen geprüft"),
    ("Anzahl (Stückzählung)", "Nummerierte Zählmarken mit laufender Summe", "Messung", "Core", "Umschalt+Alt+C", "Q4/Q5", "V", "M", "", ""),
    ("Durchmesser", "Kreisdurchmesser", "Messung", "Core", "Umschalt+Alt+D", "Q4/Q5", "V", "H", "", ""),
    ("Radius", "Radius aus Mittelpunkt oder drei Punkten", "Messung", "Core", "Umschalt+Alt+U", "Q4/Q5", "V", "H", "", ""),
    ("Winkel", "Winkel aus drei Punkten", "Messung", "Core", "Umschalt+Alt+G", "Q4/Q5", "V", "H", "", "Genauigkeit 0,01°"),
    ("Dynamische Füllung", "Flächen, Messungen und Bereiche automatisch aus geschlossenen Linien des Inhalts erzeugen", "Messung", "Complete", "J", "Q1/Q4/Q5", "V", "H", "", "Füllung stoppt an Lücken unter definierter Toleranz"),
    ("Live-Vorschau", "Messwerte während des Zeichnens anzeigen", "UI", "Basics", "", "Q6", "V", "M", "", ""),
    ("Alle Messwerte anzeigen", "Umfang, Fläche, Wandfläche, Volumen eines Markups gemeinsam", "UI", "Core", "", "Q6", "V", "H", "", ""),
    ("Summen", "Laufende Summen je Typ, Summen der Auswahl (auch gemischte Typen)", "Auswertung", "Basics", "", "Q6", "V", "M", "", ""),
    ("Benutzerdefinierte Messungen", "Eigene Messgrößen mit Formeln (z. B. Schalungsfläche)", "Messung", "", "", "MP", "E", "H", "MKP-LIST-005", ""),
    ("Höhen, Boden-, Decken-, Raumflächen", "Vorkonfigurierte Messarten auf Basis Fläche/Umfang/Tiefe", "Messung", "", "", "MP", "E", "M", "QTO-005", ""),
])

mod("QTO-SCL", "2D-Mengenermittlung", "Maßstab und Einheiten", 5, [
    ("Maßstab festlegen", "Voreingestellte Maßstäbe (1:1 bis 1:5000)", "Dialog", "Basics", "", "Q1/Q5", "V", "M", "", "Maßstab 1:100 auf Testplan: 1 m = 10 mm Papier"),
    ("Maßstab kalibrieren", "Aus bekannter Referenzstrecke ermitteln", "Dialog", "Basics", "", "Q6", "V", "M", "", "Kalibrierter Maßstab weicht unter 0,01 % von Soll ab"),
    ("Ansichtsfenster (Viewports)", "Unterschiedliche Maßstäbe pro Ansichtsfenster auf einer Seite", "Funktion", "Basics", "", "Q1", "V", "M", "", "Messung wählt automatisch Maßstab des Viewports"),
    ("Maßstab auf Seitenbereich", "Aktuelle Seite, alle Seiten, benutzerdefinierter Bereich", "Funktion", "Core", "", "Q1", "V", "M", "", ""),
    ("Einheiten", "mm, cm, m, m², m³, lfm, Stk; getrennt für Länge, Fläche, Volumen; Standard festlegen", "Einstellung", "Basics", "", "Q6/MP", "V", "M", "", "Nur metrische Einheiten in UI-Standard; imperial optional"),
    ("Werkzeugsets kalibrieren", "Werkzeugsets für PDFs mit verschiedenen Maßstäben", "Funktion", "Basics", "", "Q1", "V", "H", "TOOL-001", ""),
])

mod("QTO-EVAL", "2D-Mengenermittlung", "Auswertungen", 5, [
    ("Mengenlisten und Summen", "Gruppiert nach Gewerk, Bauteil, Raum, Material", "Auswertung", "", "", "MP", "E", "M", "MKP-LIST-001", ""),
    ("Kostenansätze und Einheitspreise", "Einheitspreise je Werkzeug/Markup, Formelspalten", "Berechnung", "Core", "", "Q1/MP", "V", "M", "MKP-LIST-005", ""),
    ("Excel-Mengenlink (Quantity Link)", "Messsummen live in Excel-Zellen verknüpfen", "Integration", "Complete", "", "Q1", "V", "H", "", "Wert in Excel aktualisiert sich nach Änderung im Plan"),
    ("Übernahme in EGLE360 AVA", "Freigegebene Mengen mit LV-Position übertragen, Doppelübernahme verhindert", "Integration", "", "", "MP", "E", "M", "AVA-001", "Idempotenter Transfer; zweite Übertragung erzeugt keine Dublette"),
    ("Mengenfreigabe", "Status Entwurf, geprüft, freigegeben mit Prüfer und Zeitstempel", "Workflow", "", "", "MP", "E", "M", "", ""),
])

mod("TOOL", "Werkzeugbibliotheken", "Werkzeugkasten", 5, [
    ("Werkzeugsets", "Eigene Werkzeuge mit Eigenschaften in Werkzeugsets speichern", "Bibliothek", "Basics", "Alt+X", "Q1/Q4", "V", "M", "", ""),
    ("Projekt- und teamweite Werkzeugsets", "Freigabe über Projektserver, Versionierung", "Bibliothek", "", "", "MP", "E", "H", "COL-001", ""),
    ("Import/Export Werkzeugsets", "Eigenes Format; Import aus Bluebeam-BTX nur wenn rechtlich und technisch zulässig", "Import/Export", "", "", "MP", "U", "H", "", "BTX-Format nicht öffentlich dokumentiert; Import vorerst ungeklärt"),
    ("Favoriten und Suche", "Werkzeuge suchen, favorisieren", "UI", "", "", "MP", "U", "H", "", ""),
    ("Gewerkspezifische Vorlagen", "Vorlagen Hochbau, Tiefbau, Landschaftsbau, Elektro, Heizung, Lüftung, Sanitär, Tragwerk, Brandschutz, Innenausbau", "Inhalt", "", "", "MP", "E", "H", "TOOL-001", "Je Gewerk mindestens 20 Werkzeuge mit LV-Bezug, fachlich abgenommen"),
    ("Legenden in Werkzeugsets", "Markierungslegenden speichern", "Bibliothek", "Core", "", "Q1", "V", "H", "MKP-TOOL-032", ""),
])

mod("PLAN", "Planverwaltung", "Pläne und Revisionen", 5, [
    ("Planliste", "Plannummer, Bezeichnung, Index, Datum, Verfasser, Gewerk, Geschoss, Gebäude, Bauabschnitt, Status, Freigabe", "Datenmodell", "", "", "MP", "E", "M", "", ""),
    ("Automatische Plannummernerkennung", "Plannummer und Index aus Plankopf (Region, OCR, Muster)", "Automatisierung", "Core", "", "Q1/MP", "V", "M", "PDF-OCR-001", "98 % korrekt auf Testplansatz"),
    ("Planaustausch (Slip Sheet)", "Neue Revisionen einsortieren, Markups übernehmen, stapelweise", "Revision", "Complete", "", "Q1/Q9", "V", "M", "PLAN-002", "Markups nach Austausch positionsgenau"),
    ("Versionierung und Historie", "Alle Indizes archiviert, wiederherstellbar", "Revision", "", "", "MP", "E", "M", "", ""),
    ("Stapel-Links (Batch Link)", "Automatische Hyperlinks zwischen Plänen anhand von Plannummern-Verweisen", "Automatisierung", "Complete", "", "Q1/Q9", "V", "H", "PLAN-002", "Verweise im Testplansatz zu 95 % verlinkt"),
    ("Änderungsberichte", "Bericht je Revision", "Bericht", "", "", "MP", "E", "H", "CMP-001", ""),
])

mod("CMP", "Planvergleich", "Vergleich und Overlay", 6, [
    ("Dokumentenvergleich", "Unterschiede zweier Revisionen erkennen und als Wolken in Markup-Liste ablegen, auch stapelweise", "Vergleich", "Core", "", "Q1/Q9", "V", "M", "", "Alle Änderungen eines Testpaars erkannt, Fehlalarme unter 5 %"),
    ("Überlagerung (Overlay)", "Farbige Überlagerung mit Ebenen je Revision, Transparenz", "Vergleich", "Core", "", "Q1/Q9", "V", "M", "", ""),
    ("Ausrichtung", "Manuelle Ausrichtung über Referenzpunkte, Maßstabsabgleich", "Vergleich", "Core", "", "MP", "U", "M", "", "Details der Referenz nicht in gesichteten Quellen"),
    ("Automatische Ausrichtung beim Vergleich", "Plansätze automatisch ausrichten", "Vergleich", "Max", "", "Q1/Q2", "V", "H", "CMP-003", ""),
    ("Vergleich Dateien/Sets/Ordner", "Massenvergleich ganzer Plansätze", "Vergleich", "Max", "", "Q1/Q2", "V", "H", "PLAN-002", "Blattzuordnung über Plannummer"),
    ("Unterschiede nach Gewerk und Blatt", "Farbcodierte Ergebnisübersicht", "Bericht", "Max", "", "Q1/Q2", "V", "H", "CMP-005", ""),
    ("KI-Planvergleich (vergleichbar Smart Overlay)", "Erkennt geänderte Bauteile, Beschriftungen, Symbole, Maße, Raum-/Tür-/Fensterkennzeichnungen; unsichere Ergebnisse markiert", "KI", "Max (Preview)", "", "Q3/Q8/MP", "V", "H", "AI-001", "Referenz: nur innerhalb gleicher Disziplin, ohne Ausrichtung, Ausgabe gerastert. EGLE360 soll vektorbasiert mit Ausrichtung arbeiten"),
    ("Vergleichsergebnisse exportieren", "PDF, XLSX, Aufgaben", "Export", "", "", "MP", "E", "H", "CMP-001", ""),
])

mod("STITCH", "Planvergleich", "Plan-Stitching", 6, [
    ("Stitching", "Mehrere Blätter zu durchgehender, maßstäblicher Ansicht zusammenfügen", "Funktion", "Max", "", "Q1/Q2/Q3", "V", "H", "QTO-SCL-001", "Übergang zwischen Blättern unter 1 mm Plan-Versatz"),
    ("Bezugspunkte und Überlappung erkennen", "Passlinien, Achsen, Überlappungen automatisch", "KI", "Max", "", "Q3/MP", "S", "H", "STITCH-001", "Verfahren der Referenz nicht dokumentiert"),
    ("Manuelle Korrektur und Herkunft", "Teilpläne manuell justieren, Herkunft dokumentiert", "Funktion", "", "", "MP", "E", "H", "STITCH-001", ""),
    ("Gesamtplan exportieren", "Als PDF mit Messfähigkeit", "Export", "", "", "MP", "E", "H", "STITCH-001", ""),
])

mod("AI-REV", "KI", "KI-Planprüfung", 10, [
    ("Dokumentprüfung", "Leere Blätter, fehlende Blätter, Blatt fehlt im Index, doppelte Plannummern, fehlende referenzierte Blätter", "KI-Prüfung", "Max (Preview)", "", "Q1/Q8", "V", "H", "PLAN-002", "Testplansatz mit eingebauten Fehlern: Recall 95 %"),
    ("Achsenkoordination", "Widersprüchliche Achsabstände zwischen Tragwerk und Architektur", "KI-Prüfung", "Max (Preview)", "", "Q8", "V", "H", "", ""),
    ("Türkennzeichnungen", "Tür nicht in Liste, nicht im Plan, nicht eindeutig, Türliste fehlt", "KI-Prüfung", "Max (Preview)", "", "Q8", "V", "H", "", ""),
    ("Sanitärkennzeichnungen", "Objekte der Liste fehlen im Plan", "KI-Prüfung", "Max (Preview)", "", "Q8", "V", "N", "", ""),
    ("Fensterkennzeichnungen, Raumbezeichnungen, Geschossbezeichnungen, gewerkübergreifende Inkonsistenzen", "Erweiterte Prüfregeln", "KI-Prüfung", "", "", "MP", "E", "H", "AI-REV-001", ""),
    ("Metrische Pläne und DACH-Konventionen", "Prüfungen für metrische Pläne nach ÖNORM/DIN-Konventionen", "KI-Prüfung", "", "", "MP", "E", "M", "", "Referenz unterstützt laut Q8 nur imperiale Maße; für AT/DE zwingend"),
    ("Ergebnisse", "Prüfmarkierungen mit Fundstelle, Fehlerlisten, Berichte, Status, Verantwortliche, Freigabe", "Ausgabe", "Max (Preview)", "", "Q3/Q8/MP", "V", "H", "MKP-LIST-001", "Jeder Befund verweist auf Seite und Koordinate"),
])

mod("AI", "KI", "EGLE360 AI Assistent", 10, [
    ("MCP-Schnittstelle", "EGLE360 PLAN als MCP-Server: Informationen finden, Markups anlegen/ändern, Spalten anlegen, Metadaten auswerten", "Integration", "Max", "", "Q1/Q2/Q3", "V", "H", "API-001", "Schreibende Aktionen erst nach Benutzerfreigabe"),
    ("Natürliche Sprache, Quellenangabe", "Fragen zu Plänen, Modellen, Mengen, Mängeln; jede Antwort nennt Datenquelle", "KI", "", "", "MP", "E", "H", "AI-001", ""),
    ("Symbol- und Bauteilerkennung", "Türen, Fenster, Sanitärobjekte, Elektrosymbole im PDF", "KI", "", "", "MP", "E", "H", "", "mAP-Zielwert je Klasse festlegen"),
    ("Skriptbefehle", "Skripte über mehrere PDFs ausführen", "Automatisierung", "Max", "", "Q1/Q9", "V", "H", "AUTO-001", ""),
])

mod("AUTO", "Automatisierung", "Stapelverarbeitung und Workflows", 6, [
    ("Stapelverarbeitung allgemein", "Warteschlange, Fortschritt, Protokoll, Abbruch, Wiederaufnahme", "Infrastruktur", "", "", "MP", "E", "M", "", "Jeder Lauf ist protokolliert und wiederholbar"),
    ("Stapelnummerierung", "Bates-/Plannummerierung", "Stapel", "", "", "MP", "U", "N", "", "In gesichteten Quellen nicht belegt"),
    ("Dokumentklassifizierung", "Automatische Zuordnung zu Gewerk/Plantyp", "KI", "", "", "MP", "E", "N", "AI-003", ""),
    ("Regelbasierte Workflows", "Wenn-dann-Regeln (z. B. neue Revision: Vergleich, Aufgabe)", "Workflow", "", "", "MP", "E", "N", "", ""),
])

mod("BIM-CORE", "BIM", "3D-Viewer und IFC", 7, [
    ("IFC importieren/öffnen", "IFC2x3, IFC4, IFC4.3 (STEP, ggf. ifcZIP)", "Import", "", "", "MP", "E", "M", "", "buildingSMART-Beispieldateien öffnen; Kompatibilitätsbericht je Version"),
    ("3D-Navigation", "Drehen, schwenken, zoomen, perspektivisch, orthografisch, Standardansichten", "Viewer", "", "", "MP", "E", "M", "BIM-CORE-001", "Modell mit 500.000 Objekten mit 30 fps"),
    ("Schnitte und Schnittbox", "Schnittebenen und Schnittbox", "Viewer", "", "", "MP", "E", "M", "", ""),
    ("Isolieren, ausblenden, transparent, einfärben", "Nach Geschoss, Klasse, Auswahl", "Viewer", "", "", "MP", "E", "M", "", ""),
    ("Föderierte Modelle", "Mehrere Fachmodelle gemeinsam", "Viewer", "", "", "MP", "E", "M", "", "Koordinaten korrekt überlagert"),
    ("Modellbaum und Eigenschaften", "Räumliche Struktur, Psets, Qto, Materialien, Klassifikationen, GlobalId", "Daten", "", "", "MP", "E", "M", "", "Werte identisch zu IfcOpenShell-Referenzauslese"),
    ("3D-PDF markieren (Referenz)", "Bluebeam: 3D-PDFs anzeigen und markieren; kein IFC-Viewer im engeren Sinn", "Viewer", "Core", "Alt+3 / Strg+Alt+3", "Q1/Q4", "V", "N", "", "Für EGLE360 durch nativen IFC-Viewer ersetzt"),
])

mod("BIM-QTO", "BIM", "BIM-Mengenermittlung", 8, [
    ("Mengen aus IFC-Qto", "Im Modell hinterlegte Mengen übernehmen, Herkunft gekennzeichnet", "Mengen", "", "", "MP", "E", "M", "BIM-CORE-006", ""),
    ("Geometrisch berechnete Mengen", "Flächen, Volumen, Längen aus Geometrie neu berechnet", "Mengen", "", "", "MP", "E", "M", "BIM-CORE-001", "Referenzwürfel und Testmodelle analytisch geprüft"),
    ("Manuell korrigierte Mengen", "Korrektur mit Begründung und Protokoll", "Mengen", "", "", "MP", "E", "M", "", "Drei Mengenarten nie vermischt (eigene Spalten, eigene Herkunft)"),
    ("Auswertungen", "Nach Geschoss, Gebäude, Gewerk, Material, Typ, Klassifikation", "Auswertung", "", "", "MP", "E", "M", "", ""),
])

mod("BIM-CLASH", "BIM", "Kollisionsprüfung", 8, [
    ("Geometrische Kollision", "Modell/Gewerk/Bauteil gegeneinander mit Toleranz", "Prüfung", "", "", "MP", "E", "H", "BIM-CORE-005", "Referenzfälle mit bekannten Kollisionen: 100 % gefunden"),
    ("Abstandsprüfung", "Regelbasierte Mindestabstände getrennt von harten Kollisionen", "Prüfung", "", "", "MP", "E", "H", "BIM-CLASH-001", ""),
    ("Gruppieren, priorisieren, zuweisen, Bericht", "Kollisionsmanagement mit Status", "Workflow", "", "", "MP", "E", "H", "ISS-001", ""),
])

mod("BIM-DIFF", "BIM", "Modellvergleich", 8, [
    ("Versionsvergleich", "Neu, entfernt, Geometrie, Position, Eigenschaften, Material, Klassifikation, Mengen über GlobalId", "Vergleich", "", "", "MP", "E", "H", "BIM-CORE-001", ""),
    ("Auswirkung auf Mengen und Kosten", "Delta-Bericht", "Bericht", "", "", "MP", "E", "H", "BIM-5D-001", ""),
])

mod("LINK", "BIM", "2D-/3D-Verknüpfung", 8, [
    ("PDF-Plan mit IFC-Geschoss verknüpfen", "Passpunkte, Maßstab, Geschoss", "Verknüpfung", "", "", "MP", "E", "H", "", ""),
    ("Gegenseitige Auswahl", "Bauteil im PDF wählen, im Modell zeigen und umgekehrt; manuelle Bestätigung bei Mehrdeutigkeit", "Verknüpfung", "", "", "MP", "E", "H", "LINK-001", ""),
    ("Revit-Verbindung (Referenz)", "Session-Markups in Revit anzeigen, Status aus Revit ändern, Indikatoren in 3D-Ansichten, benutzerdefinierte Status", "Integration", "Max", "", "Q1/Q2/Q3", "V", "N", "COL-001", "Erfordert Revit-API-Add-in; Zulässigkeit und Lizenz prüfen"),
])

mod("BIM-4D", "BIM", "4D Terminplanung", 9, [
    ("Vorgänge und Bauteilzuordnung", "Phasen, Vorgänge, Termine, Dauern; Zuordnung zu Bauteilen", "4D", "", "", "MP", "E", "N", "BIM-CORE-001", ""),
    ("Terminplan-Import/Export", "MS Project XML, Primavera XER, CSV", "Import/Export", "", "", "MP", "E", "N", "", ""),
    ("Bauablaufsimulation, Soll-Ist", "Zeitliche Visualisierung aus echten Vorgangsdaten", "4D", "", "", "MP", "E", "N", "BIM-4D-001", ""),
])

mod("BIM-5D", "BIM", "5D Kosten", 9, [
    ("Bauteil zu LV-Position", "Verknüpfung, Einheitspreis, Positions-/Gewerk-/Geschosskosten", "5D", "", "", "MP", "E", "H", "AVA-001", ""),
    ("Kostenstände und Änderungen", "Snapshots, Delta bei Modell- und Mengenänderung, Prognose", "5D", "", "", "MP", "E", "N", "BIM-DIFF-001", ""),
])

mod("ISS", "Zusammenarbeit", "Aufgaben, Mängel, BIM-Issues, BCF", 8, [
    ("Aufgaben und Mängel", "Verantwortliche, Priorität, Frist, Status, Fotos, Anhänge, Kommentare, Wiedervorlage, Berichte, OP-Listen", "Workflow", "", "", "MP", "E", "M", "", ""),
    ("BCF-Import/Export", "BCF 2.1 und 3.0 (Dateiformat), Viewpoints, Snapshots", "Standard", "", "", "MP", "E", "H", "BIM-CORE-001", "Rundlauf mit buildingSMART-Testdateien und gängigen Werkzeugen"),
    ("BCF-API", "BCF-API 3.0 für Server-Server-Austausch", "Standard", "", "", "MP", "E", "N", "ISS-002", ""),
])

mod("COL", "Zusammenarbeit", "EGLE360 COLLAB", 11, [
    ("Echtzeit-Sitzungen", "Gemeinsame PDF-Markierung in Echtzeit, Markup-Benachrichtigungen", "Kollaboration", "Basics", "Alt+C", "Q1/Q4", "V", "H", "", "Zwei Clients sehen Änderungen in unter 1 s"),
    ("Projekte und Dateiablage", "Projektordner, Ein-/Auschecken, Auschecken widerrufen, Serverkopie aktualisieren, lokal speichern", "Kollaboration", "Basics", "Alt+K", "Q1/Q4", "V", "H", "", ""),
    ("Sitzungen/Projekte erstellen und verwalten", "Einstellungen, Rechte", "Kollaboration", "Core", "", "Q1", "V", "H", "SEC-001", ""),
    ("Offline-Synchronisation", "Projektdateien offline bearbeiten und synchronisieren, Konfliktbehandlung", "Kollaboration", "Basics", "", "Q1", "V", "H", "COL-002", "Konflikt nach Netzunterbrechung ohne Datenverlust aufgelöst"),
    ("Web und Mobil", "Dateien hochladen, ansehen, markieren", "Plattform", "Basics", "", "Q1/Q3", "V", "N", "", ""),
    ("SharePoint und ProjectWise", "Dokumentenablagen anbinden", "Integration", "Basics", "", "Q1", "V", "N", "", ""),
    ("Aktivitätsprotokoll und Benachrichtigungen", "Wer hat wann was geändert", "Kollaboration", "Basics", "", "Q1", "V", "H", "", ""),
])

mod("SEC", "Plattform", "Benutzer und Sicherheit", 11, [
    ("Konten, Rollen, Rechte", "Projekt- und Dokumentenrechte, SSO (OIDC), MFA", "Sicherheit", "", "", "MP", "E", "M", "", ""),
    ("Audit-Log", "Unveränderliches Protokoll sicherheitsrelevanter Aktionen", "Sicherheit", "", "", "MP", "E", "M", "", ""),
    ("DSGVO-Funktionen", "Auskunft, Löschung, Auftragsverarbeitung, Datenhaltung in der EU", "Recht", "", "", "MP", "E", "M", "", ""),
    ("Backup und Wiederherstellung", "Point-in-Time-Recovery, getestete Wiederherstellung", "Betrieb", "", "", "MP", "E", "M", "", "Wiederherstellungstest quartalsweise"),
    ("Bluebeam Administrator (Referenz)", "Verwaltungstool für Installationen/Einstellungen", "Plug-in", "Basics", "", "Q1", "V", "N", "", "EGLE360: zentrale Richtlinienverwaltung"),
])

mod("API", "Plattform", "Schnittstellen", 2, [
    ("REST-API", "Versionierte, dokumentierte REST-API (OpenAPI)", "Schnittstelle", "", "", "MP", "E", "M", "", ""),
    ("Datenformate", "PDF, PDF/A, IFC, BCF, XLSX, CSV, JSON, XML, Bildformate", "Schnittstelle", "", "", "MP", "E", "M", "", "Je Format Testdateien im Repository"),
    ("EGLE360 AVA API", "Mengenübergabe, LV-Positionen; Austausch zusätzlich ÖNORM A 2063 bzw. GAEB DA XML", "Schnittstelle", "", "", "MP", "E", "M", "", "Spezifikation der AVA-Schnittstelle liegt noch nicht vor (ungeklärt)"),
    ("Microsoft Office / Excel Plug-ins (Referenz)", "Office-Integration, Excel-Mengenlink", "Plug-in", "Core/Complete", "", "Q1", "V", "N", "QTO-EVAL-003", ""),
    ("Bluebeam Stapler (Referenz)", "Eigenständiges Stapel-Konvertierungstool", "Plug-in", "Basics", "", "Q1", "V", "N", "", ""),
])


# --- Abgleich mit installiertem Revu 21.11 (16.10.2026) ---
# Neue Einträge werden je Modul hinten angehängt, damit bestehende IDs stabil bleiben.
ADDITIONS = {
    "PDF-DOC": [
        ("Lesezeichen-Vorlagen", "Vorlagen für Lesezeichenstrukturen (z. B. Baudokumente) speichern und anwenden", "Navigation", "unbekannt", "", "Q11", "L", "H", "PDF-DOC-019", ""),
    ],
    "PDF-VIEW": [
        ("Zuletzt geschlossene Datei wieder öffnen", "Zuletzt geschlossene Dokumente wiederherstellen", "Dokument", "unbekannt", "Strg+Alt+O", "Q11", "L", "H", "", ""),
        ("Nächste/vorige Datei im Ordner", "Benachbarte Datei im selben Ordner öffnen", "Navigation", "unbekannt", "Alt+Bild ab / Alt+Bild auf", "Q11", "L", "H", "", ""),
        ("Web-Registerkarte", "Webseite als Registerkarte im Programm öffnen", "Ansicht", "Basics", "Strg+T", "Q4/Q11", "V", "N", "", ""),
        ("Zoomfenster / Lupe", "Lupe bzw. Zoomwerkzeug umschalten", "Ansicht", "Basics", "Umschalt+Z", "Q4/Q11", "V", "H", "PDF-VIEW-005", ""),
        ("Ansichten umschalten und ausgleichen", "Aktive Teilansicht wechseln, Teilansichten gleich groß machen", "Ansicht", "Basics", "Strg+1 / Umschalt+F12", "Q4/Q11", "V", "H", "PDF-VIEW-002", ""),
        ("Navigationsleiste und Panels ein-/ausblenden", "Navigationsleiste und Seitenpanels umschalten", "Arbeitsbereich", "Basics", "F4 / Umschalt+F4", "Q4/Q11", "V", "M", "PDF-VIEW-012", "In EGLE360 seit Phase 3: F4 klappt die Hauptnavigation ein und aus"),
        ("Datei-Manager-Panel", "Panel mit Dateizugriff innerhalb des Programms", "Arbeitsbereich", "Basics", "Alt+A", "Q4/Q11", "V", "H", "PDF-VIEW-012", ""),
        ("Startseite", "Startseite mit zuletzt verwendeten Dateien und Projekten", "UI", "unbekannt", "F12", "Q11", "L", "H", "", ""),
        ("Linienstärken ausblenden", "Darstellung aller Linien in Haarlinienstärke", "Ansicht", "unbekannt", "", "Q11", "L", "N", "", ""),
    ],
    "PDF-SEC": [
        ("Vertraulichkeitsbezeichnungen (Microsoft Purview)", "Dokumente mit Vertraulichkeitsbezeichnungen lesen und schützen", "Sicherheit", "unbekannt", "", "Q11", "U", "N", "", "Nur aus Dateinamen von Bibliotheken vermutet; Funktion nicht belegt"),
    ],
    "PDF-OCR": [
        ("Dateieigenschaften aus CAD übernehmen", "Zuordnung von CAD-Dateieigenschaften zu PDF-Metadaten bei der Erstellung", "Konvertierung", "Core", "", "Q11", "L", "N", "PDF-DOC-022", ""),
        ("Druckertreiber-Profile für CAD-Programme", "Voreinstellungen u. a. für Inventor, MicroStation, NX, Pro/ENGINEER, DWF; PDF-Erstellung aus Outlook", "Konvertierung", "Basics/Core", "", "Q11", "L", "N", "PDF-OCR-004", ""),
    ],
    "PDF-FORM": [
        ("Dokumentvorlagen", "Mitgelieferte Vorlagen (z. B. Baustellenbericht, Mängelliste, RFI, Memo, Rechnung), teils als Formular", "Vorlage", "unbekannt", "", "Q11", "L", "H", "PDF-FORM-001", "EGLE360: eigene Vorlagen nach AT/DE-Praxis erstellen"),
    ],
    "MKP-TOOL": [
        ("Flaggen-Panel", "Panel mit allen markierten Flaggen", "Arbeitsbereich", "unbekannt", "", "Q11", "L", "N", "MKP-TOOL-026", ""),
    ],
    "MKP-EDIT": [
        ("Markups neu aufbauen", "Darstellung der Markups aus den gespeicherten Daten neu erzeugen", "Bearbeitung", "unbekannt", "Strg+Alt+A", "Q11", "L", "N", "", ""),
        ("Vervielfältigen", "Markup in Reihe oder Raster mehrfach kopieren", "Bearbeitung", "unbekannt", "Strg+M", "Q11", "L", "H", "", "Klärt teilweise MKP-EDIT-005"),
        ("Gleiche Größe, Breite, Höhe; auf Seite zentrieren", "Mehrere Markups angleichen bzw. auf der Seite zentrieren", "Bearbeitung", "unbekannt", "", "Q11", "L", "H", "MKP-EDIT-007", ""),
        ("Kontrollpunkte bearbeiten", "Kontrollpunkte hinzufügen, entfernen und zwischen Ecke und Bogen umwandeln", "CAD", "unbekannt", "", "Q11", "L", "M", "MKP-TOOL-010", ""),
    ],
    "MKP-LIST": [
        ("Vorheriges/nächstes Markup ansteuern", "Mit Tabulator durch die Markups springen", "Navigation", "unbekannt", "Tab / Umschalt+Tab", "Q11", "L", "H", "", ""),
    ],
    "QTO": [
        ("Magic Fill", "Messfläche durch Klick in eine Fläche automatisch erzeugen (Abgrenzung zu Dynamischer Füllung ungeklärt)", "Messung", "unbekannt", "Umschalt+Alt+M", "Q11", "L", "H", "QTO-016", "Verhältnis zu „Magic Wand“ (MKP-EDIT-023) nicht belegt"),
    ],
    "PLAN": [
        ("Gewerk- und Disziplinkategorien", "Vordefinierte Kategorien mit Kürzeln für Plansätze", "Datenmodell", "unbekannt", "", "Q11", "L", "M", "PLAN-002", "EGLE360: Kategorien nach AT/DE-Gewerken"),
    ],
    "AUTO": [
        ("Profil-Skripte", "Mehrschrittige Abläufe als Skript im Profil (z. B. vereinfachen und per E-Mail senden)", "Automatisierung", "unbekannt", "", "Q11", "L", "N", "AI-004", ""),
    ],
    "COL": [
        ("Studio-Papierkorb", "Gelöschte Projektdateien wiederherstellen", "Kollaboration", "unbekannt", "", "Q11", "L", "H", "COL-002", "Nur über aktives Feature-Flag belegt"),
        ("Revisionsverlauf von Projektdateien", "Frühere Fassungen einer Projektdatei einsehen und wiederherstellen", "Kollaboration", "unbekannt", "", "Q11", "L", "H", "COL-002", "Nur über aktives Feature-Flag belegt"),
        ("Bedrohungserkennung beim Hochladen", "Hochgeladene Dateien auf Schadcode prüfen", "Sicherheit", "unbekannt", "", "Q11", "L", "H", "COL-002", "Nur über aktives Feature-Flag belegt"),
        ("GoCanvas-Anbindung", "Integration mit GoCanvas (mobile Formulare)", "Integration", "unbekannt", "", "Q11", "L", "N", "", "Nur über aktives Feature-Flag belegt; für EGLE360 ohne Priorität"),
        ("Länderspezifische Smart Search", "Suchfunktion mit Länderausprägung (u. a. Deutschland)", "Suche", "unbekannt", "", "Q11", "U", "N", "", "Nur Feature-Flags; Funktion aus den Dateien nicht erkennbar"),
    ],
}

# Änderungen an bestehenden Einträgen: Spalte -> neuer Wert (Spaltennamen wie in HEAD)
PATCHES = {
    "PDF-DOC-004": {"Referenz-Kürzel": "Umschalt+F2", "Quelle": "Q11"},
    "PDF-DOC-024": {"Anforderung": "Inhalt löschen, ausschneiden, radieren, Text ändern"},
    "PDF-DOC-018": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Lesezeichen anzeigen und anspringen; Bearbeiten offen", "Akzeptanzkriterium / Hinweis": "Lesezeichen sind in Fremdviewern (Acrobat, PDF.js) funktionsfähig. Vorlagen siehe PDF-DOC-037"},
    "PDF-OCR-006": {"Akzeptanzkriterium / Hinweis": "3D-PDF in Acrobat darstellbar. In 21.11 sind IFC- und U3D-Bibliotheken mitinstalliert (Q11)"},
    "PDF-VIEW-013": {"Verifizierung": "L", "Quelle": "Q5/Q11", "Akzeptanzkriterium / Hinweis": "Layout auf anderem Rechner identisch wiederherstellbar. Referenz: Profile als XML (*.bpx) mit Spalten, Werkzeugleisten, Panels, Werkzeugsätzen, Status-Modellen, Skripten"},
    "MKP-EDIT-009": {"Referenz-Kürzel": "Strg+] / Strg+Umschalt+] / Strg+[ / Strg+Umschalt+[", "Quelle": "Q11"},
    "MKP-EDIT-016": {"Referenz-Kürzel": "Strg+Umschalt+F8 (Inhalt) / Strg+Umschalt+F7 (Markup) / Strg+Umschalt+F9 (Raster)"},
    "MKP-EDIT-019": {"Referenz-Kürzel": "O laut Online-Liste (Q4), in 21.11 nicht belegt", "Quelle": "Q4/Q11"},
    "MKP-EDIT-005": {"Akzeptanzkriterium / Hinweis": "Teilweise geklärt: kein eigener Befehl in 21.11, dort „Vervielfältigen“ (MKP-EDIT-025, Strg+M)"},
    "MKP-EDIT-023": {"Akzeptanzkriterium / Hinweis": "Teilweise geklärt: in 21.11 nicht vorhanden, dort „Magic Fill“ (QTO-022). Ob identisch, ist nicht belegt"},
    "MKP-TOOL-023": {"Akzeptanzkriterium / Hinweis": "Dynamische Felder korrekt aufgelöst. Referenz liefert 13 deutsche Standardstempel; EGLE360 erstellt eigene"},
    "MKP-TOOL-027": {"Anforderung": "Foto, Video, Audio, 360-Grad-Foto an Markups anhängen; Bild von Kamera", "Quelle": "Q1/Q4/Q7/Q11"},
    "MKP-TOOL-035": {"Anforderung": "Kalibrierte Markups mit Längen-/Winkeleingabe zeichnen, für Linie, Polylinie, Rechteck, Ellipse und Polygon", "Quelle": "Q1/Q11"},
    "MKP-TOOL-037": {"Anforderung": "Rechtschreibprüfung für Markup-Texte (Deutsch AT/DE); Referenz liefert Wörterbücher in 15 Sprachen"},
    "CMP-003": {"Akzeptanzkriterium / Hinweis": "Teilweise geklärt: Bibliothek für automatische Ausrichtung in 21.11 vorhanden (nur Dateiname); manuelle Ausrichtung lokal nicht beschrieben"},
    "CMP-004": {"Akzeptanzkriterium / Hinweis": "Bibliothek „Autoalignment“ in 21.11 vorhanden (nur Dateiname, Vermutung)"},
    "PDF-FORM-003": {"Akzeptanzkriterium / Hinweis": "Rundlauf ohne Datenverlust. Teilweise geklärt: FDF-Dateisymbol in 21.11 deutet auf FDF-Unterstützung (Vermutung); XFDF, XML, CSV nicht belegt"},
    "TOOL-003": {"Akzeptanzkriterium / Hinweis": "Technisch geklärt: BTX ist XML mit zlib-komprimierten, hex-kodierten Feldern und PDF-Annotationswörterbüchern; Import machbar. Rechtliche Zulässigkeit weiterhin offen"},
    "AI-001": {"Akzeptanzkriterium / Hinweis": "Schreibende Aktionen erst nach Benutzerfreigabe. In 21.11 ist ein MCP-Server mitinstalliert (Q11)"},
    "COL-002": {"Akzeptanzkriterium / Hinweis": "Zuordnung von Alt+K zum Projekte-Panel nur vermutet (Q11)"},
    "PDF-DOC-001": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-002": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): leeres Dokument mit wählbarem Format; Erstellung aus Office/CAD offen"},
    "PDF-DOC-003": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-005": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Seiten aus einer anderen PDF einfügen; Mehrfachauswahl von Dateien offen"},
    "PDF-DOC-006": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Seitenbereich in neue Datei extrahieren; Aufteilen nach Lesezeichen oder Seitenzahl offen"},
    "PDF-DOC-007": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-008": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-010": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-011": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Seite vor/zurück per Befehl; Drag & Drop in Miniaturen offen"},
    "PDF-DOC-012": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-013": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-DOC-017": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Seitenlabels werden angezeigt; Bearbeiten offen"},
    "PDF-DOC-022": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Eigenschaften und Metadaten anzeigen; Bearbeiten offen"},
    "PDF-VIEW-001": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-VIEW-005": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-VIEW-006": {"Implementierungsstatus": "Umgesetzt (Phase 4, Teil 1)"},
    "PDF-VIEW-012": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Seitenleiste mit Miniaturen und Lesezeichen"},
    "PDF-VIEW-019": {"Implementierungsstatus": "Teilweise (Phase 4, Teil 1): Statusleiste mit Pfad, Seitenformat und Zoom"},
}

for prefix, bereich, modul, phase, rs in M:
    rs.extend(ADDITIONS.pop(prefix, []))
assert not ADDITIONS, f"Unbekannte Module: {list(ADDITIONS)}"

# --- Erzeugung ---
HEAD = ["ID", "Bereich", "Modul", "Funktion", "Anforderung", "Art", "Referenz-Paket (Bluebeam)", "Referenz-Kürzel", "Quelle", "Verifizierung", "Priorität", "Phase", "Abhängigkeiten", "Akzeptanzkriterium / Hinweis", "Implementierungsstatus"]
rows = []
for prefix, bereich, modul, phase, rs in M:
    for i, r in enumerate(rs, 1):
        f, desc, art, paket, kz, q, v, p, dep, acc = r
        rows.append([f"{prefix}-{i:03d}", bereich, modul, f, desc, art, paket or "nicht in Referenz", kz, q, v, PRIO[p], phase, dep, acc, "Offen"])


# Umsetzungsstand aus Phase 4 (Teil 2) und Phase 5 (Teil 1).
STATUS = {
    "PDF-VIEW-007": "Teilweise (Phase 5, Teil 2): vorherige und nächste Ansicht, Seite vor und zurück; erste und letzte Seite über das Seitenfeld",
    "PDF-DOC-031": "Teilweise (Phase 4, Teil 2): Drucken über den Windows-Druckdialog mit Seitenauswahl, eingepasst",
    "PDF-VIEW-015": "Umgesetzt (Phase 4, Teil 2): Suche über alle Seiten, Trefferliste, Hervorhebung, F3",
    "PDF-VIEW-012": "Teilweise (Phase 4, Teil 2): Seitenleiste mit Miniaturen, Lesezeichen und Suche; Markierungsliste unten",
    "PDF-VIEW-020": "Teilweise (Phase 4, Teil 2): Kontextmenüs für Miniaturen und Markierungen",
    "MKP-TOOL-001": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-002": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-003": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-005": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-006": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-007": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-008": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-011": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-013": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-014": "Umgesetzt (Phase 5, Teil 2): als FreeTextTypeWriter gespeichert",
    "MKP-TOOL-015": "Umgesetzt (Phase 5, Teil 2): Textfeld mit Pfeillinie, als FreeTextCallout gespeichert",
    "MKP-TOOL-016": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-TOOL-017": "Umgesetzt (Phase 5, Teil 2): Maßlinie mit Pfeilen, Zahl über der Linie, nach Seitenmaßstab",
    "MKP-TOOL-023": "Teilweise (Phase 5, Teil 2): sieben eigene Textstempel und eigene Aufschrift, mit Autor und Datum; Bildstempel offen",
    "PLAN-001": "Umgesetzt (Phase 5, Teil 3): Planliste je Projektordner mit Nummer, Bezeichnung, Index, Datum, Verfasser, Gewerk, Geschoss, Gebäude, Bauabschnitt, Status, Freigabe; Excel-Export",
    "PLAN-002": "Teilweise (Phase 5, Teil 3): Muster im Seitentext und im Dateinamen; Plankopf-Region und OCR offen",
    "PLAN-003": "Teilweise (Phase 5, Teil 3): neue Indizes nach Plannummer einsortieren, mehrere Dateien auf einmal, Markierungen und Maßstäbe übernehmen",
    "PLAN-004": "Umgesetzt (Phase 5, Teil 3): jeder Index bleibt als eigene Datei erhalten und lässt sich öffnen",
    "PLAN-007": "Teilweise (Phase 5, Teil 3): Vorschlagsliste mit Gewerkkürzeln, frei änderbar",
    "CMP-001": "Teilweise (Phase 6, Teil 1): Seitenweiser Vergleich zweier Stände, Änderungen als Wolken im neuen Stand; stapelweise offen",
    "CMP-002": "Teilweise (Phase 6): Überlagerung rot (alt), blau (neu), grau (gleich), jede Farbe ein- und ausblendbar; eigene Farben und Transparenz offen",
    "QTO-008": "Umgesetzt (Phase 5): Neigung in Prozent bei Länge, Polylänge und Fläche; geneigte Länge oder Fläche und Höhenunterschied in Liste und Export. Ob die Rechenweise der Referenz entspricht, ist ungeprüft",
    "MKP-TOOL-025": "Umgesetzt (Phase 5): Bild aus Datei oder Zwischenablage, gezeichnetes Unterschriftsbild (keine digitale Signatur), speicherbar in der Werkzeugbibliothek; Scanner offen",
    "AUTO-001": "Teilweise (Phase 6): Stapellauf mit Fortschritt, Protokoll, Abbruch und Fortsetzen über bereits erledigte Kopien; bisher Schritte Stempel und Maßstab",
    "MKP-TOOL-024": "Umgesetzt (Phase 6): Stempel auf erste oder alle Seiten vieler Dateien, Ecke wählbar, als Kopien oder in den Originalen",
    "CMP-003": "Teilweise (Phase 6, Teil 1): Verschiebung wird automatisch erkannt und ausgeglichen; Drehung, Maßstab und manuelle Ausrichtung offen",
    "TOOL-001": "Umgesetzt (Phase 5, Teil 3): Werkzeugsätze in der Seitenleiste, Werkzeug aus Markierung speichern, bleibt beim Zeichnen aktiv",
    "TOOL-003": "Teilweise (Phase 5, Teil 3): Import und Export im eigenen Format e360-tools (JSON); BTX-Import offen",
    "TOOL-004": "Umgesetzt (Phase 5, Teil 3): Suche nach Name, Satz und Art; Favoriten zuerst",
    "TOOL-005": "Teilweise (Phase 5, Teil 3): Grundausstattung Planprüfung und Mengen Hochbau ohne Preise; weitere Gewerke offen",
    "MKP-TOOL-018": "Umgesetzt (Phase 4, Teil 3): folgt den Textzeilen im aufgezogenen Bereich, ohne Text als Rechteck",
    "MKP-TOOL-019": "Umgesetzt (Phase 4, Teil 3): je Textzeile, als PDF-Underline mit QuadPoints",
    "MKP-TOOL-020": "Umgesetzt (Phase 4, Teil 3): je Textzeile, als PDF-StrikeOut mit QuadPoints",
    "MKP-EDIT-001": "Umgesetzt (Phase 4, Teil 2): Klick, Strg+Klick, Strg+A",
    "MKP-EDIT-002": "Umgesetzt (Phase 5, Teil 3): Verschieben, Skalieren, Drehen in 90-Grad-Schritten für alle Arten, beliebige Winkel für Punktformen",
    "MKP-EDIT-004": "Teilweise (Phase 4, Teil 2): Kopieren und Einfügen (Strg+C, Strg+V); Ausschneiden und an Position einfügen offen",
    "MKP-EDIT-005": "Umgesetzt (Phase 4, Teil 2): Strg+D",
    "MKP-EDIT-009": "Teilweise (Phase 4, Teil 2): Vordergrund und Hintergrund über Kontextmenü",
    "MKP-EDIT-007": "Umgesetzt (Phase 5, Teil 2): sechs Ausrichtungen und Verteilen, bezogen auf die angezeigte Seitendrehung",
    "MKP-EDIT-010": "Umgesetzt (Phase 5, Teil 2): Darstellung kopieren und einfügen (Strg+Umschalt+C/V)",
    "MKP-EDIT-011": "Teilweise (Phase 4, Teil 2): Farbe, Linienbreite, Deckkraft, Füllung, Text, Kommentar",
    "MKP-EDIT-012": "Umgesetzt (Phase 5, Teil 2): Sperren und Entsperren, im PDF zusätzlich mit Locked-Flag",
    "MKP-EDIT-013": "Umgesetzt (Phase 4, Teil 2)",
    "MKP-EDIT-016": "Teilweise (Phase 5, Teil 2): Fang an End- und Mittelpunkten des Planinhalts und an Eckpunkten von Markierungen; Schnittpunkte und Raster offen",
    "MKP-EDIT-017": "Umgesetzt (Phase 4, Teil 2): Umschalttaste beim Zeichnen",
    "MKP-EDIT-027": "Umgesetzt (Phase 4, Teil 2): Eckpunkte von Linien, Polygonen und Messungen ziehen",
    "MKP-LIST-001": "Teilweise (Phase 5, Teil 1): Liste aller Markierungen des Dokuments",
    "MKP-LIST-002": "Teilweise (Phase 5, Teil 1): Seite, Art, Wert, Text, Maßstab, Autor, Datum",
    "MKP-LIST-003": "Teilweise (Phase 5, Teil 1): Sortieren und Filtern; Gruppieren offen",
    "MKP-LIST-006": "Teilweise (Phase 5, Teil 2): sechs feste Status mit Person und Zeitpunkt; eigene Statusmodelle offen",
    "MKP-LIST-011": "Teilweise (Phase 5, Teil 2): alle Markierungen ein- und ausblenden; nach Art oder Autor offen",
    "MKP-LIST-008": "Teilweise (Phase 5, Teil 1): Summen je Messart in der Liste",
    "MKP-LIST-009": "Umgesetzt (Phase 5, Teil 2): XLSX mit Summenblatt, alternativ CSV",
    "QTO-002": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-003": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-004": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-005": "Umgesetzt (Phase 5, Teil 1): Fläche mit Umfang",
    "QTO-006": "Umgesetzt (Phase 5, Teil 2): Tiefe je Fläche, Volumen in m³",
    "QTO-007": "Umgesetzt (Phase 5, Teil 2): Höhe je Länge, Polylänge oder Umfang, Wandfläche in m²",
    "QTO-009": "Umgesetzt (Phase 5, Teil 1): Ausschnitte per Rechtsklick auf eine Fläche",
    "QTO-012": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-013": "Umgesetzt (Phase 5, Teil 2): mit Kreisfläche in der Liste",
    "QTO-014": "Umgesetzt (Phase 5, Teil 2): mit Kreisfläche in der Liste",
    "QTO-015": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-017": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-019": "Teilweise (Phase 5, Teil 1): Summen in der Liste und für ausgewählte Messungen",
    "QTO-EVAL-002": "Teilweise (Phase 5, Teil 2): Einheitspreis je Messung, Gesamtpreis und Kostensumme in Liste und Export; Preislisten je Werkzeug offen",
    "QTO-EVAL-005": "Teilweise (Phase 5, Teil 2): Status Geprüft und Freigegeben mit Person und Zeitpunkt, Sperren; eigener Freigabeablauf offen",
    "QTO-SCL-001": "Umgesetzt (Phase 5, Teil 1): übliche Maßstäbe 1:1 bis 1:2000 je Seite",
    "QTO-SCL-002": "Umgesetzt (Phase 5, Teil 1)",
    "QTO-SCL-005": "Teilweise (Phase 5, Teil 1): m, cm, mm",
    "QTO-EVAL-001": "Teilweise (Phase 5, Teil 2): Mengenliste mit Summen, Volumen und Wandflächen, Export als XLSX oder CSV",
}
for _id, _status in STATUS.items():
    PATCHES.setdefault(_id, {})["Implementierungsstatus"] = _status

for r in rows:
    for col, val in PATCHES.pop(r[0], {}).items():
        r[HEAD.index(col)] = val
    r[9] = VER[r[9]]
assert not PATCHES, f"Unbekannte IDs: {list(PATCHES)}"

wb = Workbook()
ws = wb.active
ws.title = "Funktionsmatrix"
ws.append(HEAD)
for r in rows:
    ws.append(r)
for c in ws[1]:
    c.font = Font(bold=True, color="FFFFFF")
    c.fill = PatternFill("solid", fgColor="102A43")
widths = [14, 18, 26, 34, 60, 14, 16, 22, 10, 30, 9, 7, 18, 50, 14]
for i, w in enumerate(widths, 1):
    ws.column_dimensions[get_column_letter(i)].width = w
for row in ws.iter_rows(min_row=2):
    for c in row:
        c.alignment = Alignment(wrap_text=True, vertical="top")
ws.freeze_panes = "B2"
ws.auto_filter.ref = ws.dimensions

qs = wb.create_sheet("Quellen")
qs.append(["Kürzel", "Quelle", "URL / Ort"])
for k, (t, u) in SOURCES.items():
    qs.append([k, t, u])
qs.column_dimensions["B"].width = 70
qs.column_dimensions["C"].width = 90

lg = wb.create_sheet("Legende")
lg.append(["Feld", "Wert", "Bedeutung"])
for k, v in VER.items():
    lg.append(["Verifizierung", v, ""])
lg.append(["Priorität", "MVP", "Für erste nutzbare Version erforderlich"])
lg.append(["Priorität", "Hoch", "Folgt direkt nach MVP"])
lg.append(["Priorität", "Später", "Spätere Ausbaustufe"])
lg.append(["Implementierungsstatus", "Offen", "Noch nicht implementiert (Stand Phase 1: keine Implementierung)"])
lg.append(["Referenz-Kürzel", "", "Tastenkürzel der Referenz nur zur Orientierung; EGLE360 legt eigene Standardbelegung fest"])
lg.column_dimensions["B"].width = 45
lg.column_dimensions["C"].width = 80

# Zusammenfassung
from collections import Counter
sm = wb.create_sheet("Übersicht")
sm.append(["Kennzahl", "Wert"])
sm.append(["Funktionen gesamt", len(rows)])
for k, n in Counter(r[9] for r in rows).items():
    sm.append([k, n])
for k, n in Counter(r[10] for r in rows).items():
    sm.append([f"Priorität {k}", n])
sm.column_dimensions["A"].width = 50

wb.save(f"{OUT}/EGLE360_PLAN_Funktionsmatrix.xlsx")
with open(f"{OUT}/EGLE360_PLAN_Funktionsmatrix.csv", "w", newline="", encoding="utf-8-sig") as fh:
    w = csv.writer(fh, delimiter=";")
    w.writerow(HEAD)
    w.writerows(rows)

ids = [r[0] for r in rows]
assert len(ids) == len(set(ids))
print(len(rows), Counter(r[9] for r in rows), Counter(r[10] for r in rows))

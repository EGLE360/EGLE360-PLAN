# EGLE360 PLAN – Testanleitung

Stand: Entwicklungsversion 0.4 (Phase 4, PDF-Grundsystem und Markierungen). Dies ist eine frühe Testversion, keine fertige Software. Bitte nicht mit einzigen Kopien wichtiger Pläne arbeiten, sondern mit Kopien.

## Starten

1. Die Datei `EGLE360-PLAN-win-x64.zip` herunterladen.
2. Mit Rechtsklick „Alle extrahieren“ wählen und in einen beliebigen Ordner entpacken, zum Beispiel auf den Desktop.
3. Im entpackten Ordner `EGLE360-PLAN` die Datei `EGLE360.PLAN.exe` doppelklicken.

Eine Installation oder ein installiertes .NET ist nicht nötig, alles Erforderliche liegt im Ordner. Voraussetzung ist Windows 10 oder 11 (64 Bit).

Weil die Testversion noch nicht digital signiert ist, zeigt Windows beim ersten Start wahrscheinlich „Der Computer wurde durch Windows geschützt“. Dann auf „Weitere Informationen“ und anschließend „Trotzdem ausführen“ klicken. Manche Virenscanner fragen ebenfalls nach.

## Was sich testen lässt

### Grundgerüst
- Navigation links: Dokumente, Designsystem, Einstellungen.
- Einstellungen: Farbmodus hell, dunkel oder wie Windows. Die Wahl bleibt beim nächsten Start erhalten.
- Designsystem: Farben, Schriften und Bedienelemente der Marke EGLE360 mit den Original-Logos.

### PDF öffnen, ansehen und verwalten
- „PDF öffnen“ (Strg+O), Dateien per Ziehen ins Fenster legen, „Zuletzt geöffnet“. Mehrere PDFs erscheinen als Registerkarten (Strg+Tab wechselt).
- Ohne eigene Pläne: im Startbildschirm „Beispielplan öffnen“ wählen. Das ist ein frei erfundener Grundriss mit einigen Beispielmarkierungen.
- Zoom mit Strg+Mausrad, Strg+Plus/Minus, Eingabe in Prozent, „An Breite anpassen“ (Strg+2), „Ganze Seite“ (Strg+0).
- Verschieben mit gedrückter mittlerer Maustaste oder mit dem Hand-Werkzeug (H), Blättern mit Bild auf/ab und über das Seitenfeld.
- Seitenleiste mit Miniaturen und Lesezeichen (falls das PDF welche hat).
- Seiten drehen, verschieben, löschen, leere Seite einfügen, Seiten aus einem anderen PDF einfügen, Seiten als neues PDF speichern.
- Dokumenteigenschaften ansehen.
- Speichern (Strg+S) und Speichern unter (Strg+Umschalt+S).

### Markierungen
Über dem Plan liegt die Markierungsleiste. Werkzeuge und Tastenkürzel:

| Werkzeug | Taste | Bedienung |
|---|---|---|
| Auswählen | V | Markierung anklicken, verschieben, an den Griffen ziehen |
| Hand | H | Plan verschieben |
| Rechteck | R | Aufziehen |
| Ellipse | E | Aufziehen |
| Linie | L | Aufziehen |
| Pfeil | A | Aufziehen |
| Notiz | N | Klicken, Text eingeben |
| Polylinie | P | Punkte klicken, Doppelklick, Eingabetaste oder Rechtsklick beendet |
| Wolke | C | Punkte klicken, beenden wie bei Polylinie |
| Freihand | F | Mit gedrückter Maustaste zeichnen |
| Textfeld | T | Aufziehen oder klicken, Text eingeben |
| Polygon | K | Punkte klicken, beenden wie bei Polylinie |
| Hervorheben | M | Bereich aufziehen |

- Farbe, Linienstärke, Deckkraft und Füllung gelten für das nächste Werkzeug und für die ausgewählte Markierung.
- Doppelklick auf Textfeld oder Notiz bearbeitet den Text, Rechtsklick öffnet ein Menü (Text, nach vorn/hinten, löschen).
- Entf löscht, Esc bricht ab, Strg+Z macht rückgängig, Strg+Y wiederholt.
- Nach dem Speichern sind die Markierungen normale PDF-Anmerkungen. Zum Gegenprüfen die gespeicherte Datei in einem anderen PDF-Programm öffnen (zum Beispiel Bluebeam, Adobe Acrobat oder einem Browser). Die Markierungen sollten dort an derselben Stelle und gleich aussehen.

## Was noch fehlt

Diese Punkte sind geplant, aber in dieser Version noch nicht enthalten:

- Textsuche im PDF, Drucken, geteilte und synchronisierte Ansichten.
- Markierungsliste mit Filter und Export, Stempel, Unterschriften.
- Mengenermittlung (Maßstab, Längen, Flächen, Zählen) folgt in Phase 5.
- BIM, 4D/5D, KI-Funktionen und die AVA-Anbindung folgen in späteren Phasen.
- Programmsymbol, digitale Signatur und Installationsprogramm.

## Rückmeldung

Am hilfreichsten sind kurze Notizen nach dem Muster: Was wurde gemacht, was ist passiert, was wurde erwartet. Ein Screenshot (Windows+Umschalt+S) hilft sehr. Stürzt das Programm ab, bitte das Datum und die Uhrzeit notieren.

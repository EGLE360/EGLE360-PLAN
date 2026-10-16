# EGLE360 PLAN – Testanleitung

Stand: Entwicklungsversion 0.5 (PDF-Grundsystem, Markierungen, Textsuche, erste Mengenermittlung). Das ist eine frühe Testversion, keine fertige Software. Bitte mit Kopien von Plänen arbeiten, nicht mit den einzigen Originalen.

## Starten

1. `EGLE360-PLAN-win-x64.zip` herunterladen.
2. Rechtsklick auf die ZIP-Datei, „Alle extrahieren“, in einen beliebigen Ordner entpacken (zum Beispiel auf den Desktop).
3. Im entpackten Ordner `EGLE360-PLAN` die Datei `EGLE360.PLAN.exe` doppelklicken.

Eine Installation ist nicht nötig, auch kein .NET. Voraussetzung ist Windows 10 oder 11 mit 64 Bit.

Die Testversion ist noch nicht digital signiert. Windows meldet deshalb beim ersten Start wahrscheinlich „Der Computer wurde durch Windows geschützt“. Dann „Weitere Informationen“ und „Trotzdem ausführen“ wählen. Manche Virenscanner fragen ebenfalls nach.

Zum Löschen genügt es, den Ordner zu entfernen. Einstellungen und die Liste der zuletzt geöffneten Dateien liegen unter `%APPDATA%\EGLE360\PLAN`.

## Schnellster Einstieg

Im Startbildschirm „Beispielplan öffnen“ wählen. Das ist ein frei erfundener Grundriss im Maßstab 1:50 mit einigen Prüfvermerken und drei Beispielmessungen. Damit lässt sich alles unten Beschriebene ausprobieren, ohne eigene Pläne zu öffnen.

## Was sich testen lässt

### Grundgerüst
- Navigation links: Dokumente, Designsystem, Einstellungen.
- Einstellungen: hell, dunkel oder wie Windows. Die Wahl bleibt beim nächsten Start erhalten.

### PDF öffnen, ansehen und verwalten
- „PDF öffnen“ (Strg+O), Dateien ins Fenster ziehen, „Zuletzt geöffnet“. Mehrere PDFs erscheinen als Registerkarten, Strg+Tab wechselt, Strg+W schließt.
- Zoomen mit Strg+Mausrad, Strg+Plus und Strg+Minus, Prozentfeld, „An Breite anpassen“ (Strg+2), „Ganze Seite“ (Strg+0).
- Verschieben mit gedrückter mittlerer Maustaste, mit dem Hand-Werkzeug (H) oder durch Ziehen auf einer leeren Stelle. Umschalt+Mausrad verschiebt waagrecht.
- Seitenleiste mit Miniaturen, Lesezeichen und Suche.
- Seiten drehen, verschieben, löschen, leere Seite einfügen, Seiten aus einem anderen PDF einfügen, Seiten als neues PDF speichern (Werkzeugleiste oben oder Rechtsklick auf eine Miniatur).
- Dokumenteigenschaften ansehen.
- Speichern (Strg+S), Speichern unter (Strg+Umschalt+S).
- Drucken (Strg+P) über den Windows-Druckdialog, mit Seitenauswahl. Jede Seite wird auf das Papier eingepasst, quer liegende Pläne werden auf Hochformat gedreht. Zum Ausprobieren ohne Drucker eignet sich „Microsoft Print to PDF“.

### Textsuche
- Strg+F öffnet die Suche in der Seitenleiste. Begriff eingeben, Eingabetaste.
- Alle Fundstellen werden gelb hinterlegt, die aktuelle orange. Eingabetaste oder F3 springt weiter, Umschalt+F3 zurück, Klick auf einen Eintrag springt hin.
- Gescannte Pläne ohne Textebene enthalten keinen durchsuchbaren Text; die Suche meldet das.

### Markierungen
Über dem Plan liegt die Markierungsleiste. Nach dem Zeichnen springt das Werkzeug zurück auf „Auswählen“ und die neue Markierung ist ausgewählt.

| Werkzeug | Taste | Bedienung |
|---|---|---|
| Auswählen | V | Anklicken, verschieben, an den Griffen ziehen. Strg+Klick wählt mehrere |
| Hand | H | Plan verschieben |
| Rechteck | R | Aufziehen |
| Ellipse | E | Aufziehen |
| Linie | L | Aufziehen |
| Pfeil | A | Aufziehen |
| Polylinie | N | Punkte klicken, beenden mit Doppelklick, Eingabetaste oder Rechtsklick |
| Polygon | P | wie Polylinie |
| Wolke | C | wie Polylinie |
| Freihand | F | Mit gedrückter Maustaste zeichnen |
| Textfeld | T | Aufziehen oder klicken, dann Text eingeben |
| Notiz | K | Klicken, dann Text eingeben |
| Hervorhebung | M | Bereich aufziehen |

- Mit gedrückter Umschalttaste rasten Linien waagrecht, senkrecht oder unter 45 Grad ein, Rechtecke werden quadratisch.
- Farbe, Linienbreite, Deckkraft und Füllung gelten für neue Markierungen und für die Auswahl.
- Doppelklick bearbeitet Text oder Kommentar. Rechtsklick öffnet ein Menü (Text, Kopieren, Duplizieren, Vordergrund, Hintergrund, Löschen).
- Strg+C, Strg+V, Strg+D (Duplizieren), Strg+A (alle auf der Seite), Entf, Esc, Strg+Z, Strg+Y.
- Nach dem Speichern sind Markierungen normale PDF-Anmerkungen. Zum Gegenprüfen die Datei in einem anderen Programm öffnen, zum Beispiel Bluebeam, Adobe Acrobat oder einem Browser. Sie sollten dort an derselben Stelle gleich aussehen.

### Mengenermittlung (neu)
Rechts in der Markierungsleiste sitzen die Messwerkzeuge und die Schaltfläche für den Maßstab.

1. Maßstab festlegen: auf „Kein Maßstab“ klicken und einen Maßstab wie 1:50 wählen. Alternativ „Kalibrieren …“: eine bekannte Strecke im Plan aufziehen (zum Beispiel eine Bemaßung) und das wirkliche Maß eingeben, etwa „10“ oder „4,25“. Einheit m, cm oder mm. Der Maßstab gilt je Seite und lässt sich auf alle Seiten übertragen.
2. Messen:
   - Länge: Strecke aufziehen.
   - Polylänge: Punkte klicken, beenden mit Doppelklick, Eingabetaste oder Rechtsklick.
   - Umfang und Fläche: Eckpunkte klicken, beenden wie bei der Polylänge. Die Fläche zeigt zusätzlich den Umfang in der Statuszeile.
   - Ausschnitt: Rechtsklick auf den Rand oder die Beschriftung einer Fläche, „Ausschnitt hinzufügen“, dann das abzuziehende Polygon zeichnen (zum Beispiel einen Schacht). Der Wert wird um den Ausschnitt verringert.
   - Anzahl: jedes Objekt anklicken, mit Eingabetaste beenden. Braucht keinen Maßstab.
   - Winkel: erst einen Punkt auf dem ersten Schenkel, dann den Scheitel, dann einen Punkt auf dem zweiten Schenkel klicken. Braucht keinen Maßstab.
3. Der Wert steht als Beschriftung im Plan und ändert sich live beim Ziehen an den Eckpunkten.
4. Wird der Maßstab einer Seite geändert, übernehmen die vorhandenen Messungen auf dieser Seite ihn. Rückgängig nimmt das zurück.

Zum Ausprobieren im Beispielplan: Die Küche ist 10 m × 10 m, die Fläche muss also 100,00 m² ergeben. Der Abschnitt zwischen den Achsen A und C ist 10,00 m lang.

### Markierungs- und Mengenliste (neu)
- Schaltfläche mit dem Listensymbol rechts oben in der Werkzeugleiste. Die Liste erscheint unter dem Plan und lässt sich in der Höhe ziehen.
- Sie zeigt alle Markierungen des Dokuments mit Seite, Art, Messwert, Text, Maßstab, Autor und Datum. Spalten lassen sich durch Klick auf die Überschrift sortieren.
- Filterfeld und „Nur Messungen“ grenzen die Liste ein. Oben stehen die Summen je Messart, zum Beispiel alle Flächen zusammen.
- Klick auf eine Zeile springt zur Markierung und wählt sie aus.
- „Als CSV exportieren“ speichert die (gefilterte) Liste. Die Datei öffnet sich mit Doppelklick in Excel, mit deutschem Zahlenformat.

## Was noch fehlt

Diese Punkte sind geplant, aber noch nicht enthalten:

- Drucken in Originalgröße oder gekachelt, geteilte und synchronisierte Ansichten, Metadaten bearbeiten.
- Stempel, Unterschriften, Unterstreichen und Durchstreichen von Text, Bemaßungslinien, Drehen von Markierungen.
- Messungen: Volumen, Wandflächen, Radius, Objektfang, Kostenansätze, Excel als XLSX und die Übernahme in EGLE360 AVA.
- Ob Bluebeam die gespeicherten Messungen als Messungen (mit Wert in der Markups-Liste) erkennt, ist noch nicht geprüft. Die Datei enthält dafür die genormten PDF-Messangaben; im Plan sind die Messungen in jedem Fall sichtbar.
- BIM, 4D/5D, KI-Funktionen und die AVA-Anbindung folgen in späteren Phasen.
- Programmsymbol, digitale Signatur und Installationsprogramm.

## Rückmeldung

Am hilfreichsten sind kurze Notizen nach dem Muster: Was wurde gemacht, was ist passiert, was war erwartet. Ein Screenshot (Windows+Umschalt+S) hilft sehr. Bei einem unerwarteten Fehler zeigt das Programm eine Meldung und schreibt die Einzelheiten nach `%APPDATA%\EGLE360\PLAN\Fehlerprotokoll.txt`. Diese Datei bitte mitschicken.

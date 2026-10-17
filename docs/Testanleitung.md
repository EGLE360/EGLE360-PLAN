# EGLE360 PLAN – Testanleitung

Stand: Entwicklungsversion 0.7 (PDF-Grundsystem, Markierungen mit Stempeln und Bemaßung, Textsuche, Mengenermittlung mit Volumen, Wandflächen und Excel-Export, Werkzeugbibliothek, Planverwaltung mit Indexverlauf). Das ist eine frühe Testversion, keine fertige Software. Bitte mit Kopien von Plänen arbeiten, nicht mit den einzigen Originalen.

## Starten

1. `EGLE360-PLAN-win-x64.zip` herunterladen.
2. Rechtsklick auf die ZIP-Datei, „Alle extrahieren“, in einen beliebigen Ordner entpacken (zum Beispiel auf den Desktop).
3. Im entpackten Ordner `EGLE360-PLAN` die Datei `EGLE360.PLAN.exe` doppelklicken.

Eine Installation ist nicht nötig, auch kein .NET. Voraussetzung ist Windows 10 oder 11 mit 64 Bit.

Die Testversion ist noch nicht digital signiert. Windows meldet deshalb beim ersten Start wahrscheinlich „Der Computer wurde durch Windows geschützt“. Dann „Weitere Informationen“ und „Trotzdem ausführen“ wählen. Manche Virenscanner fragen ebenfalls nach.

Zum Löschen genügt es, den Ordner zu entfernen. Einstellungen und die Liste der zuletzt geöffneten Dateien liegen unter `%APPDATA%\EGLE360\PLAN`.

## Schnellster Einstieg

Im Startbildschirm „Beispielplan öffnen“ wählen. Das ist ein frei erfundener Grundriss im Maßstab 1:50 mit einigen Prüfvermerken, einem Stempel und mehreren Beispielmessungen. Damit lässt sich alles unten Beschriebene ausprobieren, ohne eigene Pläne zu öffnen.

## Was sich testen lässt

### Grundgerüst
- Navigation links: Dokumente, Designsystem, Einstellungen.
- Einstellungen: hell, dunkel oder wie Windows. Die Wahl bleibt beim nächsten Start erhalten.

### PDF öffnen, ansehen und verwalten
- „PDF öffnen“ (Strg+O), Dateien ins Fenster ziehen, „Zuletzt geöffnet“. Mehrere PDFs erscheinen als Registerkarten, Strg+Tab wechselt, Strg+W schließt.
- Zoomen mit Strg+Mausrad, Strg+Plus und Strg+Minus, Prozentfeld, „An Breite anpassen“ (Strg+2), „Ganze Seite“ (Strg+0).
- Verschieben mit gedrückter mittlerer Maustaste, mit dem Hand-Werkzeug (H) oder durch Ziehen auf einer leeren Stelle. Umschalt+Mausrad verschiebt waagrecht.
- Seitenleiste mit Miniaturen, Lesezeichen und Suche.
- Vorherige und nächste Ansicht: Nach einem Sprung (Seitenwechsel, Fundstelle, Eintrag in der Liste) führt die Pfeiltaste links neben der Seitenzahl, Alt+Pfeil links oder die Zurück-Taste der Maus zur vorigen Stelle zurück.
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
| Schreibmaschine | W | Klicken und Text eingeben; der Text steht ohne Rahmen direkt auf dem Plan |
| Sprechblase | B | Von der Stelle, auf die der Pfeil zeigen soll, zum Textfeld ziehen (oder nur klicken), dann Text eingeben. Rahmen und Pfeilspitze lassen sich danach an den Griffen verschieben |
| Notiz | K | Klicken, dann Text eingeben |
| Hervorhebung | M | Über Text ziehen: Die Markierung folgt den Textzeilen. Ohne Text im Bereich entsteht ein Rechteck |
| Unterstreichen | U | Über Text ziehen; jede Zeile wird unterstrichen. Funktioniert nur bei echtem Text im PDF, nicht bei gescannten Plänen |
| Durchstreichen | X | wie Unterstreichen |
| Stempel | S | Aufschrift im Menü der Schaltfläche wählen (GEPRÜFT, FREIGEGEBEN, ABGELEHNT und andere oder eine eigene), dann klicken oder aufziehen. Darunter stehen automatisch Autor und Datum |
| Bemaßung | D | Maßlinie aufziehen; braucht wie die Messungen einen Maßstab. Die Zahl steht über der Linie |

- Mit gedrückter Umschalttaste rasten Linien waagrecht, senkrecht oder unter 45 Grad ein, Rechtecke werden quadratisch.
- Objektfang (Schaltfläche mit dem Fadenkreuz neben dem Maßstab, standardmäßig an): Beim Zeichnen und beim Ziehen von Eckpunkten rastet der Punkt an Linienenden und Linienmitten des Plans und an Eckpunkten anderer Markierungen ein. Ein grünes Quadrat zeigt den Fangpunkt. Das funktioniert nur bei Vektorplänen (aus CAD exportiert), nicht bei gescannten. Die Umschalttaste schaltet den Fang vorübergehend ab.
- Farbe, Linienbreite, Deckkraft und Füllung gelten für neue Markierungen und für die Auswahl.
- Doppelklick bearbeitet Text oder Kommentar. Rechtsklick öffnet ein Menü (Text, Kopieren, Duplizieren, Vordergrund, Hintergrund, Löschen).
- Strg+C, Strg+V, Strg+D (Duplizieren), Strg+A (alle auf der Seite), Entf, Esc, Strg+Z, Strg+Y.
- Mehrere ausgewählte Markierungen lassen sich per Rechtsklick ausrichten (links, rechts, oben, unten, zentriert) und ab drei gleichmäßig verteilen.
- Darstellung übertragen: Strg+Umschalt+C kopiert Farbe, Linie und Füllung der ausgewählten Markierung, Strg+Umschalt+V überträgt sie auf die Auswahl.
- Die Schaltfläche mit dem durchgestrichenen Auge ganz rechts in der Markierungsleiste blendet alle Markierungen aus, um den Plan ohne sie zu sehen.
- Nach dem Speichern sind Markierungen normale PDF-Anmerkungen. Zum Gegenprüfen die Datei in einem anderen Programm öffnen, zum Beispiel Bluebeam, Adobe Acrobat oder einem Browser. Sie sollten dort an derselben Stelle gleich aussehen.

### Mengenermittlung
Rechts in der Markierungsleiste sitzen die Messwerkzeuge und die Schaltfläche für den Maßstab.

1. Maßstab festlegen: auf „Kein Maßstab“ klicken und einen Maßstab wie 1:50 wählen. Alternativ „Kalibrieren …“: eine bekannte Strecke im Plan aufziehen (zum Beispiel eine Bemaßung) und das wirkliche Maß eingeben, etwa „10“ oder „4,25“. Einheit m, cm oder mm. Der Maßstab gilt je Seite und lässt sich auf alle Seiten übertragen.
2. Messen:
   - Länge: Strecke aufziehen.
   - Polylänge: Punkte klicken, beenden mit Doppelklick, Eingabetaste oder Rechtsklick.
   - Umfang und Fläche: Eckpunkte klicken, beenden wie bei der Polylänge. Die Fläche zeigt zusätzlich den Umfang in der Statuszeile.
   - Ausschnitt: Rechtsklick auf den Rand oder die Beschriftung einer Fläche, „Ausschnitt hinzufügen“, dann das abzuziehende Polygon zeichnen (zum Beispiel einen Schacht). Der Wert wird um den Ausschnitt verringert.
   - Anzahl: jedes Objekt anklicken, mit Eingabetaste beenden. Braucht keinen Maßstab.
   - Winkel: erst einen Punkt auf dem ersten Schenkel, dann den Scheitel, dann einen Punkt auf dem zweiten Schenkel klicken. Braucht keinen Maßstab.
   - Radius: erst den Mittelpunkt, dann einen Punkt auf dem Kreis anklicken oder aufziehen. Durchmesser: von einem Kreisrand zum gegenüberliegenden ziehen. Die Liste zeigt zusätzlich die Kreisfläche.
   - Volumen: Rechtsklick auf eine Fläche, „Tiefe für Volumen …“, zum Beispiel 0,25 für eine Bodenplatte. Die Beschriftung zeigt dann Fläche und Volumen.
   - Wandfläche: Rechtsklick auf eine Länge, Polylänge oder einen Umfang, „Höhe für Wandfläche …“, zum Beispiel 2,80. Ein leeres Feld entfernt die Angabe wieder.
3. Der Wert steht als Beschriftung im Plan und ändert sich live beim Ziehen an den Eckpunkten.
4. Wird der Maßstab einer Seite geändert, übernehmen die vorhandenen Messungen auf dieser Seite ihn. Rückgängig nimmt das zurück.

Zum Ausprobieren im Beispielplan: Die Küche ist 10 m × 10 m, die Fläche muss also 100,00 m² ergeben. Der Abschnitt zwischen den Achsen A und C ist 10,00 m lang. Die Innenwand an Achse E ist 11,00 m lang und 2,80 m hoch, ihre Wandfläche also 30,80 m². Die Bemaßung unter dem Grundriss zeigt die Gebäudelänge von 30,00 m.

### Markierungs- und Mengenliste (neu)
- Schaltfläche mit dem Listensymbol rechts oben in der Werkzeugleiste. Die Liste erscheint unter dem Plan und lässt sich in der Höhe ziehen.
- Sie zeigt alle Markierungen des Dokuments mit Seite, Art, Messwert, Text, Maßstab, Autor und Datum. Spalten lassen sich durch Klick auf die Überschrift sortieren.
- Filterfeld und „Nur Messungen“ grenzen die Liste ein. Oben stehen die Summen je Messart, zum Beispiel alle Flächen zusammen, dazu Volumen und Wandflächen.
- Klick auf eine Zeile springt zur Markierung und wählt sie aus.
- „Exportieren (Excel) …“ speichert die (gefilterte) Liste als Excel-Arbeitsmappe (XLSX) mit echten Zahlen und Datumswerten, einem Filter in der Kopfzeile und einem zweiten Blatt mit den Summen. Im Speichern-Dialog lässt sich stattdessen CSV wählen.

### Status, Sperren und Preise (neu)
- Rechtsklick auf eine Markierung, „Status“: Offen, In Bearbeitung, Erledigt, Geprüft, Freigegeben oder Abgelehnt. Gespeichert werden auch Name und Zeitpunkt. Der Status steht in der Liste, in der Statuszeile und im Excel-Export.
- „Sperren“ schützt eine Markierung: Sie lässt sich noch auswählen und kopieren, aber nicht mehr verschieben, ändern oder löschen, bis sie wieder entsperrt wird. Im Beispielplan ist die Küchenfläche gesperrt.
- „Einheitspreis …“ bei Längen, Flächen, Umfängen und Anzahlen: Preis in Euro je Einheit. Ist eine Höhe oder Tiefe angegeben, gilt der Preis je m² Wandfläche oder je m³ Volumen. Die Liste zeigt den Gesamtpreis und oben die Summe aller Kosten. Im Beispielplan: Küche 100 m² zu 38,50 € und drei Türen zu 650 €, zusammen 5.800,00 €.

### Werkzeugbibliothek (neu)
- In der Seitenleiste unter „Werkzeuge“ stehen gespeicherte Werkzeuge, nach Sätzen geordnet. Mitgeliefert sind „Planprüfung“ (Mangel, Hinweis, Rückfrage, Erledigt, Geprüft) und „Mengen Hochbau“ (Boden- und Deckenfläche, Wand, Sockelleiste, Türen, Fenster). Preise und Höhen sind dort bewusst leer, weil sie vom Projekt abhängen.
- Ein Klick wählt ein Werkzeug. Es bleibt aktiv, sodass sich mehrere Flächen oder Zählungen hintereinander erfassen lassen. Esc beendet es.
- Eigene Werkzeuge: Markierung so einstellen, wie sie sein soll (Farbe, Linie, Höhe oder Tiefe, Einheitspreis, Text), dann Rechtsklick, „Als Werkzeug speichern …“. Neue Markierungen mit diesem Werkzeug bekommen alles davon mit.
- Rechtsklick in der Liste: Favorit (steht bei der Suche oben), Umbenennen, Löschen, Satz umbenennen oder löschen, Import und Export als Datei für andere Arbeitsplätze. Das Suchfeld findet Werkzeuge nach Name, Satz und Art.
- Die Bibliothek liegt in `%APPDATA%\EGLE360\PLAN\Werkzeuge.json`.

### Planverwaltung (neu)
- Links in der Navigation gibt es „Pläne“. Ein Planprojekt ist ein Ordner mit einer Planliste. „Neues Planprojekt“ legt sie in einem Ordner deiner Wahl an, am besten in einem leeren Testordner.
- „Pläne hinzufügen“ (oder PDF-Dateien in die Liste ziehen) kopiert die Dateien in den Unterordner `Pläne` des Projekts. Die Originale bleiben unverändert.
- Plannummer, Index und Bezeichnung werden aus dem Plankopf („Plannummer …“, „Plan-Nr. …“, „Index …“) oder aus dem Dateinamen erkannt, z. B. `AR-101_Grundriss EG_Index B.pdf`. Die Erkennung arbeitet mit Mustern und kann danebenliegen. Rechts lassen sich alle Angaben korrigieren, dazu Gewerk, Geschoss, Gebäude, Bauabschnitt, Status, Verfasser und Freigabe.
- Hat eine neue Datei dieselbe Plannummer wie ein vorhandener Plan, aber einen anderen Index, wird sie als neuer Stand eingetragen. Mit dem Häkchen „Markierungen in neuen Index übernehmen“ wandern Markierungen und Maßstäbe des bisherigen Stands auf dieselben Seiten der neuen Datei. Bitte vorher Änderungen am alten Stand speichern, übernommen wird, was in der Datei steht.
- „Neuer Index“ trägt für den ausgewählten Plan gezielt eine Datei ein, mit Änderungsvermerk. Unter „Verlauf“ stehen alle Stände, ein Doppelklick öffnet auch ältere.
- „Planliste exportieren (Excel)“ schreibt die (gefilterte) Liste und den Verlauf aller Stände in eine Arbeitsmappe.

## Was noch fehlt

Diese Punkte sind geplant, aber noch nicht enthalten:

- Drucken in Originalgröße oder gekachelt, geteilte und synchronisierte Ansichten, Metadaten bearbeiten.
- Stempel aus eigenen Bilddateien, Unterschriften, Drehen von Markierungen.
- Messungen: Gefälle, Schnittpunktfang, Preislisten je Werkzeug und die Übernahme in EGLE360 AVA.
- Ob Bluebeam die gespeicherten Messungen als Messungen (mit Wert in der Markups-Liste) erkennt, ist noch nicht geprüft. Die Datei enthält dafür die genormten PDF-Messangaben; im Plan sind die Messungen in jedem Fall sichtbar.
- BIM, 4D/5D, KI-Funktionen und die AVA-Anbindung folgen in späteren Phasen.
- Programmsymbol, digitale Signatur und Installationsprogramm.

## Rückmeldung

Am hilfreichsten sind kurze Notizen nach dem Muster: Was wurde gemacht, was ist passiert, was war erwartet. Ein Screenshot (Windows+Umschalt+S) hilft sehr. Bei einem unerwarteten Fehler zeigt das Programm eine Meldung und schreibt die Einzelheiten nach `%APPDATA%\EGLE360\PLAN\Fehlerprotokoll.txt`. Diese Datei bitte mitschicken.

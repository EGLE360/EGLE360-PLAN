# EGLE360 PLAN – Projektunterlagen

Stand: 16.10.2026

## Inhalt

- `phase1/EGLE360_PLAN_Funktionsmatrix.xlsx` (und `.csv`): 272 Funktionen mit eindeutiger ID, Referenzpaket, Tastenkürzel der Referenz, Quelle, Verifizierungsstatus, Priorität, Phase, Abhängigkeiten, Akzeptanzkriterium und Implementierungsstatus. Tabellenblätter: Funktionsmatrix, Quellen, Legende, Übersicht.
- `phase2/EGLE360_PLAN_Architektur.md`: Architekturentscheide mit Begründung zu Lizenz, Sicherheit, Leistung und Wartbarkeit.
- `phase2/EGLE360_PLAN_Datenbankschema.sql`: PostgreSQL-Schema mit 44 Tabellen, gegen PostgreSQL 16 fehlerfrei eingespielt.
- `brand/`: Logodateien, unverändert wie geliefert.

## Wichtige Erkenntnisse aus Phase 1

1. Die offizielle Funktionsliste von Bluebeam (Revu 21, Pakete Basics, Core, Complete, Max) ist vollständig in die Matrix übernommen und mit Quelle belegt. Einzelwerkzeuge, Tastenkürzel, Markup-Listen-Spalten, Messwerkzeuge und das Stapelmenü stammen aus den offiziellen Hilfeseiten.
2. Bluebeam hat keinen eigenen IFC-Viewer, keine Kollisionsprüfung, kein 4D/5D und kein BCF. Diese Bereiche sind in der Matrix als EGLE360-Erweiterung geführt.
3. Smart Review und Smart Overlay (Bluebeam Max) sind laut offizieller FAQ im Preview-Status, laufen in der Cloud und unterstützen nur imperiale Maße. Für Österreich und Deutschland braucht EGLE360 metrische Prüfregeln nach ÖNORM/DIN.
4. 14 Funktionen sind als ungeklärt markiert, weil die gesichteten Quellen sie nicht ausreichend beschreiben (z. B. Magic Wand, Synchronisierte Ansicht im Detail, Stapelnummerierung, Werkzeugset-Import aus Bluebeam).

## Grenzen der Analyse

- Die Matrix gibt die öffentlich dokumentierten Funktionen wieder. Dialogfenster und Einstellungen sind auf Funktionsebene erfasst, nicht Feld für Feld. Eine Feld-für-Feld-Erfassung aller Dialoge wäre nur mit Zugriff auf eine lizenzierte Revu-Installation verlässlich möglich.
- Tastenkürzel der Referenz dienen nur zur Orientierung. EGLE360 legt eine eigene Standardbelegung fest.
- Lizenzangaben zu Drittbibliotheken sind vor der Einbindung an der jeweils aktuellen Lizenzdatei zu prüfen.

## Logodateien

- `EGLE360_Master_Light_Flat_8192.png`: transparenter Hintergrund, Schriftzug „GLE“ in Weiß, also für dunkle Flächen. Die Datei ist 1498 × 1050 Pixel groß (nicht 8192) und als 16-Farben-Palettenbild gespeichert. Die Farben weichen leicht von der Farbtabelle ab (#45A049 statt #45A04B, #2682D6 statt #2283D6, #B8C0C8 statt #BAC2CA). Die E-Striche sind grün, blau und grau, die Tabelle im Master-Prompt nennt für die E-Striche nur Grün.
- `EGLE360_Horizontal_Light_4096x1024.png`: weißer, nicht transparenter Hintergrund, dunkler Schriftzug mit Zusatz „Digitales Baumanagement“, für helle Flächen. Die Farben dieser Variante (Dunkelgrün, Dunkelblau, Anthrazit) entsprechen eher den allgemeinen Markenfarben als der Logofarbtabelle.
- Für das Designsystem (Phase 3) werden Vektorfassungen (SVG oder PDF) benötigt, damit das Logo in jeder Größe scharf bleibt.

# EGLE360 PLAN

PDF-, Bauplan-, BIM-, Mengenermittlungs- und KI-Software von EGLE360 – Digitales Baumanagement.

Zielmärkte: Österreich und Deutschland. Zielplattform: Windows-Desktop, ergänzend Web und Mobile.

Projektunterlagen liegen unter `docs/`.

## Testversion

Jeder CI-Lauf eines Pull Requests baut ein eigenständiges Windows-Paket (64 Bit, .NET eingebaut, keine Installation nötig). Der neueste Stand liegt als Vorabversion unter [Releases, „testversion“](https://github.com/EGLE360/EGLE360-PLAN/releases/tag/testversion). Wie man es startet und was sich testen lässt, steht in [docs/Testanleitung.md](docs/Testanleitung.md).

## Entwickeln

```
dotnet build EGLE360.PLAN.sln -c Release
dotnet test EGLE360.PLAN.sln -c Release
```

Auf Linux zusätzlich `-p:EnableWindowsTargeting=true`. Die Oberfläche (WPF) läuft nur unter Windows; `EGLE360.PLAN.exe --screenshots <Ordner>` erzeugt Screenshots aller Ansichten, so wie im CI.

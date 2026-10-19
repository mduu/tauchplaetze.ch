# Testdaten

Echte Antworten der Quellen aus dem Spike 0.5 (Riff Williams, Zürichsee). Die Contract Tests lesen sie.

| Ordner | Quelle | Abgerufen |
|---|---|---|
| `alplakes/` | Alplakes: 3D `delft3d-flow`, 1D Simstrat `upperzurich`, Satellit `Zsd_lee` und `chla` | 17.10.2026, 13:06 |
| `alplakes/` (Sentinel-2) | Satellit `Z490` bei Riff Williams, 50 m seewärts (47.2846/8.6080) und 100 m seewärts (47.28432/8.60749), je `window=2`, und `remotesensing/metadata` gekürzt auf Zürichsee, Zugersee, Barone | 19.10.2026 |
| `alplakes/` (Strömung) | 3D `delft3d-flow` Zürichsee bei Riff Williams (47.2849/8.6085) und `mitgcm` Zugersee (47.13/8.49), je 19.10.2026 00:00–24:00 UTC, mit `u` und `v` nach dem Fix von Eawag | 19.10.2026 |
| `alplakes/` (Abdeckung) | `simulations/metadata`, `simulations/1d/metadata`, `remotesensing/metadata` ungekürzt (`metadata-*-20261019.json`) | 19.10.2026, 19:20 |
| `open-meteo/` | ICON-CH1, ICON-DWD (13:06), ICON-CH2 (12:57) | 17.10.2026 |
| `open-meteo/` (gebündelt) | ICON-CH2 an zwei Punkten in einem Call: Riff Williams (47.2849/8.6085) und Zugersee (47.13/8.49) | 19.10.2026, 19:05 |
| `hydro/` | BAFU über existenz.ch, Station 2104 Linth Weesen, Abfluss | 17.10.2026, 13:06 |
| `hydro/` (Flussplätze) | BAFU über existenz.ch, Station 2091 Rhein – Rheinfelden, Abfluss und Pegel, `startdate=2026-10-18`, `enddate=2026-10-20` | 19.10.2026, 15:45 |
| `hydro/` (Temperatur) | BAFU über existenz.ch, Stationen 2091 Rhein – Rheinfelden und 2605 Verzasca – Lavertezzo, `parameters=flow,temperature`, 2605 ohne Temperatur | 19.10.2026, 16:15 |

Ändert eine Quelle ihr Format, kommt eine neue Antwort dazu. Die alte bleibt, solange der Parser sie lesen muss.

# Testdaten

Echte Antworten der Quellen aus dem Spike 0.5 (Riff Williams, Zürichsee). Die Contract Tests lesen sie.

| Ordner | Quelle | Abgerufen |
|---|---|---|
| `alplakes/` | Alplakes: 3D `delft3d-flow`, 1D Simstrat `upperzurich`, Satellit `Zsd_lee` und `chla` | 17.10.2026, 13:06 |
| `alplakes/` (Sentinel-2) | Satellit `Z490` bei Riff Williams, 50 m seewärts (47.2846/8.6080) und 100 m seewärts (47.28432/8.60749), je `window=2`, und `remotesensing/metadata` gekürzt auf Zürichsee, Zugersee, Barone | 19.10.2026 |
| `open-meteo/` | ICON-CH1, ICON-DWD (13:06), ICON-CH2 (12:57) | 17.10.2026 |
| `hydro/` | BAFU über existenz.ch, Station 2104 Linth Weesen, Abfluss | 17.10.2026, 13:06 |

Ändert eine Quelle ihr Format, kommt eine neue Antwort dazu. Die alte bleibt, solange der Parser sie lesen muss.

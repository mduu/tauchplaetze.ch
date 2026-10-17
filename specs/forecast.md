# Tauchprognose

Die Prognose beantwortet pro Platz und Tag: **Lohnt sich ein Tauchgang?** Sie fasst das im **Tauch-Index** (0.0 bis 10.0) zusammen. Dazu zeigt sie die Werte, aus denen der Index entsteht.

Die Prognose ist ein **Indikator**. Die Oberfläche sagt das klar. Sie misst die Sicht nicht.

## Grundsatz

1. **Phase 1: regelbasierter Index** (`rules-1.0`). Transparent, erklärbar, ohne Trainingsdaten (ADR-004).
2. **Phase 2: eigenes ML-Modell**, sobald genug Labels vorliegen. Es ersetzt die Regeln nur, wenn es im Rückblick besser trifft.

## Eingaben pro Platz und Tag

Das Tauchfenster ist 08:00 bis 16:00 Uhr Lokalzeit.

| Eingabe | Quelle | Verwendung |
|---|---|---|
| Temperaturprofil (0 bis 40 m) | Alplakes 3D-Simulation an der Koordinate des Platzes | Temperatur auf 0/10/20/30 m, Sprungschicht |
| Oberflächenklarheit | Alplakes Remote Sensing (Satellit), falls für den See verfügbar | Klarheit |
| Gemessene Secchi-Tiefe | Alplakes In-situ (`/insitu/secchi/{lake}`) | Basiswert Klarheit, Kalibrierung |
| Algen (Chlorophyll) | Alplakes Remote Sensing | Abzug bei Klarheit |
| Wind (Stärke, Richtung) | MeteoSchweiz ICON-Prognose am Punkt | Oberfläche, Ufer aufgewühlt |
| Niederschlag der letzten 72 h | MeteoSchweiz (Messung oder Reanalyse) | Trübung durch Zuflüsse |
| Abfluss der Zuflüsse | BAFU Hydrodaten (Station pro See, optional pro Platz) | Trübung durch Zuflüsse |

Die Endpunkte stehen in `data-sources.md`.

## Abgeleitete Werte

### Sprungschicht

- Aus dem Temperaturprofil die Schicht mit dem stärksten Temperaturabfall bestimmen.
- `ThermoclineTop` und `ThermoclineBottom`: Grenzen, an denen der Gradient den Schwellwert überschreitet (Standard 0.5 °C pro m, konfigurierbar).
- Kein Gradient über dem Schwellwert (typisch im Winter): keine Sprungschicht. Der See ist durchmischt.

### Sicht-Indikator

- Grundlage: geschätzte Klarheit (Secchi-Schätzung aus Satellit, sonst letzte Messung).
- Unter der Sprungschicht ist die Sicht im Sommer meist besser. Dann setzt der Algorithmus `VisibilityGoodBelow = ThermoclineBottom`.
- Ergebnis als Stufe: `Poor`, `Moderate`, `Good`, `VeryGood`, `Unknown`.

## Der regelbasierte Index (`rules-1.0`)

Vier Teilwerte, je 0 bis 10. Der Index ist der gewichtete Mittelwert.

| Teilwert | Gewicht | 10 Punkte | 1 Punkt |
|---|---|---|---|
| Klarheit | 35 % | Secchi-Schätzung ≥ 10 m, wenig Algen | < 2 m oder starke Algenblüte |
| Wind | 25 % | 0–2 Bft im Tauchfenster | ≥ 5 Bft |
| Zuflüsse | 20 % | < 5 mm Regen in 72 h und Abfluss unter 1.2 × Median | Starkregen oder Abfluss über 3 × Median |
| Schichtung | 20 % | klare Schicht unter einer Sprungschicht ≤ 20 m, oder durchmischter See mit wenig Algen | Sprungschicht tiefer als 30 m bei trüber Oberfläche |

Zwischenwerte linear interpolieren. Die Stützpunkte und Gewichte stehen in `appsettings.json` (`Forecast:Rules`). So lassen sie sich ohne Code anpassen. Jede Änderung erhöht die `ModelVersion` (`rules-1.1`, …).

**Fehlende Daten:** Fehlt ein Teilwert, verteilt der Algorithmus sein Gewicht auf die anderen. Fehlen Klarheit **und** Wind, ist der Index `null`. Die Seite zeigt dann «Keine Prognose».

**Begründung:** Der Algorithmus speichert `ReasonCodes` (z.B. `CalmLake`, `ClearBelowThermocline`, `TurbidAfterRain`, `WindAfternoon`). Die Oberfläche bildet daraus einen Satz aus den `.resx`-Ressourcen. So ist die Begründung in allen vier Sprachen verfügbar.

**Temperatur ist kein Abzug.** Kaltes Wasser ist in der Schweiz normal. Die Temperatur in der Tiefe erscheint als Information (und als Anzug-Hinweis), nicht im Index.

## Der Job

Der Job läuft als `BackgroundService` im Web-Host (ADR-006). Er ruft nur Handler des Core.

- **Zeitplan:** täglich 04:30 Lokalzeit (vollständiger Lauf) und 13:00 Lokalzeit (nur Wetter neu). Konfigurierbar.
- **Sperre:** nur ein Lauf gleichzeitig (Blob Lease oder SQL-Lock). Wichtig, falls später mehrere Instanzen laufen.
- **Ablauf (Command `RunForecast`):**
  1. `ForecastRun` mit Status `Running` anlegen.
  2. Pro aktivem See: Metadaten der Alplakes-Modelle laden (verfügbarer Zeitraum).
  3. Pro veröffentlichtem Platz: Temperatur über Tiefe und Zeit mit **einem** Aufruf holen (`/simulations/depthtime/...` für den ganzen Prognosezeitraum).
  4. Pro See: Secchi (in situ), Remote-Sensing-Werte.
  5. Pro Platz: Wetterprognose MeteoSchweiz am Punkt. Plätze im selben Gitterpunkt teilen sich die Abfrage.
  6. Pro See: Abfluss der zugeordneten BAFU-Stationen.
  7. Rohdaten als JSON in Blob Storage ablegen (`forecast-raw/{date}/{runId}/{source}/{key}.json`).
  8. Index pro Platz und Tag berechnen. `DailyForecast` und `LakeConditions` schreiben (Upsert).
  9. Output Cache der öffentlichen Seiten invalidieren (Tag `forecast`).
  10. `ForecastRun` abschliessen (`Succeeded`, `PartiallySucceeded` oder `Failed`).
- **Höflich gegenüber den Quellen:** höchstens 2 Anfragen pro Sekunde und Quelle. Wiederholung mit Backoff (Polly über `Microsoft.Extensions.Http.Resilience`). Eindeutiger `User-Agent` mit Kontaktadresse.
- **Ausfall einer Quelle:** Der Lauf schreibt die übrigen Werte. Die letzten gültigen Werte der ausgefallenen Quelle bleiben bis zu 48 h gültig. Die Seite zeigt das Datenalter.

## Phase 2: eigenes Modell

### Labels

- **Meldungen** der Community (Sicht pro Tiefenband).
- **Gemessene Secchi-Tiefen** (Alplakes in situ).
- **Historische Labels von Marc:** Tauchlogs sowie Datum und Ort aus den EXIF-Daten der Fotos (tauchfotos.ch). Dazu eine Sichtbewertung von Marc pro Tauchgang.

### Features

Dieselben Eingaben wie oben. Für historische Labels liefert Alplakes Reanalyse- bzw. Vergangenheitsdaten. So lassen sich auch alte Tauchgänge mit Features versehen.

### Modell

- **ML.NET mit LightGBM** (`Microsoft.ML.LightGbm`). Ziel: Sichtklasse pro Tiefenband (Klassifikation) oder Secchi-Schätzung (Regression).
- Kein Python nötig. Das Training läuft lokal in Sekunden bis Minuten. Keine GPU, keine laufenden Kosten.
- Training über einen Admin-Command oder ein CLI-Projekt (`Tauchplaetze.Cli`, Fassade wie bei bau-ticker).
- Das Modell liegt versioniert in Blob Storage (`models/visibility-{version}.zip`). Der Job lädt die aktive Version.

### Wechsel von Regeln zu Modell

- Backtesting über die letzten Monate: Trefferquote gegenüber Meldungen und Secchi-Messungen.
- Das Modell ersetzt die Regeln erst, wenn es klar besser ist. Bis dahin läuft es im Schatten mit (beide Werte speichern, nur Regeln anzeigen).
- Die Teilwerte für die Begründung bleiben auch mit Modell erhalten (Feature-Beiträge oder weiter die Regeln für den Text).

## Tests

- Unit Tests für Sprungschicht-Erkennung, Teilwerte, Interpolation, fehlende Daten, Gewichtsverteilung.
- Feste Beispielprofile (Sommer mit Sprungschicht, Winter durchmischt, nach Starkregen) als Testdaten.
- Kein Test gegen die echte API in der CI. Ein manueller Smoke Test (`dotnet run -- forecast smoke`) prüft die Anbindung.

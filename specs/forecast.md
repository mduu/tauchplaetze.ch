# Tauchprognose

Die Prognose beantwortet pro Platz und Tag: **Lohnt sich ein Tauchgang?** Sie fasst das im **Tauch-Index** (0.0 bis 10.0) zusammen. Dazu zeigt sie die Werte, aus denen der Index entsteht.

Die Prognose ist ein **Indikator**. Die Oberfläche sagt das klar. Sie misst die Sicht nicht.

## Grundsatz

1. **Phase 1: regelbasierter Index** (`rules-1.0`). Transparent, erklärbar, ohne Trainingsdaten (ADR-004).
2. **Phase 2: eigenes ML-Modell**, sobald genug Labels vorliegen. Es ersetzt die Regeln nur, wenn es im Rückblick besser trifft.

## Prognosetage

- Die Prognose zeigt **5 Tage**: heute und die 4 folgenden Tage (Entscheid Marc, 17.10.2026).
- **Mit Index:** Tage, für die Wasserdaten vorliegen. Beim 3D- und 1D-Modell sind das heute und die 3 folgenden Tage.
- **Ausblick:** Die übrigen Tage zeigen Wind und Regen, aber keinen Index. Die Oberfläche nennt sie «Ausblick».
- Der Job liest das Ende der Wasserdaten aus der Antwort der Quelle, nicht aus deren Metadaten (`data-sources.md`).

## Datenstufe

Jeder Platz hat eine **Datenstufe** (`DataLevel`). Sie sagt, wie genau die Daten für den Platz sind. Sie ist unabhängig von der **Ausbaustufe** (`ContentLevel`, `cms.md`). Die Ausbaustufe sagt, wie gut der Platz dokumentiert ist. Ein Platz kann eine hohe Datenstufe und keine Dokumentation haben, oder umgekehrt.

| Stufe | Name | Bedingung | Was die Platzseite zeigt |
|---|---|---|---|
| 3 | Genau | Temperaturprofil am Platz (3D-Modell, nächster Gitterpunkt höchstens 500 m vom Platz, konfigurierbar) | Index, Profil mit Sprungschicht am Platz, Wind, Klarheit |
| 2 | Gewässer | Werte für das ganze Gewässer: Profil aus dem 1D-Modell (See) oder Wassertemperatur und Abfluss einer BAFU-Station (Fluss) | Index, Werte mit Hinweis «für den ganzen See» bzw. «Messstation {Name}» |
| 1 | Wetter | nur Wetter am Punkt, dazu Satellit, falls vorhanden | Index nur mit Satellitenwert, sonst Wind und Regen ohne Index |
| 0 | Keine | Prognose für den Platz ausgeschaltet (z.B. Hallenbad, Stausee mit Zutrittsverbot) | nur Dokumentation |

- Der Job bestimmt die Datenstufe bei jedem Lauf und speichert sie pro Tag (`DailyForecast.DataLevel`) und am Platz (`DiveSite.DataLevel`, letzter Lauf).
- Die Redaktion bestimmt die Datenstufe nicht. Sie kann nur die Prognose ausschalten (`DiveSite.ForecastDisabled`) und den Satellitenpunkt setzen.
- Wetter gibt es für jeden Punkt der Schweiz. Ein veröffentlichter Platz hat darum mindestens Stufe 1, ausser die Prognose ist ausgeschaltet.
- Die Datenstufe kann pro Tag sinken, wenn eine Quelle ausfällt.
- Die Rangliste «Wo tauchen?» zeigt nur Plätze mit Index. Plätze ohne Index erscheinen im Verzeichnis und auf der Gewässerseite.
- Ein Platz mit gültiger Sperrung (`dive-site.md`, «Hinweise») fehlt an diesem Tag in «Beste Wahl» und Rangliste. Der Index wird trotzdem berechnet.

## Eingaben pro Platz und Tag

Das Tauchfenster ist 08:00 bis 16:00 Uhr Lokalzeit.

| Eingabe | Quelle | Verwendung | Phase |
|---|---|---|---|
| Temperaturprofil am Platz | Alplakes 3D (`depthtime` an der Koordinate) | Temperatur auf 0/10/20/30 m, Sprungschicht | MVP |
| Temperaturprofil des Sees | Alplakes 1D Simstrat (Rückfall ohne 3D) | dito, seeweit | MVP |
| Wassertemperatur des Flusses | BAFU-Station des Flusses | Temperatur | MVP |
| Klarheit (Secchi-Schätzung) | Alplakes Satellit Sentinel-3 `Zsd_lee` am Satellitenpunkt | Klarheit | MVP |
| Algen (Chlorophyll) | Alplakes Satellit Sentinel-3 `chla` am Satellitenpunkt | Abzug bei Klarheit | MVP |
| Wind (Mittel, Böen, Richtung) | Open-Meteo (ICON-CH1, ICON-CH2) | Wind | MVP |
| Regen der letzten 72 h | SwissMetNet gemessen (vergangene Stunden), Open-Meteo (künftige Stunden) | Zuflüsse | MVP |
| Abfluss der Zuflüsse bzw. des Flusses | BAFU | Zuflüsse | MVP |
| Trübung des Flusses | BAFU, wo gemessen | Klarheit im Fluss | MVP |
| Gemessenes Temperaturprofil | Eawag Datalakes | ersetzt das Modell für heute, wo eine Messstation nah ist | Phase 1 |
| Abflussprognose | BAFU-Vorhersagen | Zuflüsse an künftigen Tagen | Phase 1, nach Klärung |

Die Endpunkte stehen in `data-sources.md`.

## Abgeleitete Werte

### Sprungschicht

- Aus dem Temperaturprofil die Schicht mit dem stärksten Temperaturabfall bestimmen.
- `ThermoclineTop` und `ThermoclineBottom`: Grenzen, an denen der Gradient den Schwellwert überschreitet (Standard 0.5 °C pro m, konfigurierbar).
- Kein Gradient über dem Schwellwert (typisch im Winter): keine Sprungschicht. Der See ist durchmischt.
- Das 1D-Modell liefert die Sprungschicht als eigene Variable (`Thermocline`). Der Job bestimmt sie trotzdem selbst aus dem Profil. So gilt für beide Modelle dieselbe Regel.

### Regen der letzten 72 h

- Summe der 72 Stunden **vor** dem Prognosetag, ohne den Tag selbst.
- Vergangene Stunden aus der Messung (SwissMetNet), künftige aus der Prognose.

### Klarheit

Gemessene Secchi-Tiefen gibt es über Alplakes nicht mehr (Spike 17.10.2026). Die Klarheit stützt sich auf den Satellit. Sie ist **keine Prognose**, sondern der letzte beobachtete Wert, korrigiert um das, was seither geschah.

- **Satellitenpunkt:** Der Job fragt den Satellit an einem Punkt seewärts ab. Der Platz kann einen eigenen Punkt haben (`DiveSite.SatellitePoint`). Sonst gilt der Standardpunkt des Gewässers (`WaterBody.SatellitePoint`). Grund: Am Ufer fehlen die Pixel.
- **Höchstalter 14 Tage:** Ein Satellitenwert gilt höchstens 14 Tage (konfigurierbar). Älter: Die Klarheit ist unbekannt.
- **Korrektur:** Fällt seit dem Bild viel Regen oder steigt der Abfluss stark, sinkt die geschätzte Klarheit. Die Stützpunkte stehen in `Forecast:Rules`.
- **Ohne Wert:** Die Klarheit ist unbekannt. Ihr Gewicht geht auf die anderen Teilwerte.
- **Anzeige:** Die Seite zeigt immer das Datum des Satellitenbilds («Satellit vom 05.10.: ca. 6 m»).
- **Flüsse:** Wo eine BAFU-Station Trübung misst, ersetzt die Trübung den Satellitenwert.

### Sicht-Indikator

- Grundlage: die Klarheit.
- Unter der Sprungschicht ist die Sicht im Sommer meist besser. Dann setzt der Algorithmus `VisibilityGoodBelow = ThermoclineBottom`.
- Ergebnis als Stufe: `Poor`, `Moderate`, `Good`, `VeryGood`, `Unknown`.

## Wetter pro Stunde

Die Platzseite zeigt unter den fünf Tagen eine Grafik mit dem Wetter pro Stunde (`ux.md`, Tauchplatzseite). Sie ist eine Information und fliesst nicht in den Index.

- **Zeitraum:** heute 00:00 bis Ende des fünften Tags, 120 Stunden, Lokalzeit.
- **Werte:** Temperatur 2 m, Regen, Sonnenminuten, Wetterlage als Symbol, Tag oder Nacht, Wind, Böen, Windrichtung.
- **Modell pro Wert:** Jede Stunde nimmt jeden Wert aus dem feinsten Modell, das ihn hat (ICON-CH1, dann ICON-CH2, dann DWD ICON). Fehlt er überall, bleibt er leer.
- **Symbol:** Wettercode nach WMO 4677: 0 klar, 1–2 teilweise bewölkt, 3 bedeckt, 45/48 Nebel, 51–57 Niesel, 61–67 Regen, 80–82 Schauer, 71–77 und 85–86 Schnee, 95–99 Gewitter. Die Reihenfolge ist die Schwere. Ein unbekannter Code ergibt kein Symbol.
- **Speicher:** `SiteWeatherOutlook` pro Platz, bei jedem Lauf ersetzt (`datastructure.md`).

## Der regelbasierte Index (`rules-1.0`)

Vier Teilwerte, je 0 bis 10. Der Index ist der gewichtete Mittelwert.

| Teilwert | Gewicht | 10 Punkte | 1 Punkt |
|---|---|---|---|
| Klarheit | 35 % | Secchi-Schätzung ≥ 10 m, wenig Algen | < 2 m oder starke Algenblüte |
| Wind | 25 % | 0–2 Bft im Tauchfenster | ≥ 5 Bft |
| Zuflüsse | 20 % | < 5 mm Regen in 72 h und Abfluss unter 1.2 × Median | Starkregen oder Abfluss über 3 × Median |
| Schichtung | 20 % | klare Schicht unter einer Sprungschicht ≤ 20 m, oder durchmischter See mit wenig Algen | Sprungschicht tiefer als 30 m bei trüber Oberfläche |

**Startwerte `rules-1.0`** (18.10.2026, ohne Messung, im Code `DiveIndexRules`):

| Regel | Stützpunkte |
|---|---|
| Klarheit | Secchi 2 m → 1, 10 m → 10. Mittlere Algen −2 Punkte. Algenblüte höchstens 3 Punkte. |
| Wind | 2 Bft → 10, 5 Bft → 1 |
| Zuflüsse | Regen 72 h 5 mm → 10, 40 mm → 1. Abfluss 1.2 × Median → 10, 3 × Median → 1. Der tiefere Wert zählt. |
| Schichtung | Untergrenze der Sprungschicht 20 m → 10, 30 m → 1. Bei Klarheit ab 7 Punkten mindestens 6. Durchmischter See: wenig Algen 10, mittel 6, Blüte 3. Fluss: entfällt. |
| Algen | Chlorophyll unter 4 mg/m³ wenig, unter 10 mittel, sonst Blüte |
| Korrektur Klarheit | Regen seit dem Bild 5 mm → 100 %, 30 mm → 60 %. Abfluss 1.2 × Median → 100 %, 3 × Median → 60 %. Der tiefere Wert zählt. |
| Sicht-Indikator | ab 10 m sehr gut, ab 6 m gut, ab 3 m mässig, sonst schlecht |
| Datenstufe 3 | Gitterpunkt des 3D-Modells höchstens 500 m vom Platz |

Diese Werte sind Annahmen. Meldungen und eigene Tauchgänge prüfen sie. Zwischenwerte linear interpolieren. Die Stützpunkte und Gewichte stehen in `appsettings.json` (`Forecast:Rules`). So lassen sie sich ohne Code anpassen. Jede Änderung erhöht die `ModelVersion` (`rules-1.1`, …).

Für Flussplätze gilt dieselbe Formel. Die Schichtung fällt weg (Fluss ist durchmischt). Der Abfluss zählt doppelt: einmal als Zufluss, einmal als Strömung (über 1.5 × Median gibt es Abzug). Die Werte stehen ebenfalls in `Forecast:Rules`.

**Fehlende Daten:** Fehlt ein Teilwert, verteilt der Algorithmus sein Gewicht auf die anderen. Der Index braucht den Wind **und** Wasserdaten (Klarheit, Profil oder Flussstation). Fehlt eines davon, ist der Index `null`. Die Seite zeigt dann die vorhandenen Werte ohne Index. An Tagen ohne Wasserdaten heisst das «Ausblick».

**Begründung:** Der Algorithmus speichert `ReasonCodes` (z.B. `CalmLake`, `ClearBelowThermocline`, `TurbidAfterRain`, `WindAfternoon`). Die Oberfläche bildet daraus einen Satz aus den `.resx`-Ressourcen. So ist die Begründung in allen vier Sprachen verfügbar.

**Temperatur ist kein Abzug.** Kaltes Wasser ist in der Schweiz normal. Die Temperatur in der Tiefe erscheint als Information, nicht im Index.

## Der Job

Der Job läuft als `BackgroundService` im Web-Host (ADR-006). Er ruft nur Handler des Core.

- **Zeitplan:** täglich 04:30 Lokalzeit (vollständiger Lauf) und 13:00 Lokalzeit (nur Wetter und Messwerte neu). Konfigurierbar.
- **Dauer:** Alplakes braucht 5–30 s pro Call. Bei 100 Plätzen mit 3D-Abdeckung dauert ein Lauf darum einige Minuten. Das ist für einen Job am frühen Morgen genug.
- **Lock:** nur ein Lauf gleichzeitig (Blob Lease `locks/forecast`, 60 s, alle 20 s erneuert). Hält ein anderer Lauf den Lock, endet der zweite sofort.
- **Lokal:** `Forecast__RunOnStartup` startet einen Lauf beim Start der Web-App (Aspire), ausser ein Lauf ohne Fehlschlag ist jünger als `Forecast__StartupMinAge` (Standard 6 h). So kostet nicht jeder Neustart rund 900 Calls.
- **Profil eines Tages:** 12 Uhr Lokalzeit, höchstens 3 h vom nächsten Zeitschritt des Modells.
- **Ablauf (Command `RunForecast`):**
  1. `ForecastRun` mit Status `Running` anlegen.
  2. Pro aktivem Gewässer: Abdeckung der Alplakes-Modelle laden (3D und 1D).
  3. Pro Platz mit 3D-Abdeckung: Temperatur über Tiefe und Zeit mit **einem** Call holen (`depthtime`, ganzer Prognosezeitraum). Pro See ohne 3D: ein Call auf das 1D-Modell.
  4. Pro Satellitenpunkt: Secchi und Chlorophyll der letzten 14 Tage.
  5. Pro Gitterpunkt: Wetterprognose (Open-Meteo, ICON-CH1, ICON-CH2, DWD ICON, je 3 Tage zurück). Plätze im selben Gitterpunkt (zwei Dezimalen) teilen sich den Call. Pro zugeordneter Station: gemessener Wind und Regen (SwissMetNet). **Stand 18.10.2026:** SwissMetNet fehlt noch. Der Regen der vergangenen Tage kommt aus `past_days` von Open-Meteo.
  6. Pro Gewässer: Abfluss, Wassertemperatur und Trübung der zugeordneten BAFU-Stationen.
  7. Rohdaten als JSON in Blob Storage ablegen (`forecast-raw/{date}/{runId}/{source}/{key}.json`).
  8. Datenstufe und Index pro Platz und Tag berechnen. `DailyForecast` und `WaterBodyConditions` schreiben (Upsert). Das Wetter pro Stunde in `SiteWeatherOutlook` ersetzen.
  9. Output Cache der öffentlichen Seiten invalidieren (Tag `forecast`).
  10. `ForecastRun` abschliessen (`Succeeded`, `PartiallySucceeded` oder `Failed`).
- **Höflich gegenüber den Quellen:** höchstens 2 Anfragen pro Sekunde und Quelle. Wiederholung mit Backoff (Polly über `Microsoft.Extensions.Http.Resilience`). Eindeutiger `User-Agent` mit Kontaktadresse.
- **Ausfall einer Quelle:** Der Lauf schreibt die übrigen Werte. Die letzten gültigen Werte der ausgefallenen Quelle bleiben bis zu 48 h gültig. Die Seite zeigt das Datenalter.

## Phase 2: eigenes Modell

### Labels

- **Meldungen** der Community (Sicht pro Tiefenband).
- **Gemessene Secchi-Tiefen** (Datalakes LéXPLORE, später kantonale Messungen).
- **Historische Labels von Marc:** Tauchlogs sowie Datum und Ort aus den EXIF-Daten der Fotos (tauchfotos.ch). Dazu eine Sichtbewertung von Marc pro Tauchgang.

### Features

Dieselben Eingaben wie oben. Für historische Labels liefert Alplakes Vergangenheitsdaten (1D-Modelle ab 1981). So lassen sich auch alte Tauchgänge mit Features versehen.

### Modell

- **ML.NET mit LightGBM** (`Microsoft.ML.LightGbm`). Ziel: Sichtklasse pro Tiefenband (Klassifikation) oder Secchi-Schätzung (Regression).
- Kein Python nötig. Das Training läuft lokal in Sekunden bis Minuten. Keine GPU, keine laufenden Kosten.
- Training über einen Admin-Command oder ein CLI-Projekt (`Tauchplaetze.Cli`, Fassade wie bei bau-ticker).
- Das Modell liegt versioniert in Blob Storage (`models/visibility-{version}.zip`). Der Job lädt die aktive Version.

### Wechsel von Regeln zu Modell

- Backtesting über die letzten Monate: Trefferquote gegenüber Meldungen und gemessenen Secchi-Tiefen.
- Das Modell ersetzt die Regeln erst, wenn es klar besser ist. Bis dahin läuft es im Schatten mit (beide Werte speichern, nur Regeln anzeigen).
- Die Teilwerte für die Begründung bleiben auch mit Modell erhalten (Feature-Beiträge oder weiter die Regeln für den Text).

## Tests

- Unit Tests für Sprungschicht-Erkennung, Teilwerte, Interpolation, fehlende Daten, Gewichtsverteilung, Datenstufe, Alter des Satellitenwerts, Ausblick ohne Index.
- Feste Beispielprofile (Sommer mit Sprungschicht, Winter durchmischt, nach Starkregen) als Testdaten. Dazu die echten Antworten aus dem Spike 0.5.
- Kein Test gegen die echte API in der CI. Ein manueller Smoke Test (`dotnet run -- forecast smoke`) prüft die Anbindung.

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
| Klarheit (Secchi-Schätzung) | Alplakes Satellit Sentinel-2 `Z490` in Ufernähe, Rückfall Sentinel-3 `Zsd_lee` am Satellitenpunkt | Klarheit | MVP |
| Algen (Chlorophyll) | Alplakes Satellit Sentinel-3 `chla` am Satellitenpunkt | Abzug bei Klarheit | MVP |
| Wind (Mittel, Böen, Richtung) | Open-Meteo (ICON-CH1, ICON-CH2) | Wind | MVP |
| Strömung im Tauchbereich (`u`, `v`) | Alplakes 3D-Modell am Platz | Information, kein Teilwert | MVP |
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

- **Zwei Quellen (`rules-1.2`, 19.10.2026):**
  - **Sentinel-2 `Z490` in Ufernähe** (10 m Pixel, Hauptquelle): zuerst an einem Punkt 50 m vom Platz Richtung Satellitenpunkt des Gewässers (sonst Mitte des Gewässers), mit `window=2`. Viele Plätze liegen im Bereich bis 50 m vom Ufer (Marc, 19.10.2026). Ist dort kein Wert, hat der neueste Wert weniger als 3 Pixel (`count`) oder ist er älter als 5 Tage, folgt ein zweiter Versuch bei 100 m. Von beiden gilt der jüngere Wert; bei gleichem Datum der nahe, ausser er hat zu wenige Pixel und der ferne genug. Laut Eawag gilt jedes gelieferte Pixel als gültig; die Mindestzahl schützt vor einem Wert aus einzelnen Randpixeln. Grund für den Abstand: Auf der Koordinate eines Platzes am Ufer ist das Pixel meist als Land maskiert. Ein eigener `DiveSite.SatellitePoint` (für Sentinel-3 seewärts gesetzt) gibt für Sentinel-2 nur die Richtung, er ist nicht selbst der Abfragepunkt. Liefern 50 und 100 m nichts, schreibt der Lauf eine Information ins Log (Slug des Platzes), damit sich solche Plätze finden lassen. Ein Punkt ohne Pixel kommt von Alplakes als `200` mit leerer Liste und gilt nicht als Fehler. Ist kein Wert da, gilt Sentinel-3. Test 19.10.2026 bei Riff Williams: 50 m mit `window=1` leer, mit `window=2` 3 Pixel und 5.4 m; 100 m 18 Pixel und 5.5 m. Nur für Seen, die `Z490` laut `remotesensing/metadata` haben. `Z490` ist die Secchi-Tiefe in m, vergleichbar mit `Zsd_lee` (Eawag, 19.10.2026).
  - **Sentinel-3 `Zsd_lee` in der Seemitte** (300 m Pixel, Rückfall): am Satellitenpunkt des Platzes bzw. des Gewässers, mit Uferfaktor.
- **Wahl:** Sentinel-2 gilt, solange es frisch ist (höchstens 5 Tage). Sonst gilt der jüngere gültige Wert der beiden, bei gleichem Alter Sentinel-2. Ist keiner höchstens 14 Tage alt, ist die Klarheit unbekannt; die Seite zeigt das Datum des jüngsten Bilds. Gespeichert in `DailyForecast.ClaritySource`.
- **Alter:** Ein Satellitenwert zählt bis 5 Tage voll. Danach nähern sich seine Punkte linear bis Tag 14 dem vorsichtigen Wert von 4 Punkten (ein schlechterer Wert wird durch das Alter nicht besser). Älter als 14 Tage: Die Klarheit ist unbekannt. Begründung `ClarityStale` ab Tag 6.
- **Ufernähe:** Der Satellitenpunkt von Sentinel-3 liegt in der Seemitte, getaucht wird in Ufernähe. Dort ist das Wasser meist trüber. Die Schätzung von Sentinel-3 gilt am Platz darum nur mit dem Uferfaktor 0.7. Sentinel-2 misst schon in Ufernähe und gilt ohne Faktor (Seite: «Satellit (Sentinel-2) vom 15.10.: ca. 5.4 m in Ufernähe»). Klarheit, Sicht-Indikator und die Regel zur Oberfläche rechnen mit dem Wert in Ufernähe. Die Seite zeigt beide («ca. 6.3 m in der Seemitte, in Ufernähe ca. 4.4 m»). Gespeichert in `DailyForecast.SecchiDepth` (Seemitte) und `SecchiDepthShore` (Ufernähe).
- **Korrektur:** Fällt seit dem Bild viel Regen oder steigt der Abfluss stark, sinkt die geschätzte Klarheit. Die Stützpunkte stehen in `Forecast:Rules`.
- **Ohne Wert:** Die Klarheit ist unbekannt und zählt vorsichtig (siehe «Fehlende Daten»).
- **Anzeige:** Die Seite zeigt immer das Datum des Satellitenbilds («Satellit vom 05.10.: ca. 6 m»).
- **Flüsse:** Wo eine BAFU-Station Trübung misst, ersetzt die Trübung den Satellitenwert.

### Sicht-Indikator

- Grundlage: die Klarheit in Ufernähe.
- Unter der Sprungschicht ist die Sicht im Sommer meist besser. Dann setzt der Algorithmus `VisibilityGoodBelow = ThermoclineBottom`.
- Ergebnis als Stufe: `Poor`, `Moderate`, `Good`, `VeryGood`, `Unknown`.

### Strömung

Die Strömung ist eine Information auf der Platzseite (Kachel «Strömung»). Sie fliesst nicht in den Index. Erst Erfahrungen zeigen, ab wann sie einen Tauchgang stört.

- **Quelle:** `u` (nach Osten) und `v` (nach Norden) in m/s aus demselben 3D-Call wie das Temperaturprofil. Nur Seen mit 3D-Modell. Flüsse: siehe Zuflüsse und Abfluss.
- **Tauchbereich:** alle Tiefen des Modells von 5 bis 30 m.
- **Pro Zeitschritt:** Geschwindigkeit = Mittel der Geschwindigkeiten aller Tiefen im Tauchbereich. So heben sich Strömungen in entgegengesetzter Richtung nicht auf. Die Richtung kommt aus dem Mittel der Vektoren.
- **Pro Tag:** der stärkste Zeitschritt im Tauchfenster 08–16 Uhr, wie beim Wind. Für einen Tauchgang zählt, wie stark es werden kann.
- **Richtung:** wohin das Wasser fliesst (Konvention der Ozeanografie), in Grad von Nord. Anders als beim Wind, dessen Richtung angibt, woher er weht. Die Kachel schreibt darum «nach Nordosten» und zeigt einen Pfeil in Fliessrichtung.
- **Einstufung (Annahmen vom 19.10.2026, `CurrentRules`):** ruhig unter 0.05 m/s, spürbar 0.05 bis 0.2 m/s, stark über 0.2 m/s. Anzeige in cm/s.
- **Speicher:** `DailyForecast.CurrentSpeed`, `CurrentDirection`, `CurrentLevel`. Ohne Werte im Tauchfenster bleiben sie leer, und die Kachel fehlt.
- Weil die Strömung nicht in den Index fliesst, ändert eine neue Schwelle die `ModelVersion` nicht.

## Wetter pro Stunde

Die Platzseite zeigt unter den fünf Tagen eine Grafik mit dem Wetter pro Stunde (`ux.md`, Tauchplatzseite). Sie ist eine Information und fliesst nicht in den Index.

- **Zeitraum:** heute 00:00 bis Ende des fünften Tags, 120 Stunden, Lokalzeit.
- **Werte:** Temperatur 2 m, Regen, Sonnenminuten, Wetterlage als Symbol, Tag oder Nacht, Wind, Böen, Windrichtung.
- **Modell pro Wert:** Jede Stunde nimmt jeden Wert aus dem feinsten Modell, das ihn hat (ICON-CH1, dann ICON-CH2, dann DWD ICON). Fehlt er überall, bleibt er leer.
- **Symbol:** Wettercode nach WMO 4677: 0 klar, 1–2 teilweise bewölkt, 3 bedeckt, 45/48 Nebel, 51–57 Niesel, 61–67 Regen, 80–82 Schauer, 71–77 und 85–86 Schnee, 95–99 Gewitter. Die Reihenfolge ist die Schwere. Ein unbekannter Code ergibt kein Symbol.
- **Speicher:** `SiteWeatherOutlook` pro Platz, bei jedem Lauf ersetzt (`datastructure.md`).

## Der regelbasierte Index (`rules-1.2`)

Vier Teilwerte, je 0 bis 10. Der Index ist der gewichtete Mittelwert.

| Teilwert | Gewicht | 10 Punkte | 1 Punkt |
|---|---|---|---|
| Klarheit | 35 % | Secchi-Schätzung ≥ 10 m, wenig Algen | < 2 m oder starke Algenblüte |
| Wind | 25 % | 0–2 Bft im Tauchfenster | ≥ 5 Bft |
| Zuflüsse | 20 % | < 5 mm Regen in 72 h und Abfluss unter 1.2 × Median | Starkregen oder Abfluss über 3 × Median |
| Schichtung | 20 % | klare Schicht unter einer Sprungschicht ≤ 20 m, oder durchmischter See mit wenig Algen | Sprungschicht tiefer als 30 m bei trüber Oberfläche |

**`rules-1.2`** (19.10.2026, Stand vor dem ersten Push): wie `rules-1.1` mit zwei Änderungen. Erstens die Quelle der Klarheit (Sentinel-2 in Ufernähe vor Sentinel-3, zuerst 50 m, sonst 100 m vor dem Ufer, siehe «Klarheit»). Zweitens volle Punkte für die Schichtung erst ab guter Sicht in Ufernähe (6 m): «oben naja, unten ok» soll keine vollen Punkte geben (Marc, 19.10.2026). Bei 3 bis unter 6 m und darunter höchstens 7 Punkte. Die Begründung «unten klarer» gilt weiterhin ab 3 m. Eawag bestätigte am 19.10.2026, dass `Z490` laufend verarbeitet wird und die Secchi-Tiefe in Metern ist, vergleichbar mit `Zsd_lee`. Pixel am Ufer entfernt Eawag selbst, jedes gelieferte Pixel gilt als gültig; am Zürichsee lag der Wert am 15.10. bei rund 5.4 m in Ufernähe, Sentinel-3 hatte nur einen Wert vom 05.10.

**Startwerte `rules-1.1`** (18.10.2026, ohne Messung, im Code `DiveIndexRules`). `rules-1.0` verteilte das Gewicht einer fehlenden Klarheit auf die anderen Teilwerte. So stieg der Index, wenn der Sichtwert fehlte, und eine trübe Oberfläche über klarem Tiefenwasser ergab volle Punkte. Marcs Erfahrung am Zürichsee im Oktober 2026 («oben naja, unten ok», seit Wochen) passte nicht dazu.

| Regel | Stützpunkte |
|---|---|
| Klarheit | Secchi in Ufernähe 2 m → 1, 10 m → 10. Mittlere Algen −2 Punkte. Algenblüte höchstens 3 Punkte. Alter: bis 5 Tage voll, bis Tag 14 linear zu 4 Punkten. Unbekannt: 4 Punkte. |
| Uferfaktor | Secchi aus der Seemitte (Sentinel-3) × 0.7 = Secchi in Ufernähe; Sentinel-2 ohne Faktor (`rules-1.2`) |
| Wind | 2 Bft → 10, 5 Bft → 1 |
| Zuflüsse | Regen 72 h 5 mm → 10, 40 mm → 1. Abfluss 1.2 × Median → 10, 3 × Median → 1. Der tiefere Wert zählt. |
| Schichtung | Untergrenze der Sprungschicht 20 m → 10, 30 m → 1. Bei Klarheit ab 7 Punkten mindestens 6. Durchmischter See: wenig Algen 10, mittel 6, Blüte 3. Volle Punkte nur mit frischem Satellitenwert (bis 5 Tage) und mindestens 6 m in Ufernähe («gut», `rules-1.2`; in `rules-1.1` ab 3 m). Sonst höchstens 7. Fluss: entfällt. |
| Algen | Chlorophyll unter 4 mg/m³ wenig, unter 10 mittel, sonst Blüte |
| Korrektur Klarheit | Regen seit dem Bild 5 mm → 100 %, 30 mm → 60 %. Abfluss 1.2 × Median → 100 %, 3 × Median → 60 %. Der tiefere Wert zählt. |
| Sicht-Indikator | Secchi in Ufernähe ab 10 m sehr gut, ab 6 m gut, ab 3 m mässig, sonst schlecht |
| Ohne frischen Sichtwert | Index höchstens 6.5 (Satellitenwert unbekannt oder älter als 5 Tage) |
| Datenstufe 3 | Gitterpunkt des 3D-Modells höchstens 500 m vom Platz |

Diese Werte sind Annahmen. Meldungen und eigene Tauchgänge prüfen sie. Zwischenwerte linear interpolieren. Die Stützpunkte und Gewichte stehen in `appsettings.json` (`Forecast:Rules`). So lassen sie sich ohne Code anpassen. Jede Änderung erhöht die `ModelVersion` (`rules-1.1`, …). **Stand 18.10.2026:** Die Werte stehen erst im Code (`DiveIndexRules`), die Bindung an `Forecast:Rules` fehlt noch. Bei der Bindung gilt: `SatelliteFullAgeDays` muss kleiner sein als `SatelliteMaxAgeDays`, sonst wirft die Berechnung (`DiveIndexRules.AgingDays`).

Für Flussplätze gilt dieselbe Formel. Die Schichtung fällt weg (Fluss ist durchmischt). Der Abfluss zählt doppelt: einmal als Zufluss, einmal als Strömung (über 1.5 × Median gibt es Abzug). Die Werte stehen ebenfalls in `Forecast:Rules`.

**Fehlende Daten:**
- Der Index braucht den Wind **und** Wasserdaten (Klarheit, Profil oder Flussstation). Fehlt eines davon, ist der Index `null`. Die Seite zeigt dann die vorhandenen Werte ohne Index. An Tagen ohne Wasserdaten heisst das «Ausblick».
- **Fehlt nur die Klarheit**, zählt sie vorsichtig mit 4 Punkten, ihr Gewicht bleibt bei ihr. Ein Tag ohne Sichtwert zählt so wie eine Sicht von rund 4.7 m in Ufernähe (mässig) und erreicht höchstens 6.5. Das gilt auch für Flüsse ohne Trübungsmessung.
- **Alternder Satellitenwert:** Ab Tag 6 gelten dieselben Obergrenzen wie ohne Sichtwert (Index höchstens 6.5, Schichtung höchstens 7). So springt der Index an Tag 15, wenn der Wert unbekannt wird, nicht nach oben.
- Fehlen Zuflüsse oder Schichtung (Fluss), verteilt sich ihr Gewicht auf die anderen Teilwerte.

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
  4. Pro Satellitenpunkt: Secchi und Chlorophyll (Sentinel-3) der letzten 30 Tage. Einmal pro Lauf die Produkte pro See (`remotesensing/metadata`), dann pro Platz Secchi von Sentinel-2 in Ufernähe der letzten 30 Tage. Plätze am selben Punkt teilen den Call.
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

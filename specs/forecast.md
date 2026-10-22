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
- **Profil am Platz (22.10.2026):** Am Ufer liegt der nächste Gitterpunkt des 3D-Modells oft im Flachwasser. Reicht sein Profil nicht bis 30 m (`ModelPoint.MinDepth`), fragt der Lauf zusätzlich Punkte 100 m und 250 m Richtung Seemitte ab (Richtung wie bei Sentinel-2, siehe «Klarheit»). Es gilt das tiefste Profil. Die Distanz zum Gitterpunkt zählt für die Datenstufe ab dem Platz (Distanz draussen plus 100 bzw. 250 m). Bei Tellskapelle Sisikon endete das Profil am Platz bei 1.5 m: keine Werte auf 10, 20 und 30 m, keine Sprungschicht. 100 m draussen reicht es bis 79 m: 15.1 °C bis 15 m, 12.9 °C auf 20 m, 8.2 °C auf 30 m.
- Die Rangliste «Wo tauchen?» zeigt nur Plätze mit Index. Plätze ohne Index erscheinen im Verzeichnis und auf der Gewässerseite.
- Ein Platz mit gültiger Sperrung (`dive-site.md`, «Hinweise») fehlt an diesem Tag in «Beste Wahl» und Rangliste. Der Index wird trotzdem berechnet.

### Rangliste

Entscheid Marc, 20.10.2026 (`RankingRules`, `GetBestSites`). Die Reihung ändert nie den angezeigten Index.

- **Ohne Ort:** Reihung nach Index.
- **Mit Ort:** Reihung nach Index − 0.1 × (Distanz in km / 10), also 0.01 Punkte pro km. Bei 40 km Umkreis höchstens 0.4 Punkte. Ein naher Platz mit 7.8 steht so vor einem Platz mit 8.0 in 30 km.
- **Streuung:** höchstens 2 Plätze pro Gewässer in der Rangliste, die weiteren beim zweiten Platz dieses Gewässers. «Beste Wahl» ist der erste Platz der Reihung.
- **Bergsee:** ab 700 m ü. M. (`MountainLakes`, Höhe des Seespiegels in `WaterBody.Elevation`) nur als Notiz, keine Abwertung.

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
| Regen der letzten 72 h | Open-Meteo (vergangene Stunden über `past_days`, künftige aus der Prognose). SwissMetNet gemessen ist geplant | Zuflüsse | MVP |
| Abfluss der Zuflüsse bzw. des Flusses | BAFU | Zuflüsse, Kachel «Abfluss» an Flussplätzen | MVP |
| Trübung des Flusses | BAFU, wo gemessen | Klarheit im Fluss | geplant (die Spalte `Turbidity` gibt es, der Lauf füllt sie nicht) |
| Gemessenes Temperaturprofil | Eawag Datalakes | ersetzt das Modell für heute, wo eine Messstation nah ist | Phase 1 |
| Abflussprognose | BAFU-Vorhersagen | Kachel «Abfluss» und Tagesleiste an Flussplätzen, Zuflüsse an künftigen Tagen (`rules-1.4`, seit 20.10.2026) | Phase 1 |

Die Endpunkte stehen in `data-sources.md`.

## Abgeleitete Werte

### Sprungschicht

- Aus dem Temperaturprofil die Schicht mit dem stärksten Temperaturabfall bestimmen.
- `ThermoclineTop` und `ThermoclineBottom`: Grenzen, an denen der Gradient den Schwellwert überschreitet (Standard 0.5 °C pro m, konfigurierbar).
- Kein Gradient über dem Schwellwert (typisch im Winter): keine Sprungschicht. Der See ist durchmischt.
- **Oberflächenhaut (`rules-1.6`):** Punkte über 1 m Tiefe zählen nicht (`ThermoclineMinDepth`). Die Sonne erwärmt tagsüber die obersten Dezimeter. Das 3D-Modell `mitgcm` hat dort Schichten von 0.2 m und zeigt bis 1 °C pro m Abfall zwischen 0.1 und 0.3 m. Daraus wurde am 21.10.2026 bei Sisikon die Sprungschicht «0–0 m» statt 15–22 m. Der eigentliche Grund war der flache Gitterpunkt am Ufer (siehe «Profil am Platz»). Dieselbe Prüfung gilt beim Lesen (`StoredThermocline`): Eine gespeicherte Sprungschicht an der Oberfläche oder dünner als 1 m zeigen die Seiten nicht, ebenso kein «besser ab» und kein «unten klarer». Der gespeicherte Index bleibt bis zum nächsten Lauf.
- Eine Sprungschicht dünner als 1 m (`ThermoclineMinThickness`) oder über 1 m Tiefe gilt als keine. Das gilt auch für gespeicherte Werte bei der Neuberechnung.
- Das 1D-Modell liefert die Sprungschicht als eigene Variable (`Thermocline`). Der Job bestimmt sie trotzdem selbst aus dem Profil. So gilt für beide Modelle dieselbe Regel.

### Regen der letzten 72 h

- Summe der 72 Stunden **vor** dem Prognosetag, ohne den Tag selbst.
- Vergangene und künftige Stunden aus Open-Meteo. Die Messung von SwissMetNet für die vergangenen Stunden ist geplant, aber nicht angebunden.

### Klarheit

Gemessene Secchi-Tiefen gibt es über Alplakes nicht mehr (Spike 17.10.2026). Die Klarheit stützt sich auf den Satellit. Sie ist **keine Prognose**, sondern der letzte beobachtete Wert, korrigiert um das, was seither geschah.

- **Zwei Quellen (`rules-1.2`, 19.10.2026):**
  - **Sentinel-2 `Z490` in Ufernähe** (10 m Pixel, Hauptquelle): zuerst an einem Punkt 50 m vom Platz Richtung Satellitenpunkt des Gewässers (sonst Mitte des Gewässers), mit `window=2`. Viele Plätze liegen im Bereich bis 50 m vom Ufer (Marc, 19.10.2026). Ist dort kein Wert, hat der neueste Wert weniger als 3 Pixel (`count`) oder ist er älter als 5 Tage, folgt ein zweiter Versuch bei 100 m. Von beiden gilt der jüngere Wert; bei gleichem Datum der nahe, ausser er hat zu wenige Pixel und der ferne genug. Laut Eawag gilt jedes gelieferte Pixel als gültig; die Mindestzahl schützt vor einem Wert aus einzelnen Randpixeln. Grund für den Abstand: Auf der Koordinate eines Platzes am Ufer ist das Pixel meist als Land maskiert. Ein eigener `DiveSite.SatellitePoint` (für Sentinel-3 seewärts gesetzt) gibt für Sentinel-2 nur die Richtung, er ist nicht selbst der Abfragepunkt. Liefern 50 und 100 m nichts, folgen Punkte bei 100 m in gedrehter Richtung: 45° im Uhrzeigersinn, 45° dagegen, dann 90° und −90° (`ShorePoint.FallbackTurns`, 22.10.2026). Der erste brauchbare Wert (frisch, genug Pixel) beendet die Suche. Grund: Führt die Richtung zur Seemitte dem Ufer entlang, liegen beide Punkte auf maskierten Uferpixeln. Bei Terlinden zeigte sie nach Süden, die Sicht war darum «unbekannt»; 45° gedreht lieferte Sentinel-2 einen Wert vom 20.10. Liefert auch das nichts, schreibt der Lauf eine Information ins Log (Slug des Platzes), damit sich solche Plätze finden lassen. Ein Punkt ohne Pixel kommt von Alplakes als `200` mit leerer Liste und gilt nicht als Fehler. Ist kein Wert da, gilt Sentinel-3. Test 19.10.2026 bei Riff Williams: 50 m mit `window=1` leer, mit `window=2` 3 Pixel und 5.4 m; 100 m 18 Pixel und 5.5 m. Nur für Seen, die `Z490` laut `remotesensing/metadata` haben. `Z490` ist die Secchi-Tiefe in m, vergleichbar mit `Zsd_lee` (Eawag, 19.10.2026).
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

### Abfluss

An Flussplätzen zeigt die Platzseite die Kachel «Abfluss» (`ux.md`, Tauchplatzseite). Sie ist eine Information und fliesst nicht in den Index. Viel Abfluss heisst starke Strömung und oft trübes Wasser.

- **Station:** `DiveSite.InflowStationOverride`, sonst die nächste Station mit Rolle `River` und Lage am Fluss des Platzes (`WaterBodyHydroStation.Location`). Plätze an Seen haben keine Kachel. Der Seed ordnet jedem Fluss mit Plätzen die nächsten BAFU-Stationen am selben Fluss zu (höchstens 30 km vom Platz, Stand 19.10.2026: Rhein, Aare, Areuse, Limmat, Maggia, Orbe, Töss, Verzasca). Hagneckkanal, Creugenat, Riale del Salto und Steinenbach haben keine Station.
- **Wassertemperatur:** Derselbe Call holt `temperature`. Die Kachel «Wassertemperatur» zeigt den neuesten Wert mit Messzeit. Misst die Station keine Temperatur, bleibt sie leer (nie 0) und die Kachel fehlt. Fehlt sie in einem Call, bleibt der letzte Wert und gilt nach 2 h als veraltet. Ein Tiefenprofil gibt es an Flüssen nicht.
- **Messwert:** der neueste 10-Minuten-Wert der Station mit Uhrzeit. Der Import läuft alle 30 Minuten. Ist der Wert älter als 2 h, zeigt die Kachel «Messwert veraltet».
- **Einordnung (`DischargeRules`):** sehr gering, gering, mittel, stark.
  - Mit Schwellen des Platzes (`DischargeLowFrom`, `DischargeMediumFrom`, `DischargeStrongFrom` in m³/s): unter der ersten sehr gering, ab der ersten gering, ab der zweiten mittel, ab der dritten stark. Nur die Moderation setzt sie (`cms.md`).
  - Ohne Schwellen: Verhältnis zum Median der Stundenwerte der letzten 30 Tage. Unter 0.5 × sehr gering, unter 1 × gering, unter 2 × mittel, sonst stark.
  - Ohne Median und ohne Schwellen zeigt die Kachel nur den Wert.
- **Legende («i»):** die vier Stufen mit den Grenzen in m³/s und der Herkunft der Schwellen.
- **Trend:** neuester Wert gegen den Wert 3 h davor (höchstens 30 min daneben). Innerhalb von 5 % «gleich», sonst «steigt» oder «fällt». Die Kachel zeigt die Änderung in m³/s.
- **Verlauf:** die Stundenmittel der letzten 48 Stunden als Kurve, mit dem Median gestrichelt. Darunter die Tagesmittel der letzten 7 Tage (Schweizer Kalendertage, heute bis jetzt).
- **Vorhersage (BAFU, 20.10.2026):** `HydroHour.Kind = Forecast`, Stundenmittel bis 5 Tage voraus (`data-sources.md`, «Phase 1: Vorhersagen des BAFU»). Die Kachel setzt die Kurve der letzten 48 Stunden gestrichelt fort, bis zum Ende der Vorhersage, beschriftet «Vorhersage BAFU». Die Tagesleiste der Prognose zeigt an Flussplätzen pro Tag die Einordnung des vorhergesagten Tagesmittels (gleiche Regel wie der Messwert). Ohne Vorhersage fehlt beides. Ausgabezeit und Ordner (`official` oder `unofficial`) stehen in `HydroReading`.
- **Im Index (`rules-1.4`, 20.10.2026):** An künftigen Tagen rechnen der Teilwert «Zuflüsse» und die Korrektur der Klarheit mit dem vorhergesagten Tagesmittel geteilt durch den Median der letzten 30 Tage (siehe `rules-1.4`). Der Abfluss-Job holt dafür auch die Zuflussstationen der Seen.
- **Speicher:** `HydroHour` (Stundenmittel pro Station, 30 Tage) und `HydroReading` (neuester Wert mit Wert vor 3 h und Median). Die Seite liest nur diese Tabellen.
- Weil der Abfluss als Kachel nicht in den Index fliesst, ändert eine neue Schwelle die `ModelVersion` nicht. Das Verhältnis zum Median im Index (Zuflüsse) bleibt Teil von `RunForecast`.

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

**`rules-1.4`** (20.10.2026, Entscheid Marc): wie `rules-1.3`, dazu rechnen **künftige Tage mit der Abflussvorhersage des BAFU**. Heute zählt weiter der gemessene Abfluss (neuester Wert geteilt durch den Median der letzten 30 Tage). Ab morgen zählt das vorhergesagte Tagesmittel (Schweizer Kalendertag, mindestens 12 Stunden) geteilt durch denselben Median, für den Teilwert «Zuflüsse» und für die Korrektur der Klarheit. Bei mehreren Zuflüssen zählt der höchste Quotient. Ohne Vorhersage für den Tag bleibt der gemessene Wert wie in `rules-1.3`. Automatisch gerechnete Vorhersagen (`unofficial`) zählen wie kontrollierte; den Unterschied zeigt nur die Kachel. Grund: Bisher sah ein Tag mit vorhergesagtem Hochwasser gleich gut aus wie heute. Vergangene Tage ändern sich nicht, denn ihr gespeicherter Quotient bleibt. Zuflussstationen der Seen: `data-sources.md`, Liste in `doc/inflow-stations-vorschlag.md`.

**`rules-1.6`** (21.10.2026, Fehlerbehebung): wie `rules-1.5`. Die Sprungschicht zählt erst ab 1 m Tiefe und ab 1 m Dicke (siehe «Sprungschicht»). Vorher gab die warme Oberflächenhaut im 3D-Modell eine Sprungschicht «0–0 m», volle Punkte für die Schichtung und «besser ab 0 m».

**`rules-1.5`** (20.10.2026, Entscheid Marc): wie `rules-1.4` mit vier Änderungen.
1. **Zufluss nach Distanz zur Mündung:** Ein Zufluss zählt nicht mehr für den ganzen See, sondern nach der Distanz zwischen Platz und Mündung (`WaterBodyHydroStation.Mouth`). Bis 5 km zählt er voll, dann linear weniger, ab 15 km gar nicht (10 km: halb). Der Quotient am Platz ist `1 + Gewicht × (Quotient − 1)`. Bei mehreren Zuflüssen zählt der höchste. Erreicht kein Zufluss den Platz, zählt nur der Regen. Grund: Ein Hochwasser des Ticino trübt Magadino und Tenero, nicht Brissago; eines der Reuss den Urnersee, nicht Luzern. Becken sind nicht modelliert, nur die Distanz. Gilt für den Teilwert «Zuflüsse» und die Korrektur der Klarheit, heute und an künftigen Tagen.
2. **Median mindestens 1 m³/s:** Ein Bach mit fast keinem Wasser ergab sonst Quotienten von 10 und mehr (Aabach 2416 mit 0.01 m³/s). Gilt auch für die Einordnung der Kachel «Abfluss». Der Aabach ist aus dem Seed entfernt (`removedHydroStations`).
3. **Am Fluss die Station des Platzes:** Ein Flussplatz rechnet mit derselben Station wie seine Kachel «Abfluss» (`InflowStationOverride`, sonst die nächste Station am Fluss). Bisher zählte der höchste Quotient aller Stationen am Fluss.
4. **Strömung am Fluss:** Viel Wasser heisst starke Strömung. Ab 1.5 × Median zieht der Index linear ab, bis 3 Punkte bei 3 × Median, höchstens bis auf 1 (`RiverCurrentPenalty`). Die Begründung nennt «starke Strömung» (`StrongCurrent`). Am See gibt es keinen Abzug.

**`rules-1.3`** (19.10.2026, Entscheid Marc): wie `rules-1.2`, dazu **begrenzt die Sicht den Index**. Mässige Sicht in Ufernähe (3 bis unter 6 m) erlaubt höchstens 7.0, schlechte Sicht (unter 3 m) höchstens 5.0. Gute und sehr gute Sicht begrenzen nicht. Ohne Sichtwert bleibt die Obergrenze 6.5. Grund: Am Zürichsee ergaben 5–6 m in Ufernähe bei Windstille und klarer Sprungschicht 7.5 bis 8.5. Das ist für Taucher kein Top-Tag. Greift die Grenze, steht die Begründung `VisibilityLimitsIndex` («Sicht begrenzt den Index») vorne. Die Tageswerte werden beim nächsten Lauf neu gerechnet.

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
| Sicht begrenzt den Index | Mässig (3 bis unter 6 m in Ufernähe) höchstens 7.0, schlecht (unter 3 m) höchstens 5.0 (`rules-1.3`) |
| Datenstufe 3 | Gitterpunkt des 3D-Modells höchstens 500 m vom Platz |

Diese Werte sind Annahmen. Meldungen und eigene Tauchgänge prüfen sie. Zwischenwerte linear interpolieren. Die Stützpunkte und Gewichte stehen in `appsettings.json` (`Forecast:Rules`). So lassen sie sich ohne Code anpassen. Jede Änderung erhöht die `ModelVersion` (`rules-1.1`, …). **Stand 18.10.2026:** Die Werte stehen erst im Code (`DiveIndexRules`), die Bindung an `Forecast:Rules` fehlt noch. Bei der Bindung gilt: `SatelliteFullAgeDays` muss kleiner sein als `SatelliteMaxAgeDays`, sonst wirft die Berechnung (`DiveIndexRules.AgingDays`).

Für Flussplätze gilt dieselbe Formel. Die Schichtung fällt weg (Fluss ist durchmischt). Der Abfluss zählt doppelt: einmal als Zufluss, einmal als Strömung (über 1.5 × Median gibt es Abzug, seit `rules-1.5` umgesetzt: bis 3 Punkte bei 3 × Median). Die Werte stehen ebenfalls in `DiveIndexRules`.

**Fehlende Daten:**
- Der Index braucht den Wind **und** Wasserdaten (Klarheit, Profil oder Flussstation). Fehlt eines davon, ist der Index `null`. Die Seite zeigt dann die vorhandenen Werte ohne Index. An Tagen ohne Wasserdaten heisst das «Ausblick».
- **Fehlt nur die Klarheit**, zählt sie vorsichtig mit 4 Punkten, ihr Gewicht bleibt bei ihr. Ein Tag ohne Sichtwert zählt so wie eine Sicht von rund 4.7 m in Ufernähe (mässig) und erreicht höchstens 6.5. Das gilt auch für Flüsse ohne Trübungsmessung.
- **Alternder Satellitenwert:** Ab Tag 6 gelten dieselben Obergrenzen wie ohne Sichtwert (Index höchstens 6.5, Schichtung höchstens 7). So springt der Index an Tag 15, wenn der Wert unbekannt wird, nicht nach oben.
- Fehlen Zuflüsse oder Schichtung (Fluss), verteilt sich ihr Gewicht auf die anderen Teilwerte.

**Begründung:** Der Algorithmus speichert `ReasonCodes` (z.B. `CalmLake`, `ClearBelowThermocline`, `TurbidAfterRain`, `WindAfternoon`). Die Oberfläche bildet daraus einen Satz aus den `.resx`-Ressourcen. So ist die Begründung in allen vier Sprachen verfügbar. An Flussplätzen fallen die See-Gründe weg: `CalmLake`, `MixedLake` und `ClarityUnknown` (am Fluss gibt es keinen Satellitenwert).

**Temperatur ist kein Abzug.** Kaltes Wasser ist in der Schweiz normal. Die Temperatur in der Tiefe erscheint als Information, nicht im Index.

## Der Job

Der Job läuft als `BackgroundService` im Web-Host (ADR-006). Er ruft nur Handler des Core.

- **Zeitplan:** täglich 04:30 Lokalzeit (vollständiger Lauf) und 13:00 Lokalzeit (nur Wetter und Messwerte neu). Konfigurierbar.
- **Dauer:** Alplakes braucht 5–30 s pro Call. Ein voller Lauf mit 319 Plätzen dauert rund 22–25 Minuten. Die Drosselung von Open-Meteo auf 400 Punkte pro Minute kostet rund 1–2 Minuten zusätzlich, ein 429 wegen des Minutenlimits eine weitere Minute (`data-sources.md`, «Wetter: Open-Meteo»). Das ist für einen Job am frühen Morgen genug.
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
  9. Den Median des Index pro Gewässer für heute schreiben (`WaterBodyDailyIndex`, Upsert, siehe «Verlauf»).
  10. Output Cache der öffentlichen Seiten invalidieren (Tag `forecast`), sobald es ihn gibt (geplant, `architecture.md`, «Caching»).
  11. `ForecastRun` abschliessen (`Succeeded`, `PartiallySucceeded` oder `Failed`).
  Stoppt der Host während eines Laufs (Neustart, Deployment), bricht der Lauf sofort ab und gilt als `Failed`. Die übrigen Plätze behalten die Werte des letzten Laufs. Ein abgebrochener Call ist kein Teilerfolg (Fehler vom 19.10.2026: Nach einem Neustart scheiterten 357 Calls in 1 ms, und der Lauf überschrieb gute Werte mit leeren).
- **Höflich gegenüber den Quellen:** höchstens 2 Anfragen pro Sekunde und Quelle. Wiederholung mit Backoff (Polly über `Microsoft.Extensions.Http.Resilience`). Eindeutiger `User-Agent` mit Kontaktadresse.
- **Ausfall einer Quelle:** Der Lauf schreibt die übrigen Werte. Die letzten gültigen Werte der ausgefallenen Quelle bleiben bis zu 48 h gültig. Die Seite zeigt das Datenalter.

## Verlauf

Der Index über Wochen, Monate und Jahre (Entscheid Marc, 19.10.2026). Höchste Auflösung ist ein Tag.

- **Pro Platz:** Die Tage bis heute aus `DailyForecast`, höchstens 365. Pro Tag gilt die Prognose vom Morgen dieses Tages: Der Lauf überschreibt einen Tag, bis er vorbei ist. Die Seite zeigt dazu die Sicht in Ufernähe, den Wind und die Algen (Query `GetSiteIndexHistory`).
- **Pro Gewässer:** der Median des Index aller freigegebenen Plätze mit Index an diesem Tag, in `WaterBodyDailyIndex`. Jeder Lauf schreibt den Wert für heute (Upsert). Die Zeilen bleiben unbegrenzt, so trägt die Ansicht «Jahr über Jahr» auch nach dem Löschen alter `DailyForecasts` (Query `GetWaterBodyIndexHistory`).
- **Glättung:** gleitendes Mittel über 7 Kalendertage (3 davor, der Tag, 3 danach). Es zählen nur Tage mit Wert. Am Rand und bei Lücken mittelt es über weniger Tage. Die Rohwerte bleiben sichtbar.
- **Regelwechsel:** Ändert die `ModelVersion` von einem Tag zum nächsten, markiert das Diagramm den Tag mit einer senkrechten Linie. Davor und danach sind die Werte nicht direkt vergleichbar.
- **Neuberechnung** (Command `RecomputeForecastHistory`, Admin, `/platform/prognose`): rechnet jede gespeicherte Tagesprognose mit älterer `ModelVersion` mit der aktuellen Regel neu. Danach baut sie den Median pro Gewässer für alle vergangenen Tage neu auf. Sie braucht keine Rohdaten und keine externen Calls: Die Eingaben des Index stehen in der Zeile jedes Tages. Zwei Grenzen:
  - Die Sprungschicht bleibt die gespeicherte. Aus vier Punkten des Profils (0, 10, 20, 30 m) lässt sie sich nicht neu bestimmen. Eine neue Regel für die Sprungschicht wirkt darum erst ab dem nächsten Lauf.
  - Zeilen vor `rules-1.2` haben keinen Wert in Ufernähe. Dann gilt der Wert der Seemitte mal Uferfaktor, wie damals. Die Zeile erhält diesen Wert und die Quelle `Sentinel3Lake`, damit Verlauf und Mini-Diagramm die Sicht zeigen.
  - Die Neuberechnung setzt `ComputedAt` auf jetzt. `ForecastRunId` bleibt der Lauf, der die Eingaben holte.
  Sie hält den Lock `forecast` und läuft so nie neben einem Prognose-Lauf. Ein zweiter Aufruf findet nichts mehr zu rechnen und schreibt denselben Median.
- **Später:** Rückrechnung für die Jahre vor dem Start (`roadmap.md`, Phase 2).

## Der Abfluss-Job

Ein kleiner zweiter Job holt den Abfluss öfter als der Prognose-Lauf. Er ruft nur den Command `ImportDischarge` (`DischargeWorker`, ADR-006).

- **Zeitplan:** beim Start der Web-App und danach alle 30 Minuten.
- **Lock:** `locks/discharge`. Hält ein anderer Lauf den Lock, endet der zweite sofort.
- **Ein Call:** existenz.ch `hydro/daterange` mit allen Stationen der Flussplätze und `parameters=flow,temperature` (rund 15 Stationen). `startdate` ist ein UTC-Tag. Der Call holt ab der letzten gespeicherten Stunde, mindestens die letzten 4 Stunden. Eine neue Station holt 30 Tage nach.
- **Ablauf:** Stundenmittel schreiben (Upsert, die laufende Stunde mit den Werten bis jetzt), Stunden älter als 30 Tage löschen, pro Station den neuesten Wert mit Wert vor 3 h, Median der 30 Tage und, wo gemessen, die neueste Wassertemperatur schreiben.
- **Rohdaten:** `forecast-raw/discharge/{date}/{HHmm}/hydro/flow-temperature.json`.
- **Ausfall:** Scheitert der Call, bleiben die letzten Werte. Die Kachel zeigt sie mit Uhrzeit und nach 2 h als veraltet.

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

# Datenquellen

Diese Spec listet alle externen Datenquellen. Regel: **Keine Seite ruft eine externe Quelle während einer Anfrage auf.** Nur der Job importiert. Die Seiten lesen die eigene Datenbank.

Stand: 17.10.2026. Grundlage: Spike 0.5 (`doc/spikes/0.5-datenquellen.md`) und eine Recherche vom selben Tag. Live geprüft ist, was in der Spalte «Geprüft» ein ✅ hat.

Das Projekt ist **nicht kommerziell** (`vision.md`, «Finanzierung»). Mehrere Quellen erlauben die freie Nutzung nur unter dieser Bedingung (ADR-007).

## Übersicht

| Quelle | Inhalt | Nutzen | Phase | Geprüft |
|---|---|---|---|---|
| Eawag Alplakes API | Seemodelle 3D und 1D (Temperatur nach Tiefe und Zeit), Satellit (Secchi, Chlorophyll) | Prognose Temperatur und Sprungschicht, Klarheit | MVP | ✅ Spike |
| Open-Meteo (Modelle von MeteoSchweiz, DWD, ECMWF) | Wetterprognose am Punkt, stündlich | Prognose Wind und Regen | MVP | ✅ Spike |
| MeteoSchweiz SwissMetNet | gemessener Wind, Böen, Niederschlag, alle 10 min | aktueller Stand, Regen der letzten Tage | geplant | Nutzung geklärt, nicht angebunden. Heute kommt der Regen der letzten 72 h aus Open-Meteo (`past_days`) |
| BAFU Hydrodaten (über existenz.ch, Ziel: BAFU-Datenplattform) | Abfluss, Pegel, Wassertemperatur, an einigen Stationen Trübung | Zuflüsse, Flussplätze | MVP | ✅ Spike |
| swisstopo | Kartenkacheln, Gewässernetz (swissTLM3D), Bathymetrie (swissBATHY3D) | Karte, Stammdaten aller Gewässer, Tiefenkarten | MVP | ✅ |
| Eawag Datalakes | In-situ-Messungen: Temperaturketten, Bojen, Profile, Secchi (LéXPLORE) | aktueller Stand, Kontrolle der Modelle | Phase 1 | ✅ |
| BAFU Abfluss- und Seepegelvorhersagen | Prognose für etwa 50–60 Stationen | Prognose für Flussplätze | Phase 1 | ✅ Zugang HTTPS |
| OpenStreetMap (Overpass API) | Tauchplätze (`sport=scuba_diving`) mit Name und Koordinate, Gewässer, Gemeinde, Kanton | Erstbestand des Verzeichnisses | MVP | ✅ Skript |
| Eigenes Archiv (tauchfotos.ch, tauchvideos.ch) | Fotos, Videos | Dokumentation | MVP | Rechte bei Marc |

## Eawag Alplakes

Alplakes ist eine Plattform der Eawag (Abteilung Oberflächengewässer). Sie verbindet Seemodelle mit Satellitendaten für über 200 Seen im Alpenraum. Alplakes erhielt 2025 den Nationalen Preis für offene Forschungsdaten.

Der Code der API ist öffentlich: `github.com/eawag-surface-waters-research/alplakes-fastapi`. Die API heisst «Alplakes API» (Version 2.0.0). Basis-URL: `https://alplakes-api.eawag.ch`.

**Die API hat keine Endpunkte `/insitu`, `/meteoswiss` und `/bafu` mehr** (Spike 17.10.2026). Gemessene Secchi-Tiefen gibt es über die API nicht mehr. Wetter und Abfluss kommen aus eigenen Quellen.

### Abdeckung (Stand 17.10.2026)

| Art | Seen |
|---|---|
| 3D-Modell (Wert an der Koordinate des Platzes) | `delft3d-flow`: Ägerisee, Bielersee, Genfersee, Greifensee, Hallwilersee, Lago di Lugano, Murtensee, St. Moritzersee, Zürichsee (dazu Annecy, Bourget, Caldonazzo, Garda, Joux). `mitgcm`: Vierwaldstättersee, Neuenburgersee, Zugersee, Zürichsee |
| 1D-Modell Simstrat (ein Profil pro See oder Becken) | rund 130 Seen, u.a. Walensee, Thunersee, Brienzersee, Sempachersee, Sarnersee, Lago Maggiore, Bodensee (`upperconstance`, `lowerconstance`), Zürichsee (`lowerzurich`, `upperzurich`), Vierwaldstättersee in vier Becken |
| Satellit Sentinel-3 (`Zsd_lee`, `chla`) | alle grossen Seen |
| Satellit Sentinel-2 | **genutzt seit `rules-1.2`:** `Z490` (Secchi, 10 m Pixel, laufend verarbeitet laut Eawag 19.10.2026), z.B. Zürichsee 15.10.2026. Das ältere `p3qaa_zSD` endet im November 2023. Auf der Koordinate eines Platzes am Ufer oft leer (Pixel maskiert), rund 100 m seewärts mit `window=2` vorhanden. |

Der Job liest die Abdeckung aus `GET /simulations/metadata` und `GET /simulations/1d/metadata`. Die Zuordnung eines Gewässers zu den Modellen steht in `WaterBody` (`datastructure.md`).

**Angebundene Seen (Seed, 19.10.2026, aus den drei Metadaten-Endpunkten, gespeichert unter `TestData/alplakes/metadata-*-20261019.json`):**

| Art | Seen |
|---|---|
| 3D-Modell | Zürichsee, Genfersee, Luganersee, Bielersee, Ägerisee, Greifensee, Hallwilersee (`delft3d-flow`); Zugersee, Vierwaldstättersee, Neuenburgersee (`mitgcm`) |
| 1D-Modell Simstrat | die Seen oben und Thuner-, Brienzer-, Walen-, Silser-, Silvaplaner-, Sarner-, Lungerer-, Baldegger-, Pfäffiker-, Sihl-, Türler-, Klöntaler-, Burgäschi-, Champfèrer-, Oeschinen- und Greyerzersee, Lago Maggiore, Lago di Cadagno, Lago di Tomé, Lac Brenet, Lac Lioson. Mehrere Becken: Vierwaldstättersee `lucernekreuztrichterandvitznauerbecken`, Luganersee `upperlugano` |
| Nur Satellit (Sentinel-2) | Stauseen und Bergseen mit Plätzen, z.B. Davoser-, Arnen-, Sufner-, Tannen-, Schiffenen-, Engstlen-, Melch-, Marmorera-, Seealpsee, Lago di Vogorno, Lago del Sambuco, Lac de l'Hongrin, Moiry, Montsalvens, Taney, Ritom, Sanetsch, Garichti, Gigerwald |

- Der Satellitenpunkt (Sentinel-3) liegt im offenen Wasser. Jeder Punkt ist mit einem Call geprüft (Werte im September/Oktober 2026). Ohne Satellitenpunkt oder ohne `Zsd_lee` im Angebot fragt der Lauf Sentinel-3 nicht ab, denn am Ufer liefert es nichts. Silsersee und die kleinen Seen haben darum nur Sentinel-2.
- Ohne Schlüssel bleiben Seen ohne Angebot: u.a. Alter Rhein, Blausee, Caumasee, Arnisee, Trüebsee, Talalpsee, Heidsee, Muslen, Lac de Géronde, Espi, Duzillet, Moesola, Lago Scuro, Lej da las Culuonnas, Mapragg, Wägitalersee, Stille Reuss.
- Zuflussstationen (BAFU, Rolle `Inflow`, seit 20.10.2026 für `rules-1.4`): Zürich-, Zuger-, Bieler-, Brienzer-, Thuner-, Genfer-, Hallwiler-, Greyerzer-, Schiffenen-, Neuenburger-, Vierwaldstätter- und Walensee, Lago Maggiore und Luganersee, je 1–2 Hauptzuflüsse. Liste und Begründung: `doc/inflow-stations-vorschlag.md` (von Marc zu prüfen). Ohne Station zählt nur der Regen als Zufluss. Seit `rules-1.5` hat jeder Zufluss die Lage seiner Mündung (Seed `mouthLatitude`/`mouthLongitude`); ein Zufluss zählt nur für Plätze bis 15 km von der Mündung. Der Hallwilersee hat keinen Zufluss mehr (Aabach 2416, Median 0.01 m³/s, entfernt am 20.10.2026).
- **Calls pro Lauf** (Schätzung 19.10.2026, 319 Plätze): bisher rund 210 an Alplakes (69 Plätze mit 3D-Modell, Satellit, 2 Simstrat), neu rund 650: 3D-Modell an 136 Plätzen, Simstrat an 31 Seen, Sentinel-2 an rund 260 Plätzen (1–2 Calls), Sentinel-3 an 18 Seen (2 Calls pro Satellitenpunkt). Dauer: `mitgcm` braucht rund 18 s pro Call, Vierwaldstättersee (31 Plätze) und Neuenburgersee (6) kosten damit rund 11 Minuten. Der ganze Lauf dauert geschätzt 20–25 Minuten.

### Endpunkte

| Endpunkt | Zweck |
|---|---|
| `GET /simulations/metadata` | 3D-Modelle und Seen |
| `GET /simulations/depthtime/{model}/{lake}/{start}/{end}/{lat}/{lng}` | Temperatur über Tiefe und Zeit am Gitterpunkt nächst der Koordinate. **Hauptquelle** (ein Call pro Platz). Zeiten als `yyyyMMddHHmm` (UTC). |
| `GET /simulations/1d/metadata` | 1D-Modelle, Seen, Zeitraum, Variablen |
| `GET /simulations/1d/depthtime/simstrat/{lake}/{start}/{end}` | Profil über Tiefe und Zeit für den ganzen See. Enthält die Variable `Thermocline`. |
| `GET /remotesensing/metadata` | Satellitenprodukte pro See. Leitet auf eine Datei in S3 weiter. Einmal pro Lauf. |
| `GET /remotesensing/products/{lake}/{satellite}/{variable}?start_date=yyyyMMdd` | Liste der Bilder mit Anteil gültiger Pixel |
| `GET /remotesensing/timeseries/{lake}/{satellite}/{variable}/{lat}/{lng}/{start}/{end}?window={n}` | Werte am Punkt über die Zeit. `window` mittelt über n Pixel um den Punkt. |

### Eigenheiten (aus dem Spike)

- Alplakes ist langsam: 5–16 s pro Call, `mitgcm` bis 30 s und mehr. Timeout 60 s pro Versuch, 240 s total, bis 4 Retries. Die Service Defaults legen um jeden HttpClient einen Standard-Handler mit 30 s total; für die Clients der Quellen ersetzt ihn der eigene Handler (`ForecastsModule`). Vorher brachen Calls von `mitgcm` am Zugersee nach 30 s ab (Lauf vom 19.10.2026, `PartiallySucceeded`).
- Eine Antwort, die der Parser nicht lesen kann, ist ein gescheiterter Call (Status im Lauf), ihr Body liegt trotzdem in den Rohdaten. Der Lauf rechnet weiter.
- Das 3D-Modell reicht heute plus 3 Tage. Es endet am Abend des dritten Tages.
- **Den Zeitraum aus der Antwort von `depthtime` lesen, nicht aus den Metadaten.** `/simulations/metadata` und `/simulations/metadata/{model}/{lake}` nannten am selben Tag verschiedene Enddaten.
- Die Strömung (`u`, `v`, m/s) ist in der Antwort enthalten. Bis 19.10.2026 waren alle Werte `null`. Unser Beispiel-Request hat bei Eawag einen Fehler mit bestimmten Randbedingungen aufgedeckt, seit dem Fix liefert er Werte (Riff Williams 19.10.2026: alle 204 Werte gesetzt, höchstens 0.02 m/s). Auch `mitgcm` (Zugersee) liefert sie. **Genutzt** für die Kachel «Strömung» (`forecast.md`, «Strömung»), ohne zusätzlichen Call. `u` zeigt nach Osten, `v` nach Norden.
- Unter 25 m liegen die Modellschichten 1.5 bis 3.5 m auseinander. Die Untergrenze der Sprungschicht springt darum von Schicht zu Schicht.
- Satellit: Ein Bild kommt 1- bis 2-mal pro Woche, je nach Wolken. Gültige Pixel im Median 15 %. Am Ufer fehlen Pixel. Darum fragt der Job einen **Satellitenpunkt** seewärts ab, nicht die Koordinate des Platzes (`datastructure.md`).

### Rechtliches

- Die API beschreibt sich als «as is»: keine Garantie für Genauigkeit, Vollständigkeit, Aktualität und Verfügbarkeit.
- **Anfrage vom 17.10.2026** an Eawag (Alplakes): Nutzung der Endpunkte für Profile (3D, Simstrat) und Satellit, ein Lauf pro Tag, höchstens etwa 1000 Calls pro Tag und 2 pro Sekunde, Quellenangabe auf jeder Seite. Fragen: Bedingungen, bevorzugte Frequenz, Datalakes, Sentinel-2 in Ufernähe, leere Felder `u` und `v`.
- **Nutzung erlaubt** (Eawag, Antwort vom 19.10.2026):
  1. Die Nutzung ist erlaubt, einzige Bedingung ist die Quellenangabe.
  2. Es gibt ein Rate Limit, die vorgeschlagene Frequenz (ein täglicher Lauf, höchstens etwa 1000 Calls pro Tag, höchstens 2 pro Sekunde) ist in Ordnung.
  3. Die Daten von Datalakes dürfen wir ebenfalls nutzen.
  4. Das Secchi-Produkt `Z490` von Sentinel-2 wird laufend verarbeitet.
  5. Zu den leeren Geschwindigkeitsfeldern `u` und `v` wünscht Eawag einen Beispiel-Request. Wir schickten am 19.10.2026 einen Request für Riff Williams und fragten zu `Z490` (Einheit, Abstand vom Ufer).
- **Zweite Antwort vom 19.10.2026:**
  - Der Beispiel-Request deckte einen Fehler auf (Zellen mit bestimmten Randbedingungen gaben `null`). Eawag hat ihn behoben, `u` und `v` liefern jetzt Werte.
  - `Z490` ist die Secchi-Tiefe in Metern und mit `Zsd_lee` von Sentinel-3 vergleichbar.
  - Pixel nahe am Ufer entfernt Eawag absichtlich, weil Ufer und flaches Wasser sie verfälschen. Ältere Bilder sind teils weniger streng maskiert. Jedes gelieferte Pixel gilt als gültig und darf genutzt werden.
- Rückmeldungen von Tauchenden zu den Modellwerten teilen wir mit Eawag, darum hat Eawag gebeten.
- **Dritte Antwort vom 19.10.2026:** Sentinel-2 überfliegt einen See etwa alle 5 Tage, Sentinel-3 täglich. Ob ein Pixel einen Wert hat, hängt von der Bewölkung ab. Das passt zur Regel «Sentinel-2 bis 5 Tage voll» (`forecast.md`, «Klarheit»): Ein Wert gilt bis zum nächsten Überflug als aktuell. Nach einem bewölkten Überflug altert er, dann zählt oft ein frischerer Wert von Sentinel-3.
- **Sentinel-2 in Ufernähe (`rules-1.2`):** `GET /remotesensing/timeseries/{lake}/sentinel2/Z490/{lat}/{lng}/{start}/{end}?window=2` zuerst 50 m seewärts des Platzes, bei fehlendem Wert oder weniger als 3 Pixeln (`count`) ein zweiter Call bei 100 m (`forecast.md`, «Klarheit»), Zeitraum 30 Tage. Laut Eawag gilt jedes gelieferte Pixel als gültig. Höchstens zwei Calls pro Platz an einem See mit `Z490`, Plätze am selben Punkt teilen ihn; dazu ein Call `remotesensing/metadata` pro Lauf. Heute haben 69 Plätze ein Gewässer mit Alplakes-Zuordnung (Zürichsee, Zugersee); das sind höchstens rund 138 Calls mehr pro Lauf (meist weniger: der zweite Call fällt weg, wenn 50 m genug Pixel hat), innerhalb der von Eawag bestätigten Frequenz. Ein Call dauert 2–5 s.
- Quellenangabe auf jeder Seite mit Werten: «Daten: Eawag Alplakes» mit Link.

## Wetter: Open-Meteo

Entscheid in ADR-007. Open-Meteo liefert die Modelle von MeteoSchweiz und anderen Diensten als JSON pro Punkt. Basis-URL: `https://api.open-meteo.com`.

`GET /v1/forecast?latitude=…&longitude=…&hourly=…&past_days=3&forecast_days=…&models={model}&wind_speed_unit=kmh&timezone=Europe/Zurich`

| Modell (Schlüssel) | Auflösung | Vorlauf | Rolle |
|---|---|---|---|
| MeteoSchweiz ICON-CH1 (`meteoswiss_icon_ch1`) | 1 km | 33 h | heute, morgen |
| MeteoSchweiz ICON-CH2 (`meteoswiss_icon_ch2`) | 2 km | 5 Tage | bis zum letzten Tag der Prognose |
| DWD ICON (`icon_seamless`) | 2–13 km | ca. 7 Tage | Rückfall |

- Ein Tag nimmt Wind aus dem feinsten Modell, das das Tauchfenster ganz abdeckt.
- Regen nimmt pro Stunde den Wert des feinsten Modells mit Daten. So bleibt die Summe auch am Rand eines Modells vollständig.
- Variablen: `wind_speed_10m`, `wind_gusts_10m`, `wind_direction_10m`, `precipitation`, dazu für die Grafik «Wetter pro Stunde» `temperature_2m`, `weather_code` (WMO 4677), `sunshine_duration` (Sekunden pro Stunde) und `is_day`. Alle drei Modelle liefern sie (geprüft 18.10.2026). Mehr Variablen heisst nicht mehr Calls.
- Plätze im selben Gitterpunkt (Koordinate auf zwei Dezimalen, rund 1 km) teilen sich die Werte.
- **Gebündelt (seit 19.10.2026):** Der Lauf holt das Wetter aller Plätze vorab, pro Modell mit einem Call für bis zu 50 Gitterpunkte (`latitude=a,b,…&longitude=…`). Open-Meteo antwortet dann mit einer Liste in der Reihenfolge der Punkte. Fehlt ein Punkt, gilt der Call als gescheitert. Die Rohdaten liegen pro Call als `open-meteo/{modell}-batch-{nr}.json`, die abgefragten Punkte daneben als `….points.json`.
- **Scheitert ein Bündel** (auch nach den Retries), fehlt dieses Modell für alle Punkte des Bündels. Einen Rückfall auf einen Call pro Punkt gibt es nicht. Die übrigen Modelle bleiben, und die Regeln oben nehmen das feinste Modell mit Daten (ICON-CH1, ICON-CH2, DWD ICON). Fehlen alle drei, fehlt der Wind an diesem Tag.
- **Calls pro Lauf** (319 Plätze, 254 Gitterpunkte, Stand 19.10.2026): vorher 762 Calls an Open-Meteo (254 × 3 Modelle), gebündelt 18 (6 Bündel × 3 Modelle).
- **Kontingent:** Die freie API erlaubt 600 Calls pro Minute, 5 000 pro Stunde und 10 000 pro Tag. Ein Call zählt mehrfach ab mehr als 10 Variablen oder mehr als 2 Wochen pro Punkt (wir: 8 Variablen, 10 Tage). Wie Open-Meteo mehrere Punkte in einem Call zählt, steht nicht in der Doku (open-meteo.com/en/pricing, /en/docs, geprüft 19.10.2026). Wir rechnen vorsichtig mit einem Call pro Punkt: 762 pro Lauf, rund 1 500 pro Tag bei zwei Läufen. Das Bündeln senkt sicher die Zahl der Requests und damit die Last und die 503-Antworten.
- **Mehrere Punkte zählen einzeln (bestätigt 21.10.2026):** Im Lauf vom 21.10.2026 antwortete Open-Meteo beim Bündel `icon_seamless-batch-003` mit HTTP 429 «Minutely API request limit exceeded». Die 18 Bündel kamen schnell hintereinander, also zählt jeder Punkt als Call. Die Doku sagt dazu weiterhin nichts (open-meteo.com/en/pricing, geprüft 21.10.2026).
- **Drosselung:** Der Client verteilt die Punkte über die Minute (`Forecast:OpenMeteo:PointsPerMinute`, Standard 400, unter dem Limit von 600). Vor jedem Bündel wartet er, bis die Punkte in die letzten 60 Sekunden passen. Bei 254 Gitterpunkten × 3 Modellen (762 Punkte) kostet das rund 1–2 Minuten zusätzlich pro Lauf.
- **429:** Beim Minutenlimit wartet der Client 61 Sekunden und wiederholt das Bündel einmal. Beim Limit der Stunde oder des Tages wartet er nicht: Er schickt bis zur nächsten vollen Stunde (UTC) bzw. bis Mitternacht (UTC) keinen Call mehr und loggt eine Warnung. Den 429 wiederholt der Resilience Handler dieses Clients nicht, sonst kämen vier schnelle Versuche zusätzlich ins Kontingent.
- Open-Meteo antwortet bei Last mit HTTP 503. Retry mit Backoff.
- **Bedingungen:** Die freie API ist nur für nicht-kommerzielle Nutzung, mit Quellenangabe (CC BY 4.0). Die Daten der Modelle sind Open Data der Wetterdienste.

## Wetter gemessen: MeteoSchweiz SwissMetNet (geplant)

Stand 21.10.2026: nicht angebunden. Der Lauf ruft SwissMetNet nicht ab, und das Datenmodell hat noch keine Zuordnung der Stationen.

Gemessene Werte ergänzen die Prognose. Sie zeigen den aktuellen Stand und den Regen der letzten Tage.

- Zugang: STAC-Collection `ch.meteoschweiz.ogd-smn` auf `data.geo.admin.ch`. CSV pro Station, z.B. `…/{station}/ogd-smn_{station}_t_now.csv`.
- Inhalt: Wind (Mittel, Böen, Richtung), Niederschlag, alle 10 min. 158 Stationen.
- Zuordnung: Jedes Gewässer hat eine oder mehrere Wetterstationen (`datastructure.md`).
- Verwendung: Regen der letzten 72 h gemessen statt aus der Prognose. Aktueller Wind auf der Platzseite («jetzt am See»).
- Bedingungen: Open Data, CC BY. Quellenangabe «MeteoSchweiz».

## Abfluss, Pegel und Wassertemperatur: BAFU

### MVP: über existenz.ch

- `GET https://api.existenz.ch/apiv1/hydro/daterange?locations={station}&parameters={parameter}&startdate=…&enddate=…&app=tauchplaetze.ch`
- Parameter: `flow` (Abfluss), `height` (Pegel), `temperature` (Wassertemperatur), an einigen Stationen Trübung.
- 246 Stationen, Werte alle 10 min.
- Mehrere Stationen und Parameter in einem Call: `locations=2091,2288,…&parameters=flow,temperature`. Eine Station ohne Temperatur liefert nur `flow` (Stand 19.10.2026 mit Temperatur: 2016, 2029, 2091, 2135, 2243, 2288; ohne: 2063, 2132, 2368, 2378, 2475, 2480, 2605; Contract Test mit `daterange-2091-2605-flow-temperature.json`). Die Antwort hat pro Wert `timestamp` (Unix, s), `loc`, `par` und `val`.
- `startdate` und `enddate` sind UTC-Tage: Die Antwort für `startdate=2026-10-18` beginnt am 18.10.2026 um 00:00 UTC (Contract Test mit `TestData/hydro/daterange-2091-flow-height.json`).
- Stationen mit Name, Gewässer und Lage: `GET https://api.existenz.ch/apiv1/hydro/locations?app=tauchplaetze.ch`.
- Bedingungen: BAFU-Daten, Quelle angeben.

### Ziel: offizielle BAFU-Datenplattform

- `api.data-platform.cloud.bafu.admin.ch`, Datenprodukt «Hydrological observations». GraphQL, höchstens 10 000 Rows pro Query.
- Bedingungen: opendata.swiss «Open use, must provide source».
- Wechsel, sobald der Client steht. Der Grund: offizielle Quelle, keine Abhängigkeit von einem privaten Dienst.

### Verwendung

- **Stationen im MVP** (Seed, vor Ort prüfen): Zürichsee 2104 Linth – Weesen (Zufluss), Zugersee 2477 Lorze – Zug, Letzi (Zufluss), Rhein 2091 Rhein – Rheinfelden (Fluss).
- **Flussplätze** (Seed vom 19.10.2026, aus `hydro/locations`): pro Fluss die Stationen, die einem Platz am nächsten liegen, höchstens 30 km entfernt, mit Lage. Rhein 2091, 2288 Neuhausen (2392 Rheinau misst im Oberwasser nur den Pegel, ohne Abfluss). Aare 2016 Brugg, 2029 Brügg-Aegerten, 2063 Murgenthal, 2135 Bern-Schönau. Areuse 2480 Boudry. Limmat 2243 Baden. Maggia 2368 Locarno-Solduno, 2475 Bignasco. Orbe 2378 Orbe. Töss 2132 Neftenbach. Verzasca 2605 Lavertezzo. Der Abfluss-Job holt sie alle 30 Minuten (`forecast.md`, «Abfluss»).
- **Seen:** Jedes Gewässer hat eine Liste relevanter Stationen (Hauptzuflüsse). Ein Platz kann die Zuordnung überschreiben (z.B. nahe einer Mündung). Der Abfluss geteilt durch den Median der letzten 30 Tage (mindestens 1 m³/s) ergibt pro Station den Quotienten. `InflowDischargeRatio` speichert den Quotienten, der am Platz zählt (`forecast.md`, `rules-1.5`).
- **Flüsse:** Die Station des Flusses liefert Abfluss, Wassertemperatur und, wo vorhanden, Trübung am Platz. Das ist die Datenstufe «Gewässer» für Flussplätze (`forecast.md`).
- **Seepegel:** Information auf der Gewässerseite und bei Einstiegen, kein Teil des Index.

### Phase 1: Vorhersagen des BAFU

- Das BAFU rechnet Prognosen für Abfluss und Seepegel (etwa 3 Tage, etwa 50–60 Stationen).
- Es gibt keine dokumentierte API. Nur ein JSON der Grafiken auf `hydrodaten.admin.ch` (`/plots/p_forecast/{id}_p_forecast_de.json`).
- **Anfrage vom 17.10.2026** an das BAFU (Abteilung Hydrologie): Nutzung von Messwerten (Abfluss, Pegel, Temperatur, Trübung) und Vorhersagen, Fragen zu Schnittstelle, Stationen, Aktualisierung, Grenze der Abfragen und Quellenangabe.
- **Nutzung erlaubt** (BAFU, Abteilung Hydrologie, Antwort vom 19.10.2026):
  - Eine offizielle Schnittstelle für Vorhersagen gibt es noch nicht. Das BAFU bietet einen Zugang per SFTP oder HTTPS an, mit Beispieldaten im Anhang der Mail.
  - Vorhersagen gibt es für die Stationen, die auf Hydroweb aufgeschaltet sind («Hydrologische Daten und Vorhersagen», https://www.hydrodaten.admin.ch/de/aktuelle-lage). Sie werden 1- bis 2-mal täglich gerechnet.
  - Keine Grenze für die Anzahl Abfragen.
  - Quellenangabe: «Bundesamt für Umwelt BAFU».
- **Beispieldaten** (Anhang der Mail, Stand 19.10.2026): 53 Stationen in je 5 Dateien `Pqprevi_{Variante}_{Station}.txt`. Dabei sind 2104 Linth – Weesen und 2091 Rhein – Rheinfelden, nicht aber 2477 Lorze – Zug.

  | Variante | Wettermodell | Reicht bis | Spalten |
  |---|---|---|---|
  | `ICH1` | MeteoSchweiz ICON-CH1, Ensemble | ca. 2 Tage | Steuerlauf, 10 Mitglieder, Minimum, Quartile, Median, Maximum (je Wasserstand `H_` und Abfluss `Q_`) |
  | `ICH1_Ctrl` | ICON-CH1, Steuerlauf | ca. 2 Tage | Wasserstand, Abfluss |
  | `ICH2` | ICON-CH2, Ensemble | ca. 5 Tage | Steuerlauf, 20 Mitglieder, Perzentile |
  | `ICH2_Ctrl` | ICON-CH2, Steuerlauf | ca. 5 Tage | Wasserstand, Abfluss |
  | `ECMWF` | ECMWF | ca. 10 Tage | Wasserstand, Abfluss |

  Format: Text in UTF-8. Ein Kopf von rund 15 Zeilen nennt Ausgabezeit, Zeit des Wetterlaufs und den letzten Messwert (MEWZ). Danach eine Zeile pro Stunde: `dd mm yyyy hh Wasserstand Abfluss`, Wasserstand in m ü. M., Abfluss in m³/s. Die Reihe beginnt einen Tag vor der Ausgabe mit gemessenen Werten.
- **Verwendung (geplant):** `ICH2_Ctrl` deckt die 5 Tage der Prognose ab. Die Stunden kommen als `HydroHour` mit `Kind = Forecast` in dieselbe Tabelle wie die Messwerte. Die Kachel «Abfluss» zeigt sie dann nach der gemessenen Kurve. Der Teilwert «Zuflüsse» rechnet dann mit dem vorhergesagten statt dem aktuellen Abfluss, vor allem für Flussplätze. Das Ensemble (`ICH2`) taugt später für einen Hinweis «Abfluss unsicher».
- **Bitte um Zugang per HTTPS** am 19.10.2026 für `ICH2_Ctrl` der Stationen 2104 und 2091, mit Fragen zur Verfügbarkeit und zu 2477 Lorze – Zug.
- **Zugang per HTTPS** (BAFU, Abteilung Hydrologie, Antwort vom 20.10.2026):
  - `https://www.hydrodata.ch/data/pqprevi-official/`: die Vorhersagen, die das BAFU auf hydrodaten.admin.ch publiziert. Täglich von den Prognostikern kontrolliert.
  - `https://www.hydrodata.ch/data/pqprevi-unofficial/`: nur automatisch gerechnet, nicht kontrolliert und nicht publiziert. Dort liegen weitere Stationen, z.B. 2477 Lorze – Zug.
  - Basic Auth (Prüfung vom 20.10.2026: `401` mit `WWW-Authenticate: Basic`). Benutzername und Passwort kamen in zwei Mails. Sie liegen nur in der Konfiguration: lokal als User Secrets des Web-Projekts (`Hydro:Forecast:Username`, `Hydro:Forecast:Password`), in Prod als Secret. Nie im Repository, nie im Log: Die Clients der Quellen redigieren alle Header im Log des HttpClient, Rohdaten und Status enthalten nur URL und Body.
  - Die Vorhersagen sind ab 08:00 verfügbar. Bei Hochwasser rechnet das BAFU sie 3- bis 4-mal pro Tag neu.
  - Fragen zu Inhalt und Format: BAFU (Abflussvorhersage).
- **Verwendung (umgesetzt 20.10.2026):** Der Abfluss-Job (`ImportDischarge`) holt die Vorhersage der Stationen aller Flussplätze und, seit `rules-1.4`, der Zuflüsse aller Seen mit Plätzen ab 08:15 und danach höchstens alle 3 Stunden. Pro Station in dieser Reihenfolge, bis eine Datei kommt: `official` `ICH2_Ctrl`, `official` `ICH1_Ctrl`, `unofficial` `ICH2_Ctrl`, `unofficial` `ICH1_Ctrl`. Zwischen zwei Calls liegen 500 ms (höchstens 2 pro Sekunde). Pro Station also 1 bis 4 Calls. Mit 13 Fluss- und 21 Zuflussstationen (Stand 20.10.2026) sind das rund 34 bis 136 Calls pro Abruf (höchstens rund 70 s), rund 6 Abrufe pro Tag, also rund 200 bis 800 Calls pro Tag. existenz.ch bleibt bei einem Call pro Lauf für alle Stationen (alle 30 Minuten). Rohdaten wie die Messwerte in Blob Storage.
  - Die Zeiten der Datei sind MEWZ, also immer UTC+1, auch im Sommer (laut Kopf: «Übrige Zeitangaben in MEWZ»). Die Zeile `hh` ist das Mittel der Stunde, die um `hh` endet: «Gemessene Werte bis 06.00» heisst, Zeile `06` ist das letzte gemessene Mittel (05–06 MEWZ), Zeile `07` das erste vorhergesagte (06–07 MEWZ). Gespeichert wird jede Stunde mit ihrem Beginn in UTC, nur die Stunden ab «Gemessene Werte bis». Die Lesart folgt aus dem Kopf der Datei; beim BAFU ist sie noch zu bestätigen. Fehlende oder negative Werte überspringt der Parser. Die Spalte heisst `Abfluss` oder `Débits`.
  - Ausgabezeit: Eine Datei, die vor heute 00:00 Schweizer Zeit ausgegeben wurde, zählt nicht (z.B. vor der Ausgabe um 08:00 die von gestern). Der Import versucht dann die nächste Variante und, ohne frische Datei, beim nächsten Lauf (30 Minuten) wieder.
  - Ausfall: Antwortet das BAFU mit 5xx oder gar nicht, hört der Abruf für diesen Lauf auf; der nächste versucht es wieder. Eine fehlende Datei (404) ist kein Ausfall, nicht jede Station hat jede Variante.
  - Stationen: Der Job holt die Vorhersage nur für Stationen mit Messwerten im selben Lauf. Die Einordnung braucht den Median der Messwerte, und `HydroReadings` hält den Stand des Abrufs. Ohne Messwerte gibt es keine Kachel, also auch keine Vorhersage.
  - Mit Zugangsdaten muss `Hydro:Forecast:BaseUrl` HTTPS sein, sonst bricht der Start ab.
  - Eine neue Vorhersage ersetzt die alte der Station ganz. Stammt sie aus `unofficial`, zeigt die Kachel «automatisch gerechnet, nicht geprüft».
  - Fehlen die Zugangsdaten, holt der Job keine Vorhersage und schreibt einmal pro Prozess eine Warnung, ohne Werte.
- Nutzen: Prognose für Flussplätze. Ohne sie bleibt die Flussprognose bei Wetter und aktuellem Abfluss.

## Eawag Datalakes (Phase 1)

- In-situ-Messungen der Eawag und Partner: Temperaturketten (z.B. Hallwilersee 0.5–44 m, Murtensee, Ägerisee, Vierwaldstättersee bei Kastanienbaum), Profile (Greifensee, Genfersee LéXPLORE), Sonden und Bojen (Lago di Lugano, Lago Maggiore), Wetterstationen auf dem See, **Secchi-Tiefen auf LéXPLORE** (Genfersee).
- Zugang: `https://api.datalakes-eawag.ch/datasets` und `/files?datasets_id=…`. JSON und NetCDF. Die API ist nicht dokumentiert, läuft aber stabil.
- Frequenz: 10 min bis täglich. Etwa 9 Seen.
- Lizenz: laut Metadaten fast alle MIT, einzelne CC BY 4.0. Quellenangabe Eawag.
- Verwendung: gemessenes Temperaturprofil als aktueller Stand. Kontrolle und spätere Korrektur der Modellwerte. Secchi als Label für das eigene Modell (Phase 2).
- Eine Messstation gehört zu einem Gewässer. Sie gilt für Plätze in ihrer Nähe (Distanz konfigurierbar).

## swisstopo

| Produkt | Nutzen | Zugang | Phase |
|---|---|---|---|
| Landeskarte farbig und grau und Luftbild (WMTS: `ch.swisstopo.pixelkarte-farbe`, `ch.swisstopo.pixelkarte-grau`, `ch.swisstopo.swissimage`) | Karte auf Verzeichnis, Gewässer- und Platzseite sowie im CMS, mit Umschalter Karte/Karte grau/Luftbild (Standard Landeskarte; die Wahl bleibt im Browser. Ausschnitt auf die Schweiz begrenzt, kleinster Zoom 7, weil das Luftbild ausserhalb weiss ist) | WMTS von geo.admin.ch, ohne Registrierung | MVP |
| swissTLM3D Gewässernetz | Stammdaten aller Seen und Flüsse (Name, Geometrie) | Download, STAC `ch.swisstopo.swisstlm3d` | MVP (Seed der Gewässer) |
| swissBATHY3D | Tiefenlinien von 22 Seen (u.a. Zürichsee, Thunersee, Brienzersee, Walensee, Genfersee, Neuenburgersee, Vierwaldstättersee, Zugersee, Lago Maggiore, Bodensee) für Platzkarte und Tauchskizze | DTM als Download (STAC `ch.swisstopo.swissbathy3d`, ESRI ASCII Grid, LV95, 1 m); als WMTS nur Hillshade | MVP, siehe «Tiefenlinien» |

- Bedingungen: Open Government Data. Quellenangabe «© swisstopo».

### Tiefenlinien (swissBATHY3D)

Das Skript `tools/bathymetry/contours.cs` rechnet einmalig Tiefenlinien. Die App lädt nur die fertigen Dateien, keine Abfrage bei swisstopo.

- **Quelle:** STAC `ch.swisstopo.swissbathy3d`, pro See ein ZIP mit Kacheln zu 1 km (ESRI ASCII Grid, LV95, Höhe LN02, 1 m). Zusammen rund 3 GB, Cache in `tools/bathymetry/out/` (nicht im Git).
- **Raster:** Mittel auf Blöcke von 5 m × 5 m. Für eine Skizze genügt das.
- **Tiefe:** mittlere Seehöhe minus Höhe des Seegrunds. Die Seehöhen stehen als Tabelle im Skript (LN02, gerundet). Das Raster reicht nicht ganz ans Ufer: Beim Zürichsee liegt der höchste Wert 1.3 m unter dem Wasserspiegel. Ohne Tabelleneintrag gilt der höchste Wert. Stauseen schwanken stärker.
- **Linien:** Marching Squares bei 5, 10, 15, 20, 25, 30, 35, 40, 50 und 60 m. Vereinfacht nach Douglas-Peucker (1.5 m), Linien unter 30 m Länge fallen weg. LV95 nach WGS84 mit den Näherungsformeln von swisstopo (rund 1 m).
- **Ausgabe:** `wwwroot/bathymetry/{see}/{e}_{n}.json` (GeoJSON, ein `MultiLineString` pro Tiefe, Property `depth`; `e`, `n` = Kilometer in LV95 wie die Kacheln von swisstopo) und `wwwroot/bathymetry/index.json` mit Seehöhe, Datum der Daten, Bounding Box und Kacheln pro See.
- **Karte:** Ebene «Tiefenlinien», standardmässig an, ab Zoom 13 geladen, Beschriftung ab Zoom 15. Quellenangabe in der Karte «swissBATHY3D © swisstopo».
- **Neu rechnen:** nur, wenn swisstopo neue Daten liefert. Der Stand ist pro See verschieden (2014 bis 2026) und steht in `index.json`.
- **Grösse:** 22 Seen, 1671 Kacheln, zusammen 10 MB (gzip rund 3 MB). Die grössten: Bodensee 2.2 MB, Genfersee 1.6 MB, Vierwaldstättersee 1.4 MB.
- Damit ist offener Punkt #11 (Kartenkacheln) entschieden: swisstopo.
- Google Maps und Apple Karten sind nur Links für die Route (`google.com/maps/dir/?api=1&destination=…`, `maps.apple.com/?daddr=…`). Kein API-Key, keine Kosten. Erst der Klick schickt die Koordinate an den Anbieter.

## OpenStreetMap

Erstbestand des Verzeichnisses: alle Punkte mit `sport=scuba_diving` in der Schweiz (Stand 18.10.2026: 340). Das Skript `tools/osm-import/osm-dive-sites.cs` holt sie einmalig über die Overpass API und ergänzt die Seeds. Die App selbst ruft OSM nie auf.

- **Übernommen:** Name, weitere Namen (`alt_name`, getrennt mit `;`), Koordinate, Gewässer, Gemeinde (`admin_level=8`), Kanton (`ISO3166-2`). Keine Beschreibungen.
- **Korrekturen von Marc** (im Skript, Tabelle `Names`, 18.10.2026): «Kleiner Parkplatz Erlenbach Schipfe» heisst «Kleiner Parkplatz»; Steinrad ist auch als «Grosser Parkplatz» bekannt.
- **Startwerte** (Entscheid Marc, 18.10.2026), damit die Filter im Verzeichnis sofort greifen. Die Redaktion korrigiert sie wie jeden Inhalt.

  | OSM-Tag | Wert bei uns |
  |---|---|
  | `scuba_diving:type` enthält `wall` oder `scuba_diving:type:wall=yes` | `WallRating` 4 (Wand) |
  | `scuba_diving:type` enthält `wreck` oder `scuba_diving:type:wreck=yes` | `WrecksRating` 3 (einige) |
  | `scuba_diving:difficulty` 1, 2, 3 | `DifficultyRating` 2, 3, 4 |
  | `scuba_diving:maxdepth`, sonst `maxdepth`, sonst `scuba_diving:depth` (grösster Wert, «>50» = 50, über 200 m verworfen) | `MaxDepth` |
  | `scuba_diving:entry` `boat` / sonst | `Access` Boat / Shore |
  | `scuba_diving:type` enthält `cave` bzw. `training` | Eigenschaft Höhle bzw. Ausbildung |

  Stand 18.10.2026: Tiefe bei 38, Schwierigkeit bei 45, Steilwand bei 39, Wrack bei 6, Zugang bei 62 Plätzen.
- **Gewässer:** benanntes `natural=water` oder `waterway=river|canal` im Umkreis von 300 m oder als Fläche um den Punkt. Steht der Name des Gewässers im Namen des Platzes, gilt dieses. Sonst geht ein See einem Fluss vor (Platz an einer Mündung). Häfen und Becken zählen nicht. Neue Gewässer kommen mit Namen in vier Sprachen (`name:de|fr|it|en`) in den Seed, ohne Alplakes-Zuordnung (Datenstufe «nur Wetter», bis jemand sie ergänzt).
- **Einstiege:** Punkte mit «Einstieg», «Ausstieg», «Entrée», «Sortie», «Ingresso» oder «Uscita» im Namen werden Einstiege des Platzes mit demselben Namensanfang in höchstens 400 m. Fehlt er, entsteht ein Platz mit diesem Namen.
- **Kein Parkplatz aus dem Namen:** Ein Punkt mit `sport=scuba_diving` ist ein Tauchplatz, auch wenn er «Parkplatz» heisst (am Zürichsee «Kleiner» und «Grosser Parkplatz»). Echte Parkplätze (`amenity=parking`) holt das Skript nicht.
- **Nicht übernommen:** Punkte ohne Namen, ohne Gewässer, Dubletten nach Slug, Punkte, deren `source` auf ein anderes Tauchplatzverzeichnis zeigt.
- **Durchsicht:** Das Skript schreibt `tools/osm-import/out/review.md`. Marc prüft die Liste. Das Skript ergänzt nur, was fehlt: Ein zweiter Lauf fügt neue OSM-Punkte hinzu und lässt bestehende Seeds stehen.
- **Quellenangabe:** im Seitenfuss «© OpenStreetMap-Mitwirkende, ODbL» mit Link auf openstreetmap.org/copyright.
- **Abruf über curl:** .NET baut zu overpass-api.de keine TLS-Verbindung auf (macOS, 18.10.2026). Das Skript ruft darum curl auf.
- **Lizenz:** ODbL. Quellenangabe «© OpenStreetMap contributors» bei den Daten. Die übernommenen Felder (Name, Koordinate, Gewässer) bleiben unter ODbL verfügbar (Share-Alike). Eigene Inhalte der Redaktion (Texte, Bewertungen, Fotos) sind davon getrennt.
- **Herkunft:** Rund zwei Drittel der Punkte tragen `source=www.dive-links.com`. Entscheid Marc, 18.10.2026: alle übernehmen. Tauchplätze sind allgemein bekannte Ortsmarken, die in vielen Tauchführern stehen, kein besonderer Inhalt. Ob die Daten in OSM sein dürfen, verantwortet OSM. Wir übernehmen nur Name, Koordinate und Gewässer.
- **Abfrage:** eine Overpass-Query, dazu eine kleine Query pro Punkt ohne Gewässer (Fläche um den Punkt, für Plätze mitten im See), `User-Agent` mit Projekt, Retry mit Backoff (Overpass antwortet bei Last mit 502 oder 504).

## Geprüft und nicht genutzt

| Quelle | Grund |
|---|---|
| Google Maps (Places) | Die Bedingungen verbieten Scraping und das dauerhafte Speichern der Inhalte. Nur Links für die Route. |
| Andere Tauchplatzverzeichnisse | Eigene Verzeichnisse Dritter. Systematisches Übernehmen wäre unlauter (UWG Art. 5 lit. c). Punkte in OSM, deren `source` auf ein solches Verzeichnis zeigt, werden ausgelassen. |
| MeteoSchweiz ICON direkt (GRIB2) | Open-Meteo liefert dieselben Modelle als JSON (ADR-007). Direkt heisst: grosse Dateien für die ganze Schweiz laden, um wenige Punkte zu lesen. Mehrwert erst mit dem **Ensemble** (11 bzw. 21 Läufe): Es zeigt, wie sicher die Windprognose ist. Kandidat für Phase 2 («Prognose unsicher»). |
| MeteoSchweiz `ogd-local-forecasting` (CSV-Punktprognose) | Später möglich als Ersatz für Open-Meteo, falls dessen Bedingungen nicht mehr passen. |
| MeteoSchweiz CombiPrecip (Radar) | Regen im ganzen Einzugsgebiet wäre genauer. HDF5, Aufwand M. Später. |
| Copernicus Sentinel-2 direkt | Ufernahe Trübung und Algen in 10–20 m statt 300 m. Der Code ist nicht das Problem. Schwierig sind die Wissenschaft und der Betrieb: Atmosphärenkorrektur über Wasser, Streulicht vom Ufer, Wolken, Kalibrierung gegen echte Sichttiefen. Eine Szene hat rund 1 GB. Die Auswertung muss darum serverseitig laufen (openEO im Copernicus Data Space). Kandidat für Phase 2, als Experiment mit Marcs Tauchgängen als Vergleich. |
| Kantonale Seeberichte (AWEL, IGKB, CIPEL, CIPAIS) | Secchi-Tiefen meist monatlich, nur als PDF oder auf Bestellung. Später einmalig für die Saisonwerte. |
| Wasserversorgung Zürich | Laborwerte ohne Sichttiefe, einmal pro Jahr. |
| BAFU Badewasserqualität | E. coli, kein Bezug zur Sicht. |
| Blaualgen-Warnungen | Keine offene Datenquelle, nur Medienmitteilungen der Kantone. Redaktionell erfassen. |
| Schutzgebiete und Einschränkungen der Schifffahrt | Später für die Dokumentation (Hinweise, Verbote). Tauchverbote sind kantonal und lückenhaft. |

## Robustheit

- Pro Quelle ein typisierter `HttpClient` im Core, mit Resilience-Pipeline (Retry, Timeout, Circuit Breaker).
- Antworten unverändert in Blob Storage ablegen. Bei einer Formatänderung lassen sich alte Läufe neu berechnen.
- Contract Tests mit gespeicherten echten Antworten (`TestData/{quelle}/*.json`). Keine Live-Calls in der CI.
- Ändert sich eine API, betrifft das nur ihren Client und seine Mapper.
- Höflich: höchstens 2 Anfragen pro Sekunde und Quelle. `User-Agent` mit Kontaktadresse.

## Was wir nicht nutzen

- **Keine Daten aus anderen Tauchplatzverzeichnissen oder Plattformen für Sichtweiten.** Kein Scraping.
- Eine Zusammenarbeit ist möglich, aber nur mit Zustimmung der jeweiligen Betreiber.

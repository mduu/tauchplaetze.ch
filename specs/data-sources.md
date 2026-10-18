# Datenquellen

Diese Spec listet alle externen Datenquellen. Regel: **Keine Seite ruft eine externe Quelle während einer Anfrage auf.** Nur der Job importiert. Die Seiten lesen die eigene Datenbank.

Stand: 17.10.2026. Grundlage: Spike 0.5 (`doc/spikes/0.5-datenquellen.md`) und eine Recherche vom selben Tag. Live geprüft ist, was in der Spalte «Geprüft» ein ✅ hat.

Das Projekt ist **nicht kommerziell** (`vision.md`, «Finanzierung»). Mehrere Quellen erlauben die freie Nutzung nur unter dieser Bedingung (ADR-007).

## Übersicht

| Quelle | Inhalt | Nutzen | Phase | Geprüft |
|---|---|---|---|---|
| Eawag Alplakes API | Seemodelle 3D und 1D (Temperatur nach Tiefe und Zeit), Satellit (Secchi, Chlorophyll) | Prognose Temperatur und Sprungschicht, Klarheit | MVP | ✅ Spike |
| Open-Meteo (Modelle von MeteoSchweiz, DWD, ECMWF) | Wetterprognose am Punkt, stündlich | Prognose Wind und Regen | MVP | ✅ Spike |
| MeteoSchweiz SwissMetNet | gemessener Wind, Böen, Niederschlag, alle 10 min | aktueller Stand, Regen der letzten Tage | MVP | ✅ |
| BAFU Hydrodaten (über existenz.ch, Ziel: BAFU-Datenplattform) | Abfluss, Pegel, Wassertemperatur, an einigen Stationen Trübung | Zuflüsse, Flussplätze | MVP | ✅ Spike |
| swisstopo | Kartenkacheln, Gewässernetz (swissTLM3D), Bathymetrie (swissBATHY3D) | Karte, Stammdaten aller Gewässer, Tiefenkarten | MVP | ✅ |
| Eawag Datalakes | In-situ-Messungen: Temperaturketten, Bojen, Profile, Secchi (LéXPLORE) | aktueller Stand, Kontrolle der Modelle | Phase 1 | ✅ |
| BAFU Abfluss- und Seepegelvorhersagen | Prognose für etwa 50–60 Stationen | Prognose für Flussplätze | Phase 1, nach Klärung | ⚠️ |
| Eigenes Archiv (tauchfotos.ch, tauchvideos.ch) | Fotos, Videos | Dokumentation | MVP | Rechte bei Marc |

## Eawag Alplakes

Alplakes ist eine Plattform der Eawag (Abteilung Oberflächengewässer). Sie verbindet Seemodelle mit Satellitendaten für über 200 Seen im Alpenraum. Alplakes erhielt 2025 den Nationalen Preis für offene Forschungsdaten. Entwickler: Eawag (Alplakes) (Eawag).

Der Code der API ist öffentlich: `github.com/eawag-surface-waters-research/alplakes-fastapi`. Die API heisst «Alplakes API» (Version 2.0.0). Basis-URL: `https://alplakes-api.eawag.ch`.

**Die API hat keine Endpunkte `/insitu`, `/meteoswiss` und `/bafu` mehr** (Spike 17.10.2026). Gemessene Secchi-Tiefen gibt es über die API nicht mehr. Wetter und Abfluss kommen aus eigenen Quellen.

### Abdeckung (Stand 17.10.2026)

| Art | Seen |
|---|---|
| 3D-Modell (Wert an der Koordinate des Platzes) | `delft3d-flow`: Ägerisee, Bielersee, Genfersee, Greifensee, Hallwilersee, Lago di Lugano, Murtensee, St. Moritzersee, Zürichsee (dazu Annecy, Bourget, Caldonazzo, Garda, Joux). `mitgcm`: Vierwaldstättersee, Neuenburgersee, Zugersee, Zürichsee |
| 1D-Modell Simstrat (ein Profil pro See oder Becken) | rund 130 Seen, u.a. Walensee, Thunersee, Brienzersee, Sempachersee, Sarnersee, Lago Maggiore, Bodensee (`upperconstance`, `lowerconstance`), Zürichsee (`lowerzurich`, `upperzurich`), Vierwaldstättersee in vier Becken |
| Satellit Sentinel-3 (`Zsd_lee`, `chla`) | alle grossen Seen |
| Satellit Sentinel-2 | viele Seen gelistet, Daten oft veraltet (Zürichsee: letztes Bild November 2023) |

Der Job liest die Abdeckung aus `GET /simulations/metadata` und `GET /simulations/1d/metadata`. Die Zuordnung eines Gewässers zu den Modellen steht in `WaterBody` (`datastructure.md`).

### Endpunkte

| Endpunkt | Zweck |
|---|---|
| `GET /simulations/metadata` | 3D-Modelle und Seen |
| `GET /simulations/depthtime/{model}/{lake}/{start}/{end}/{lat}/{lng}` | Temperatur über Tiefe und Zeit am Gitterpunkt nächst der Koordinate. **Hauptquelle** (ein Call pro Platz). Zeiten als `yyyyMMddHHmm` (UTC). |
| `GET /simulations/1d/metadata` | 1D-Modelle, Seen, Zeitraum, Variablen |
| `GET /simulations/1d/depthtime/simstrat/{lake}/{start}/{end}` | Profil über Tiefe und Zeit für den ganzen See. Enthält die Variable `Thermocline`. |
| `GET /remotesensing/metadata` | Satellitenprodukte pro See |
| `GET /remotesensing/products/{lake}/{satellite}/{variable}?start_date=yyyyMMdd` | Liste der Bilder mit Anteil gültiger Pixel |
| `GET /remotesensing/timeseries/{lake}/{satellite}/{variable}/{lat}/{lng}/{start}/{end}?window={n}` | Werte am Punkt über die Zeit. `window` mittelt über n Pixel um den Punkt. |

### Eigenheiten (aus dem Spike)

- Alplakes ist langsam: 5–16 s pro Call, `mitgcm` bis 30 s. Timeout 60 s, bis 4 Retries.
- Das 3D-Modell reicht heute plus 3 Tage. Es endet am Abend des dritten Tages.
- **Den Zeitraum aus der Antwort von `depthtime` lesen, nicht aus den Metadaten.** `/simulations/metadata` und `/simulations/metadata/{model}/{lake}` nannten am selben Tag verschiedene Enddaten.
- Die Strömung (`u`, `v`) ist in der Antwort enthalten, aber alle Werte sind `null`. Wir nutzen sie nicht.
- Unter 25 m liegen die Modellschichten 1.5 bis 3.5 m auseinander. Die Untergrenze der Sprungschicht springt darum von Schicht zu Schicht.
- Satellit: Ein Bild kommt 1- bis 2-mal pro Woche, je nach Wolken. Gültige Pixel im Median 15 %. Am Ufer fehlen Pixel. Darum fragt der Job einen **Satellitenpunkt** seewärts ab, nicht die Koordinate des Platzes (`datastructure.md`).

### Rechtliches

- Die API beschreibt sich als «as is»: keine Garantie für Genauigkeit, Vollständigkeit, Aktualität und Verfügbarkeit.
- Explizite Nutzungsbedingungen für die Daten fehlen. Präzedenzfall: lake.lindt.one zeigt seit 2020 Wassertemperaturen aus Alplakes.
- **Vor dem Go-Live:** Eawag anschreiben (Eawag (Alplakes)). Inhalt: Vorhaben, nicht kommerziell, Quellenangabe, etwa ein Call pro Platz und Tag. Siehe `open-questions.md`.
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
- Variablen: `wind_speed_10m`, `wind_gusts_10m`, `wind_direction_10m`, `precipitation`.
- Plätze im selben Gitterpunkt teilen sich den Call.
- Open-Meteo antwortet bei Last mit HTTP 503. Retry mit Backoff.
- **Bedingungen:** Die freie API ist nur für nicht-kommerzielle Nutzung, mit Quellenangabe (CC BY 4.0). Die Daten der Modelle sind Open Data der Wetterdienste.

## Wetter gemessen: MeteoSchweiz SwissMetNet

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
- Bedingungen: BAFU-Daten, Quelle angeben.

### Ziel: offizielle BAFU-Datenplattform

- `api.data-platform.cloud.bafu.admin.ch`, Datenprodukt «Hydrological observations». GraphQL, höchstens 10 000 Rows pro Query.
- Bedingungen: opendata.swiss «Open use, must provide source».
- Wechsel, sobald der Client steht. Der Grund: offizielle Quelle, keine Abhängigkeit von einem privaten Dienst.

### Verwendung

- **Stationen im MVP** (Seed, vor Ort prüfen): Zürichsee 2104 Linth – Weesen (Zufluss), Zugersee 2477 Lorze – Zug, Letzi (Zufluss), Rhein 2091 Rhein – Rheinfelden (Fluss).
- **Seen:** Jedes Gewässer hat eine Liste relevanter Stationen (Hauptzuflüsse). Ein Platz kann die Zuordnung überschreiben (z.B. nahe einer Mündung). Der Abfluss geteilt durch den Median der letzten 30 Tage ergibt `InflowDischargeRatio`.
- **Flüsse:** Die Station des Flusses liefert Abfluss, Wassertemperatur und, wo vorhanden, Trübung am Platz. Das ist die Datenstufe «Gewässer» für Flussplätze (`forecast.md`).
- **Seepegel:** Information auf der Gewässerseite und bei Einstiegen, kein Teil des Index.

### Phase 1: Vorhersagen des BAFU

- Das BAFU rechnet Prognosen für Abfluss und Seepegel (etwa 3 Tage, etwa 50–60 Stationen).
- Es gibt keine dokumentierte API. Nur ein JSON der Grafiken auf `hydrodaten.admin.ch` (`/plots/p_forecast/{id}_p_forecast_de.json`).
- **Vor der Nutzung beim BAFU klären** (`BAFU (Hydrologie)`). Siehe `open-questions.md`.
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
| Landeskarte und Luftbild (WMTS) | Karte auf Verzeichnis, See- und Platzseite | WMTS von geo.admin.ch, ohne Registrierung | MVP |
| swissTLM3D Gewässernetz | Stammdaten aller Seen und Flüsse (Name, Geometrie) | Download, STAC `ch.swisstopo.swisstlm3d` | MVP (Seed der Gewässer) |
| swissBATHY3D | Tiefenkarten von 22 Seen (u.a. Zürichsee, Thunersee, Brienzersee, Walensee, Genfersee, Neuenburgersee, Vierwaldstättersee, Zugersee, Lago Maggiore, Bodensee) | Hillshade als WMTS, DTM als Download (STAC `ch.swisstopo.swissbathy3d`) | MVP als Kartenebene, DTM später (Tiefe am Einstieg, Steilwand) |

- Bedingungen: Open Government Data. Quellenangabe «© swisstopo».
- Damit ist offener Punkt #11 (Kartenkacheln) entschieden: swisstopo.
- Google Maps und Apple Karten sind nur Links für die Route (`google.com/maps/dir/?api=1&destination=…`, `maps.apple.com/?daddr=…`). Kein API-Key, keine Kosten. Erst der Klick schickt die Koordinate an den Anbieter.

## Geprüft und nicht genutzt

| Quelle | Grund |
|---|---|
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

- **Keine Daten von eine bestehende Plattform, ein Tauchclub oder swiss-divers.ch.** Kein Scraping. Marc hat damit früher schlechte Erfahrungen gemacht (Konflikt mit dem ein Tauchclub).
- Kooperation mit eine bestehende Plattform ist möglich, aber nur mit Zustimmung des Betreibers.

# Datenquellen

Diese Spec listet alle externen Datenquellen. Regel: **Keine Seite ruft eine externe Quelle während einer Anfrage auf.** Nur der Job importiert. Die Seiten lesen die eigene Datenbank.

## Übersicht

| Quelle | Inhalt | Zugang | Status |
|---|---|---|---|
| Eawag Alplakes API | Seemodelle (Temperatur nach Tiefe und Zeit), Secchi in situ, Remote Sensing, dazu Proxy für MeteoSchweiz und BAFU | REST, `https://alplakes-api.eawag.ch` | Nutzung mit Eawag klären (siehe unten) |
| MeteoSchweiz | Wetterprognose ICON, Messwerte | über Alplakes API oder Open Data von MeteoSchweiz direkt | prüfen, welcher Weg stabiler ist |
| BAFU Hydrodaten | Abfluss und Pegel der Flüsse | über Alplakes API oder hydrodaten.admin.ch | prüfen |
| swisstopo / OpenStreetMap | Kartenkacheln | WMTS bzw. Tile-Server | Nutzungsbedingungen und Namensnennung prüfen |
| Eigenes Archiv (tauchfotos.ch, tauchvideos.ch) | Fotos, Videos | Upload im CMS | Rechte bei Marc |

## Eawag Alplakes

Alplakes ist eine Plattform der Eawag (Abteilung Oberflächengewässer). Sie zeigt Messdaten und Prognosen für über 200 Seen im Alpenraum. Sie verbindet 3D-Seemodelle mit Satellitendaten. Alplakes erhielt 2025 den Nationalen Preis für offene Forschungsdaten. Entwickler: Eawag (Alplakes) (Eawag).

Der Code der API ist öffentlich: `github.com/eawag-surface-waters-research/alplakes-fastapi`. Die API heisst «Alplakes API» (Version 2.0.0).

### Relevante Endpunkte

Stand 17.10.2026, aus `app/main.py` des Repositories gelesen. Vor der Umsetzung gegen die Swagger-Doku der laufenden API prüfen.

**3D-Simulationen**

| Endpunkt | Zweck |
|---|---|
| `GET /simulations/metadata` | verfügbare Modelle und Seen |
| `GET /simulations/metadata/{model}/{lake}` | Zeitraum und Tiefen eines Modells |
| `GET /simulations/depthtime/{model}/{lake}/{start_time}/{end_time}/{lat}/{lng}` | Temperatur über Tiefe und Zeit an einem Punkt. **Hauptquelle für den Job** (ein Aufruf pro Platz). |
| `GET /simulations/profile/{model}/{lake}/{time}/{lat}/{lng}` | Temperaturprofil zu einem Zeitpunkt |
| `GET /simulations/point/{model}/{lake}/{start_time}/{end_time}/{depth}/{lat}/{lng}` | Zeitreihe in einer Tiefe |
| `GET /simulations/layer/average_temperature/...` | Mittlere Temperatur einer Schicht (Seen-Übersicht) |

**1D-Simulationen** (Rückfall für Seen ohne 3D-Modell)

| Endpunkt | Zweck |
|---|---|
| `GET /simulations/1d/metadata` | verfügbare 1D-Modelle |
| `GET /simulations/1d/point/{model}/{lake}/{start_time}/{end_time}/{depth}` | Zeitreihe in einer Tiefe (seeweit, ohne Koordinate) |

**In situ**

| Endpunkt | Zweck |
|---|---|
| `GET /insitu/secchi/metadata` | Seen mit Secchi-Messungen |
| `GET /insitu/secchi/{lake}` | gemessene Secchi-Tiefen eines Sees |
| `GET /insitu/temperature/...` | Temperatur-Messstationen |

**Wetter und Hydrologie (Proxy)**

| Endpunkt | Zweck |
|---|---|
| `GET /meteoswiss/icon/point/forecast/{model}/{forecast_date}/{lat}/{lng}` | ICON-Wetterprognose am Punkt |
| `GET /meteoswiss/meteodata/measured/{station_id}/{start_date}/{end_date}` | Messwerte einer Wetterstation |
| `GET /bafu/hydrodata/metadata` | BAFU-Stationen |
| `GET /bafu/hydrodata/measured/{station_id}/{variable}/{start_date}/{end_date}` | Abfluss bzw. Pegel einer Station |

**Remote Sensing:** Die Endpunkte waren im gelesenen Teil der Datei nicht sichtbar. Alplakes führt Secchi-Tiefe und Chlorophyll aus Satellitendaten (z.B. für den Brienzersee belegt). Die genauen Endpunkte und die Abdeckung pro See sind noch zu prüfen.

### Rechtliches und Betrieb

- Die API beschreibt sich selbst als «as is»: keine Garantie für Genauigkeit, Vollständigkeit und Aktualität. Die Verfügbarkeit ist nicht garantiert.
- Der Code steht unter einer Open-Source-Lizenz (Apache 2.0 laut API-Beschreibung, MIT-Badge im Repository). **Explizite Nutzungsbedingungen für die Daten sind nicht dokumentiert.**
- Präzedenzfall: lake.lindt.one (privates Projekt von Nicolas Lindt, Waadt) zeigt seit 2020 Wassertemperaturen an über 50 Stränden. Die Werte stammen aus Alplakes.
- **Vor dem Bau:** Eawag anschreiben (Kontakt laut API-Beschreibung: Eawag (Alplakes)). Inhalt: Vorhaben, Quellenangabe, Abfragefrequenz, Bitte um Bestätigung. Siehe `open-questions.md`.
- Quellenangabe auf jeder Seite mit Prognose: «Daten: Eawag Alplakes» mit Link.

### Robustheit

- Eigener typisierter `HttpClient` im Core (`AlplakesClient`), mit Resilience-Pipeline (Retry, Timeout, Circuit Breaker).
- Antworten unverändert in Blob Storage ablegen. Bei einer Formatänderung lassen sich alte Läufe neu berechnen.
- Contract Tests mit gespeicherten Beispielantworten (keine Live-Aufrufe in der CI).
- Ändert sich die API, betrifft das nur `AlplakesClient` und seine Mapper.

## MeteoSchweiz

- Bevorzugt über die Alplakes API (ein Client, ein Format).
- Alternative: Open-Data-Angebot von MeteoSchweiz direkt. Prüfen, welche Produkte (ICON-CH1/CH2, Stationsmessungen) als Open Data verfügbar sind und unter welchen Bedingungen.
- Verwendet: Windgeschwindigkeit und -richtung (stündlich), Niederschlag (stündlich, 72 h rückwärts).

## BAFU Hydrodaten

- Bevorzugt über die Alplakes API.
- Zuordnung: Jeder See hat eine Liste relevanter Stationen (Hauptzuflüsse). Optional überschreibt ein Platz die Zuordnung (z.B. Platz nahe einer Flussmündung).
- Verwendet: Abfluss. Vergleich mit dem Median der letzten 30 Tage ergibt `InflowDischargeRatio`.

## Kartenkacheln

- Optionen: swisstopo (Landeskarte, sehr schön für die Schweiz) oder OpenStreetMap.
- Nutzungsbedingungen und Namensnennung der gewählten Quelle prüfen. Für viele Kachelaufrufe einen eigenen Cache oder einen Anbieter mit freiem Kontingent nutzen.

## Was wir nicht nutzen

- **Keine Daten von eine bestehende Plattform, ein Tauchclub oder swiss-divers.ch.** Kein Scraping. Marc hat damit früher schlechte Erfahrungen gemacht (Konflikt mit dem ein Tauchclub).
- Kooperation mit eine bestehende Plattform ist möglich, aber nur mit Zustimmung des Betreibers.

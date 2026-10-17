# ADR-007: Wetter- und Abflussquellen

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Die Specs planten Wetter (MeteoSchweiz) und Abfluss (BAFU) über die Alplakes API. Der Spike 0.5 zeigte: Alplakes hat diese Endpunkte nicht mehr (`doc/spikes/0.5-datenquellen.md`).
- Die Prognose braucht Wind und Regen stündlich am Punkt, für heute und die 4 folgenden Tage. Dazu den gemessenen Regen der letzten 72 h.
- Für Seen braucht sie den Abfluss der Zuflüsse. Für Flussplätze braucht sie Abfluss, Wassertemperatur und, wo vorhanden, Trübung.
- Das Projekt ist nicht kommerziell (Entscheid Marc, 17.10.2026). Sponsoren sind später möglich.
- Der Core ist .NET. Ein Format, das sich ohne native Libraries lesen lässt, spart Aufwand.

## Entscheidung

Wir entscheiden uns für:

1. **Wetterprognose über Open-Meteo** mit den Modellen von MeteoSchweiz (ICON-CH1, ICON-CH2), DWD ICON als Rückfall.
2. **Gemessenes Wetter direkt von MeteoSchweiz** (SwissMetNet, Open Data, CSV).
3. **Abfluss, Pegel und Wassertemperatur über existenz.ch** im MVP. Danach Wechsel auf die **BAFU-Datenplattform** (GraphQL).

**Auslöser für eine neue Prüfung:** ein Sponsor, eine Einnahme jeder Art oder eine Änderung der Bedingungen von Open-Meteo. Dann wechseln wir auf das Abo von Open-Meteo oder auf die Punktprognose von MeteoSchweiz (`ogd-local-forecasting`, CSV).

## Optionen

### Option A: Open-Meteo (gewählt für die Prognose)

**Beschreibung:** JSON pro Punkt, Modelle wählbar, stündlich.

**Vorteile:**
- Ein Call pro Gitterpunkt und Modell, Antwort in unter 1 s.
- Dieselben Modelle wie bei MeteoSchweiz direkt.
- Im Spike geprüft: 192 Stunden ohne Lücke.

**Nachteile:**
- Freie Nutzung nur nicht-kommerziell.
- Ein privater Dienst zwischen uns und MeteoSchweiz. Bei Last HTTP 503.

### Option B: MeteoSchweiz ICON direkt (GRIB2 über STAC)

**Vorteile:**
- Offizielle Quelle, Open Data, auch kommerziell erlaubt.

**Nachteile:**
- GRIB2 ist in .NET aufwendig. Grosse Dateien für wenige Punkte.

### Option C: MeteoSchweiz Punktprognose (`ogd-local-forecasting`)

**Vorteile:**
- Offizielle Quelle, CSV, ganze Schweiz, bis 192 h.

**Nachteile:**
- Punkte sind Postleitzahlen und Stationen, nicht beliebige Koordinaten.
- Nicht im Spike geprüft.
- Ist der Rückfall, falls Option A nicht mehr passt.

### Option D: BAFU-Datenplattform statt existenz.ch

**Vorteile:**
- Offizielle Quelle, «Open use, must provide source».

**Nachteile:**
- GraphQL, im Spike nicht geprüft. existenz.ch lief im Spike sofort.
- Darum erst nach dem MVP.

## Konsequenzen

### Positive Auswirkungen

- Drei einfache Clients mit JSON bzw. CSV. Kein GRIB.
- Wetter für jeden Punkt der Schweiz. Jeder Platz hat damit mindestens Datenstufe 1 (`forecast.md`).
- Der gemessene Regen ersetzt den prognostizierten für vergangene Stunden.

### Negative Auswirkungen / Risiken

- Open-Meteo kann seine Bedingungen ändern oder ausfallen. Der Job behält die letzten Werte 48 h. Option C ist der Rückfall.
- existenz.ch ist ein privater Dienst. Der Wechsel auf die BAFU-Datenplattform ist geplant.

## Referenzen

- `doc/spikes/0.5-datenquellen.md`
- `data-sources.md`, `forecast.md`
- https://open-meteo.com/en/terms
- https://data.geo.admin.ch/api/stac/v1/collections (`ch.meteoschweiz.ogd-smn`, `ch.meteoschweiz.ogd-local-forecasting`)
- https://api.data-platform.cloud.bafu.admin.ch

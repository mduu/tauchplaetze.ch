# Datenstruktur

Diese Spec beschreibt die Entitäten in Azure SQL. Entitäten und Felder heissen englisch. Ein `DbContext` im Core: `TauchplaetzeDbContext`.

## Grundregeln

- **Geodaten** mit NetTopologySuite (`UseNetTopologySuite()`), SQL-Typ `geography`, SRID 4326 (WGS84). Die Umkreissuche läuft in der Datenbank (`Location.IsWithinDistance(...)`).
- **IDs:** `Guid` (Version 7, sortierbar) für fachliche Entitäten. `int` für kleine Stammdaten (`Lake`).
- **Zeit:** `DateTimeOffset` in UTC für Zeitpunkte. `DateOnly` für Prognosetage (Schweizer Lokalzeit, `Europe/Zurich`).
- **Optimistische Sperre:** `rowversion` auf allen Entitäten, die im CMS bearbeitet werden.
- **Slugs:** eindeutig, aus dem deutschen Namen gebildet, ohne Umlaute (`riff-williams-herrliberg`). Ein geänderter Slug erzeugt einen 301-Redirect (Tabelle `SlugRedirects`).
- **Übersetzbare Texte** stehen in eigenen Tabellen `{Entity}Translations` mit `Culture` (`de`, `en`, `fr`, `it`). Deutsch ist Pflicht, die anderen Sprachen sind optional. Fehlt eine Sprache, zeigt die Seite Deutsch mit einem Hinweis (siehe `i18n.md`).
- **Grosse Rohdaten** (Antworten der Datenquellen) liegen in Blob Storage, nicht in SQL. So bleibt die Datenbank klein (Free Offer: 32 GB).

## Entitäten

### Lake

Stammdaten der Gewässer. Ein Fluss ist ebenfalls ein `Lake` mit `WaterBodyType = River`.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | int | |
| `Slug` | string(80) | eindeutig, z.B. `zuerichsee` |
| `NameDe`, `NameEn`, `NameFr`, `NameIt` | string(100) | amtliche Namen, siehe `ubiquitous-language.md` |
| `WaterBodyType` | enum | `Lake`, `River` |
| `Outline` | geography (optional) | Umriss für die Karte |
| `Centroid` | geography | Mittelpunkt |
| `AlplakesLakeKey` | string(50), optional | Schlüssel des Sees in der Alplakes API (z.B. `zurich`) |
| `AlplakesModel3d` | string(50), optional | Modellname für 3D-Simulationen (z.B. `delft3d-flow`) |
| `AlplakesModel1d` | string(50), optional | Modellname für 1D-Simulationen (Rückfall) |
| `IsActive` | bool | |

### LakeInflowStation

Zuordnung der BAFU-Stationen (Hauptzuflüsse) zu einem See. PK: (`LakeId`, `StationId`). Felder: `StationId` (string(20)), `Name`, `Weight` (decimal, Anteil am Teilwert «Zuflüsse»).

### DiveSite

Der Tauchplatz. Nur `Name`, `Location` und `LakeId` sind Pflicht (Ausbaustufe 0).

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `Slug` | string(120) | eindeutig |
| `Name` | string(120) | Eigenname, wird nicht übersetzt |
| `LakeId` | int | FK `Lake` |
| `Location` | geography (Point) | Position des Platzes (für Prognose und Karte) |
| `EntryLocation` | geography (Point), optional | genauer Einstieg, falls abweichend |
| `Municipality` | string(80), optional | Gemeinde |
| `Canton` | string(2), optional | Kürzel, z.B. `ZH` |
| `MaxDepth` | int, optional | Meter |
| `Difficulty` | enum, optional | `Beginner`, `Advanced`, `Expert` |
| `Access` | enum, optional | `Shore`, `Boat`, `ShoreAndBoat` |
| `Features` | flags enum | `Wall`, `Wreck`, `Statues`, `Plateau`, `Cave`, `River`, `IceDiving`, `Training` |
| `HasDivingBan` | bool | Tauchverbot (dauernd oder zeitweise) |
| `InflowStationOverride` | string(20), optional | BAFU-Station, falls der Platz nahe einer Mündung liegt (überschreibt die Stationen des Sees) |
| `Status` | enum | `Draft`, `Published`, `Archived` |
| `ContentLevel` | int (0–2) | berechnet und gespeichert, siehe `cms.md` |
| `HeroMediaId` | Guid, optional | FK `MediaItem` |
| `CreatedAt`, `UpdatedAt` | DateTimeOffset | |
| `CreatedBy`, `UpdatedBy` | string | Benutzer-ID |
| `RowVersion` | rowversion | |

### DiveSiteTranslation

| Feld | Typ | Bemerkung |
|---|---|---|
| `DiveSiteId` | Guid | PK-Teil |
| `Culture` | string(2) | PK-Teil |
| `Description` | nvarchar(max) | Markdown, eingeschränkt (Absätze, Listen, Fett) |
| `EntryInfo` | string(1000) | Einstieg und Ausstieg |
| `ParkingInfo` | string(1000) | Parkierung und Gebühren |
| `Hints` | string(1000) | Gefahren, Schiffsverkehr, Verbote |
| `DivingBanNote` | string(500) | Erklärung zum Tauchverbot |
| `IsMachineTranslated` | bool | `true`, wenn automatisch übersetzt und noch nicht geprüft |

### DiveSiteRevision

Jede Speicherung im CMS erzeugt eine Revision. Details in `cms.md`.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid | |
| `Number` | int | fortlaufend pro Platz |
| `Snapshot` | nvarchar(max) | JSON des ganzen Platzes inkl. Übersetzungen |
| `Comment` | string(500), optional | Änderungsnotiz |
| `CreatedAt`, `CreatedBy` | | |

### SlugRedirect

| Feld | Typ |
|---|---|
| `OldSlug` | string(120), PK |
| `DiveSiteId` | Guid |

### MediaItem

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid, optional | ein Bild kann auch zu einem See gehören |
| `LakeId` | int, optional | |
| `Kind` | enum | `Photo`, `Video`, `Sketch` |
| `BlobPath` | string(300) | Original in Blob Storage |
| `Width`, `Height` | int | |
| `TakenAt` | DateOnly, optional | aus EXIF |
| `Depth` | int, optional | Aufnahmetiefe in m |
| `Photographer` | string(120) | Namensnennung, Pflicht |
| `PhotographerUrl` | string(300), optional | |
| `SourceUrl` | string(300), optional | z.B. Seite auf tauchfotos.ch |
| `SortOrder` | int | |

Bildlegende und Alternativtext pro Sprache in `MediaItemTranslation` (`Caption`, `AltText`).

### ForecastRun

Ein Lauf des Prognose-Jobs.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `StartedAt`, `FinishedAt` | DateTimeOffset | |
| `Status` | enum | `Running`, `Succeeded`, `PartiallySucceeded`, `Failed` |
| `SourceStatus` | nvarchar(max) | JSON: Status und Datenalter pro Quelle |
| `RawDataPath` | string(300) | Ordner der Rohdaten in Blob Storage |

### DailyForecast

Eine Zeile pro Platz und Tag. PK: (`DiveSiteId`, `Date`).

| Feld | Typ | Bemerkung |
|---|---|---|
| `DiveSiteId` | Guid | |
| `Date` | DateOnly | Lokaltag |
| `DiveIndex` | decimal(3,1) | 0.0–10.0 |
| `SurfaceTemperature` | decimal(4,1), optional | °C |
| `TemperatureAt10m`, `TemperatureAt20m`, `TemperatureAt30m` | decimal(4,1), optional | °C |
| `ThermoclineTop`, `ThermoclineBottom` | decimal(4,1), optional | m |
| `VisibilityIndicator` | enum | `Unknown`, `Poor`, `Moderate`, `Good`, `VeryGood` |
| `VisibilityGoodBelow` | decimal(4,1), optional | ab dieser Tiefe besser (meist unter der Sprungschicht) |
| `WindBeaufort` | int, optional | Maximum im Tauchfenster 08–16 Uhr |
| `WindSpeedKmh` | decimal(4,1), optional | dasselbe Maximum in km/h |
| `WindGustsKmh` | decimal(4,1), optional | stärkste Böe im Tauchfenster, km/h |
| `WindDirection` | int, optional | Grad |
| `WeatherModel` | string(40), optional | Wettermodell, aus dem die Werte stammen (z.B. `meteoswiss_icon_ch2`, `ecmwf_ifs025`) |
| `PrecipitationLast72h` | decimal(5,1), optional | mm |
| `InflowDischargeRatio` | decimal(5,2), optional | aktueller Abfluss geteilt durch Median |
| `AlgaeLevel` | enum | `Unknown`, `Low`, `Medium`, `High` |
| `ReasonCodes` | string(200) | z.B. `CalmLake,ClearBelowThermocline` (für Begründungstexte aus `.resx`) |
| `ModelVersion` | string(20) | z.B. `rules-1.0` |
| `ForecastRunId` | Guid | |
| `ComputedAt` | DateTimeOffset | |

### LakeConditions

Eine Zeile pro See und Tag für die Seen-Übersicht. PK: (`LakeId`, `Date`). Felder: `SurfaceTemperature`, `ClarityTrend` (enum `Rising`, `Stable`, `Falling`, `Unknown`), `LatestSecchiDepth`, `LatestSecchiDate`.

### Report (Phase 1)

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid | |
| `DiveDate` | DateOnly | |
| `VisibilityBand0to10`, `VisibilityBand10to20`, `VisibilityBand20to30`, `VisibilityBandBelow30` | enum, optional | `VeryPoor` bis `VeryGood` |
| `WaterTemperatureAtDepth` | decimal(4,1), optional | |
| `Comment` | string(500), optional | |
| `Culture` | string(2) | Sprache der Person |
| `ClientHash` | string(64) | Hash aus IP und Tag, gegen Missbrauch, keine Klartext-IP |
| `Status` | enum | `Visible`, `Hidden` |
| `CreatedAt` | DateTimeOffset | |

### DiveShop und PartnerListing (Phase 1)

`DiveShop`: `Id`, `Name`, `WebsiteUrl`, `Address`, `Location` (geography), `Phone`, `HasFillingStation`.
`PartnerListing`: `Id`, `DiveShopId`, `DiveSiteId`, `ValidFrom`, `ValidTo`. Ohne gültigen Partner zeigt die Seite die nächste Füllstation aus der Liste der Shops (ohne Hervorhebung).

### BuddyGroup (Phase 1)

`Id`, `NameDe/En/Fr/It`, `WhatsAppUrl`, Zuordnung zu Seen (n:m `BuddyGroupLakes`).

### Benutzer

ASP.NET Core Identity im selben `DbContext`. Rollen: `Editor`, `Admin`. Keine Besucherkonten.

## Indizes

- `DiveSites.Location`: Spatial Index.
- `DiveSites (LakeId, Status)`.
- `DailyForecasts (Date, DiveIndex DESC)` für die Rangliste.
- `Reports (DiveSiteId, DiveDate)`.

## Aufbewahrung

- `DailyForecasts`: 2 Jahre in SQL. Danach nur noch als Rohdaten in Blob Storage.
- Rohdaten in Blob Storage: unbegrenzt (Trainingsdaten für Phase 2). Lifecycle-Regel: nach 90 Tagen in die Cool-Stufe.

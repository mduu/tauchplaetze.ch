# Datenstruktur

Diese Spec beschreibt die Entitäten in Azure SQL. Entitäten und Felder heissen englisch. Ein `DbContext` im Core: `TauchplaetzeDbContext`.

## Grundregeln

- **Geodaten** mit NetTopologySuite (`UseNetTopologySuite()`), SQL-Typ `geography`, SRID 4326 (WGS84). Die Umkreissuche läuft in der Datenbank (`Location.IsWithinDistance(...)`).
- **IDs:** `Guid` (Version 7, sortierbar) für fachliche Entitäten. `int` für kleine Stammdaten (`WaterBody`).
- **Enums** stehen als Text in der Datenbank (`nvarchar(20)`). So bleibt eine Row lesbar, auch wenn sich die Reihenfolge der Werte ändert. Flags-Enums (`Features`) stehen als `int`.
- **Zeit:** `DateTimeOffset` in UTC für Zeitpunkte. `DateOnly` für Prognosetage (Schweizer Lokalzeit, `Europe/Zurich`).
- **Optimistische Sperre:** `rowversion` auf allen Entitäten, die im CMS bearbeitet werden.
- **Slugs:** eindeutig, aus dem deutschen Namen gebildet, ohne Umlaute (`riff-williams-herrliberg`). Ein geänderter Slug erzeugt einen 301-Redirect (Tabelle `SlugRedirects`).
- **Übersetzbare Texte** stehen in eigenen Tabellen `{Entity}Translations` mit `Culture` (`de`, `en`, `fr`, `it`). Deutsch ist Pflicht, die anderen Sprachen sind optional. Fehlt eine Sprache, zeigt die Seite Deutsch mit einem Hinweis (siehe `i18n.md`).
- **Grosse Rohdaten** (Antworten der Datenquellen) liegen in Blob Storage, nicht in SQL. So bleibt die Datenbank klein (Free Offer: 32 GB).

## Entitäten

### WaterBody

Stammdaten der Gewässer, schweizweit: Seen und Flüsse (Entscheid Marc, 17.10.2026). Tabelle `WaterBodies`. Der Seed steht vorerst in `Features/WaterBodies/Seed/water-bodies.json` (MVP: Zürichsee, Zugersee, Rhein). Der Command `SeedWaterBodies` legt fehlende Gewässer an und ändert bestehende nicht. Später kommt der Seed aus dem Gewässernetz von swisstopo (swissTLM3D, `data-sources.md`).

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | int | |
| `Slug` | string(80) | eindeutig, z.B. `zuerichsee` |
| `NameDe` | string(100) | amtlicher Name, Pflicht |
| `NameEn`, `NameFr`, `NameIt` | string(100), optional | Übersetzung, siehe `ubiquitous-language.md`. Fehlt sie, gilt der deutsche Name. Kleine Gewässer haben meist keine. |
| `WaterBodyType` | enum | `Lake`, `River` |
| `Outline` | geography (optional) | Umriss für die Karte |
| `Centroid` | geography | Mittelpunkt |
| `SwissTlmId` | string(40), optional | Kennung im Gewässernetz von swisstopo |
| `AlplakesLakeKey` | string(50), optional | Schlüssel des Sees für 3D-Modell und Satellit (z.B. `zurich`) |
| `AlplakesModel3d` | string(50), optional | Modellname für 3D-Simulationen (z.B. `delft3d-flow`, `mitgcm`) |
| `AlplakesSimstratKey` | string(50), optional | Schlüssel im 1D-Modell Simstrat (z.B. `lowerzurich`). Weicht oft vom 3D-Schlüssel ab. |
| `SatellitePoint` | geography (Point), optional | Standardpunkt seewärts für Satellitenwerte |
| `IsActive` | bool | |

### WaterBodyHydroStation

Zuordnung der BAFU-Stationen zu einem Gewässer. PK: (`WaterBodyId`, `StationId`).

| Feld | Typ | Bemerkung |
|---|---|---|
| `StationId` | string(20) | BAFU-Stationsnummer |
| `Name` | string(100) | |
| `Role` | enum | `Inflow` (Zufluss eines Sees), `River` (Station am Fluss selbst, für Flussplätze) |
| `Weight` | decimal, optional | Anteil am Teilwert «Zuflüsse», nur bei `Inflow` |

### WaterBodyWeatherStation

Zuordnung der SwissMetNet-Stationen zu einem Gewässer. PK: (`WaterBodyId`, `StationId`). Felder: `StationId` (string(10), z.B. `WAE`), `Name`, `SortOrder` (erste Station mit Daten gilt).

### DiveSite

Der Tauchplatz. Nur `Name`, `Location` und `WaterBodyId` sind Pflicht (Ausbaustufe 0). Ausbaustufe (Dokumentation) und Datenstufe (Prognose) sind unabhängig voneinander (`cms.md`, `forecast.md`). Bedeutung der Bewertungen und Abschnitte: siehe `dive-site.md`.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `Slug` | string(120) | eindeutig |
| `Name` | string(120) | Eigenname, wird nicht übersetzt |
| `OtherNames` | nvarchar(1000), optional | weitere Namen als JSON-Array mit Umlauten im Klartext (`dive-site.md`, «Auch bekannt als»). `null` ohne. Ungültiges JSON gilt beim Lesen als keine Namen (Warnung im Log). Teil der Revision. |
| `WaterBodyId` | int | FK `WaterBody` |
| `Location` | geography (Point) | Position des Platzes (für Prognose und Karte) |
| `Municipality` | string(80), optional | Gemeinde |
| `Canton` | string(2), optional | Kürzel, z.B. `ZH` |
| `MaxDepth` | int, optional | Meter |
| `DifficultyRating` | tinyint, optional | 1–5, Stufe |
| `WallRating` | tinyint, optional | 1–5, Steilwand |
| `MarineLifeRating` | tinyint, optional | 1–5, UW-Leben |
| `WrecksRating` | tinyint, optional | 1–5, Wracks und Gegenstände |
| `CurrentRating` | tinyint, optional | 1–5, Stufe |
| `SurroundingsRating` | tinyint, optional | 1–5, Umgebung ausserhalb des Wassers |
| `Access` | enum, optional | `Shore`, `Boat`, `ShoreAndBoat` |
| `Features` | flags enum | `Plateau`, `Cave`, `River`, `IceDiving`, `Training` (Wand und Wrack kommen aus den Bewertungen) |
| `HasDivingBan` | bool | Tauchverbot (dauernd oder zeitweise) |
| `InflowStationOverride` | string(20), optional | BAFU-Station, falls der Platz nahe einer Mündung liegt (überschreibt die Stationen des Sees) |
| `SatellitePoint` | geography (Point), optional | eigener Punkt seewärts für Satellitenwerte. Ohne ihn gilt `WaterBody.SatellitePoint`. |
| `ForecastDisabled` | bool | Prognose für den Platz ausgeschaltet (Datenstufe 0) |
| `DataLevel` | tinyint (0–3), optional | vom Job berechnet, Stand des letzten Laufs, siehe `forecast.md`. `null` bis zum ersten Lauf (noch nicht berechnet). Nicht Teil der Revision. |
| `Status` | enum | `Draft`, `Published`, `Archived` |
| `ContentLevel` | int (0–2) | berechnet und gespeichert, siehe `cms.md` |
| `HeroMediaId` | Guid, optional | Titelbild, ein `MediaItem` des Platzes (ohne FK, Teil der Version) |
| `Sketch` | nvarchar(max), optional | Tauchskizze als JSON: Liste von Elementen mit `id`, `kind` (Name aus `SketchKind`; die Revision speichert die Zahl, darum Werte nie ändern, neue Arten hinten anhängen; eine unbekannte Art fällt beim Lesen weg), `points` (`latitude`, `longitude`), `depth` (m, optional), `texts` (`culture`, `label`). Teil der Revision. `null` ohne Skizze. |
| `CreatedAt`, `UpdatedAt` | DateTimeOffset | |
| `CreatedBy`, `UpdatedBy` | string | Benutzer-ID |
| `RowVersion` | rowversion | |

Alle `…Rating`-Felder haben einen Check Constraint `BETWEEN 1 AND 5`. Im Code sind sie ein Value Object `Rating` (`byte`, validiert 1–5).

### DiveSiteTranslation

| Feld | Typ | Bemerkung |
|---|---|---|
| `DiveSiteId` | Guid | PK-Teil |
| `Culture` | string(2) | PK-Teil |
| `UnderwaterDescription` | nvarchar(max), optional | Unter Wasser. Markdown, eingeschränkt (Absätze, Listen, Fett) |
| `SurroundingsInfo` | string(2000), optional | Umgebung ausserhalb des Wassers |
| `ApproachInfo` | string(2000), optional | Anfahrt und Parkplatz, Gebühren |
| `HazardsInfo` | string(2000), optional | Gefahren, Schiffsverkehr, Verbote |
| `FacilitiesInfo` | string(1000), optional | Restaurants und Toiletten |
| `DivingBanNote` | string(500), optional | Erklärung zum Tauchverbot |
| `IsMachineTranslated` | bool | `true`, wenn automatisch übersetzt und noch nicht geprüft |

### DiveSiteEntryPoint

Einstieg, Ausstieg oder Ort an Land (Parkplatz, Toilette) eines Platzes. Ein Platz hat null bis viele.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid | FK `DiveSite`, Cascade Delete |
| `Location` | geography (Point) | Pin auf der Karte |
| `Kind` | enum | `EntryAndExit` (Standard), `Entry`, `Exit`, `Parking`, `Toilet` |
| `SortOrder` | int | erster Eintrag = Hauptzugang |

Übersetzbar in `DiveSiteEntryPointTranslation` (`EntryPointId`, `Culture`, `Name` string(80) optional, `Description` string(1000) optional).

Der Einstieg gehört zum Aggregat `DiveSite`. Er wird nur über den Platz gespeichert und ist Teil der Revision.

### DiveSiteNotice

Wichtiger Hinweis zu einem Platz (`dive-site.md`, «Hinweise»). Ein Platz hat null bis viele Hinweise.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid | FK `DiveSite`, Cascade Delete |
| `Kind` | enum | `Info`, `Warning`, `Closure` |
| `ValidFrom` | date, optional | erster gültiger Tag, Schweizer Zeit |
| `ValidUntil` | date, optional | letzter gültiger Tag (inklusive) |
| `SortOrder` | int | Reihenfolge der Redaktion |

Übersetzbar in `DiveSiteNoticeTranslation` (`NoticeId`, `Culture`, `Text` string(500)). Deutsch ist Pflicht.

Der Hinweis gehört zum Aggregat `DiveSite` und ist Teil der Revision (Feld `notices` im Snapshot, fehlt bei Versionen ohne Hinweise).

### DiveSiteRevision

Eine Version eines Platzes. Jede Speicherung im CMS erzeugt eine. Ablauf und Datenschutz in `cms.md`, Entscheid in ADR-008.

Die Row in SQL enthält nur die Metadaten. Der Inhalt liegt als JSON in Blob Storage (`revisions/{diveSiteId}/{number}.json`, privat). So bleibt die Datenbank klein. Die aktuelle freigegebene Version steht zusätzlich in den Tabellen `DiveSites`, `DiveSiteTranslations`, `DiveSiteEntryPoints`, `DiveSiteNotices` und `MediaItems`. Nur sie lesen die öffentlichen Seiten.

| Feld | Typ | Bemerkung |
|---|---|---|
| `Id` | Guid | |
| `DiveSiteId` | Guid | |
| `Number` | int | fortlaufend pro Platz, eindeutig mit `DiveSiteId` |
| `BaseNumber` | int, optional | freigegebene Version, auf der die Änderung aufbaut. `null` beim ersten Vorschlag eines neuen Platzes |
| `Status` | enum | `Pending`, `Approved`, `Rejected`, `Withdrawn` |
| `SnapshotPath` | string(300) | Blob-Pfad des Inhalts |
| `SnapshotHash` | string(64) | SHA-256 des Inhalts. Erkennt eine Speicherung ohne Änderung und prüft die Integrität. |
| `SnapshotVersion` | int | Format des JSON. Ein neuer Code liest alle alten Formate. |
| `Comment` | string(500), optional | Änderungsnotiz der Autorin oder des Autors |
| `AuthorId` | string, optional | Konto. `null` nach Löschung des Kontos |
| `CreatedAt` | DateTimeOffset | |
| `ReviewerId` | string, optional | Konto, das freigegeben oder abgelehnt hat |
| `ReviewedAt` | DateTimeOffset, optional | |
| `ReviewComment` | string(500), optional | Begründung bei Ablehnung |
| `RedactedAt`, `RedactedBy` | optional | gesetzt, wenn der Inhalt nachträglich geschwärzt wurde |

- Index `(DiveSiteId, Status)` für die Liste der offenen Vorschläge.
- Reihenfolge beim Speichern: zuerst den Blob schreiben, dann die Row. Ein Blob ohne Row ist harmlos und wird aufgeräumt. Eine Row ohne Blob darf es nicht geben.
- Gleicher `SnapshotHash` wie die Basis: keine neue Version (idempotent).

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
| `WaterBodyId` | int, optional | |
| `Section` | enum (string(20)) | `Underwater` = 0, `Surroundings` = 1 (über Wasser), `Approach` = 2, `EntryPoint` = 3, `Hazards` = 4; siehe `dive-site.md`. Beim Upload gewählt, bei der Freigabe aus der Version übernommen. Der Snapshot einer Version speichert die Zahl: Werte nie ändern, neue Abschnitte nur hinten anhängen |
| `EntryPointId` | Guid, optional | FK `DiveSiteEntryPoint`, nur bei `Section = EntryPoint` |
| `Kind` | enum (string(20)) | `Photo`, `Video` (später). Die Tauchskizze ist kein Medium, sondern `DiveSite.Sketch` |
| `BlobPath` | string(300) | Ordner in Blob Storage (`{mediaId}`): `original.{ext}` nur in `media-pending`, Bildgrössen `{breite}.webp` in `media-pending` und nach der Freigabe in `media` |
| `Width`, `Height` | int | |
| `TakenAt` | DateOnly, optional | aus EXIF |
| `Depth` | int, optional | Aufnahmetiefe in m |
| `Photographer` | string(120) | Namensnennung, Pflicht |
| `PhotographerUrl` | string(300), optional | |
| `SourceUrl` | string(300), optional | z.B. Seite auf tauchfotos.ch |
| `SortOrder` | int, optional | Position in der freigegebenen Version. `null`: Die freigegebene Version verwendet das Foto nicht (keine Galerie) |
| `UploadedBy` | string, optional | Konto. `null` nach Löschung |
| `UploadedAt` | DateTimeOffset | |
| `RightsConfirmed` | bool | Person hat die Rechte und die Nutzung mit Namensnennung bestätigt |
| `IsPublic` | bool | `true`, sobald eine freigegebene Version das Bild verwendet. Vorher liegt das Bild im privaten Container `media-pending`. |

Bildlegende und Alternativtext pro Sprache in `MediaItemTranslation` (`Caption`, `AltText`, je string(300)). Die Zuordnung der Fotos einer Version steht in ihrem Snapshot (`media`: `mediaId`, `section`, `entryPointId`; `heroMediaId`). Leere Liste und leeres Titelbild fehlen im JSON, so bleibt der Hash alter Versionen gleich. Indizes: (`DiveSiteId`, `Section`, `SortOrder`), (`UploadedBy`, `UploadedAt`) für das Rate Limit. Konto löschen: private Fotos samt Dateien weg, öffentliche bleiben mit `UploadedBy = null` und dem Namen des Fotografen.

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
| `DataLevel` | tinyint (0–3) | Datenstufe an diesem Tag |
| `DiveIndex` | decimal(3,1), optional | 0.0–10.0. `null` ohne Wasserdaten (Ausblick) oder ohne Wind |
| `ProfileSource` | enum, optional | `Model3d`, `Model1d`, `RiverStation`, `InSitu` (Phase 1) |
| `SurfaceTemperature` | decimal(4,1), optional | °C |
| `TemperatureAt10m`, `TemperatureAt20m`, `TemperatureAt30m` | decimal(4,1), optional | °C |
| `ThermoclineTop`, `ThermoclineBottom` | decimal(4,1), optional | m |
| `SecchiDepth` | decimal(4,1), optional | geschätzte Sichttiefe aus dem Satellit am Punkt der Quelle, m (Sentinel-3: Seemitte, Sentinel-2: Ufernähe) |
| `SecchiDepthShore` | decimal(4,1), optional | Secchi-Schätzung in Ufernähe (`rules-1.1`, Uferfaktor). Leer bei Rows von `rules-1.0`. |
| `SecchiObservedAt` | DateOnly, optional | Datum des Satellitenbilds |
| `ClaritySource` | enum, optional | `Sentinel2Shore` (Sentinel-2 in Ufernähe, ohne Uferfaktor) oder `Sentinel3Lake` (Sentinel-3 in der Seemitte, mit Uferfaktor), `rules-1.2`. Leer bei älteren Rows: dann Sentinel-3. |
| `VisibilityIndicator` | enum | `Unknown`, `Poor`, `Moderate`, `Good`, `VeryGood` |
| `VisibilityGoodBelow` | decimal(4,1), optional | ab dieser Tiefe besser (meist unter der Sprungschicht) |
| `WindBeaufort` | int, optional | Maximum im Tauchfenster 08–16 Uhr |
| `WindSpeedKmh` | decimal(4,1), optional | dasselbe Maximum in km/h |
| `WindGustsKmh` | decimal(4,1), optional | stärkste Böe im Tauchfenster, km/h |
| `WindDirection` | int, optional | Grad |
| `CurrentSpeed` | decimal(5,3), optional | stärkste Strömung im Tauchfenster, über 5–30 m gemittelt, in m/s (`forecast.md`, «Strömung»). Nur Seen mit 3D-Modell |
| `CurrentDirection` | int, optional | Richtung, in die die Strömung fliesst, Grad von Nord |
| `CurrentLevel` | enum, optional | `Calm`, `Noticeable`, `Strong` |
| `WeatherModel` | string(40), optional | Wettermodell, aus dem die Werte stammen (z.B. `meteoswiss_icon_ch1`, `meteoswiss_icon_ch2`) |
| `PrecipitationLast72h` | decimal(5,1), optional | mm, die 72 h vor dem Tag, ohne den Tag selbst |
| `PrecipitationOnDay` | decimal(5,1), optional | mm im Tauchfenster |
| `InflowDischargeRatio` | decimal(5,2), optional | aktueller Abfluss geteilt durch Median der letzten 30 Tage |
| `Turbidity` | decimal(6,1), optional | Trübung an der Flussstation, nur bei Flussplätzen |
| `AlgaeLevel` | enum | `Unknown`, `Low`, `Medium`, `High` |
| `ReasonCodes` | string(200) | z.B. `CalmLake,ClearBelowThermocline` (für Begründungstexte aus `.resx`) |
| `ModelVersion` | string(20) | z.B. `rules-1.0` |
| `ForecastRunId` | Guid | |
| `ComputedAt` | DateTimeOffset | |

### SiteWeatherOutlook

Das Wetter pro Stunde eines Platzes für die Grafik auf der Platzseite (`forecast.md`, «Wetter pro Stunde»). Eine Zeile pro Platz, jeder Lauf ersetzt sie. PK: `DiveSiteId` (FK `DiveSite`, Cascade Delete).

| Feld | Typ | Bemerkung |
|---|---|---|
| `DiveSiteId` | Guid | |
| `FromDate` | date | erste Stunde ist dieser Tag 00:00 Lokalzeit |
| `Hours` | nvarchar(max) | JSON-Array, 120 Stunden: `localTime`, `temperatureC`, `precipitationMm`, `sunshineMinutes`, `symbol` (Name des Enums `WeatherSymbol`), `isDay`, `windSpeedKmh`, `windGustsKmh`, `windDirection`. Leere Werte fehlen. |
| `ForecastRunId` | Guid | |
| `ComputedAt` | DateTimeOffset | |

JSON statt einer Zeile pro Stunde: Die Seite liest die Stunden immer zusammen, und ein Lauf schreibt so eine Zeile statt 120 pro Platz. Ein neues Symbol im Enum lässt alte Zeilen lesbar; ein Symbol entfernen bricht sie.

### WaterBodyConditions

Eine Zeile pro Gewässer und Tag für die Gewässerseite und die Seen-Übersicht. PK: (`WaterBodyId`, `Date`). Felder: `SurfaceTemperature`, `ClarityTrend` (enum `Rising`, `Stable`, `Falling`, `Unknown`), `LatestSecchiDepth` und `LatestSecchiDate` (aus dem Satellit am `WaterBody.SatellitePoint`), `WaterLevel` (Pegel in m ü. M., optional).

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

### DiveShop (Phase 1)

`DiveShop`: `Id`, `Name`, `WebsiteUrl`, `Address`, `Location` (geography), `Phone`, `HasFillingStation`. Die Platzseite zeigt die nächste Füllstation nach Distanz. Es gibt keine bezahlten Einträge (`vision.md`, «Finanzierung»).

### BuddyGroup (Phase 1)

`Id`, `NameDe/En/Fr/It`, `WhatsAppUrl`, Zuordnung zu Gewässern (n:m `BuddyGroupWaterBodies`).

### Benutzer

ASP.NET Core Identity im selben `DbContext`. Rollen: `Contributor`, `Moderator`, `Admin` (`cms.md`). Besucher brauchen kein Konto. Ein Konto braucht nur, wer beiträgt.

Zusätzliche Felder am Benutzer:

| Feld | Typ | Bemerkung |
|---|---|---|
| `DisplayName` | string(60) | Pseudonym erlaubt, eindeutig |
| `ShowNamePublicly` | bool | Einwilligung, den Anzeigenamen öffentlich in der History zu zeigen. Standard `false` |
| `PreferredCulture` | string(2) | Sprache des CMS und der Mails |
| `BlockedAt`, `BlockedBy` | optional | Konto gesperrt |

## Inhaltspaket

Austausch der öffentlichen Inhalte von Prod nach lokal (ADR-011). ZIP mit JSON (camelCase, Enums als Text):

| Eintrag | Inhalt |
|---|---|
| `manifest.json` | `formatVersion` (1), `createdAt`, `source` (`PublicSite:BaseUri`), Zahl der Gewässer, Plätze, Fotos |
| `water-bodies.json` | Gewässer mit Slug, Namen, Typ, Mittelpunkt, Satellitenpunkt, Schlüsseln von Alplakes, `isActive` und Stationen des BAFU |
| `dive-sites/{id}.json` | Platz: `id`, Gewässer als Slug, `status` (Published, Archived), `oldSlugs`, `content` im Format der Versionen (`DiveSiteRevision`, inkl. Einstiege, Skizze, Hinweise, weitere Namen, Zuordnung der Medien, Titelbild) |
| `photos.json` | Öffentliche Fotos der Plätze: Id, Platz, Bereich, Einstieg, Art, Grösse, Aufnahmedatum, Tiefe, Fotograf, Links, Reihenfolge, Legende und Alternativtext pro Sprache |
| `photos/{id}/{breite}.webp` | Öffentliche Bildgrössen. Keine Originale |

Nicht im Paket: Konten, Autoren, Uploader, Kommentare der Versionen, offene oder abgelehnte Vorschläge, Entwürfe, private Fotos, Mails, Prognosen.

## Indizes

- `DiveSites.Location`: Spatial Index.
- `DiveSites (WaterBodyId, Status)`.
- `DiveSiteEntryPoints (DiveSiteId, SortOrder)`.
- `DiveSiteNotices (DiveSiteId, SortOrder)`.
- `MediaItems (DiveSiteId, Section, SortOrder)`.
- `DailyForecasts (Date, DiveIndex DESC)` für die Rangliste.
- `Reports (DiveSiteId, DiveDate)`.

## Aufbewahrung

- `DailyForecasts`: 2 Jahre in SQL. Danach nur noch als Rohdaten in Blob Storage.
- Rohdaten in Blob Storage: unbegrenzt (Trainingsdaten für Phase 2). Lifecycle-Regel: nach 90 Tagen in die Cool-Stufe.

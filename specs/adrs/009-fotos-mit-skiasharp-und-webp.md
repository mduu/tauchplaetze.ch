# ADR-009: Fotos mit SkiaSharp, nur WebP, Original privat

## Status

**Akzeptiert** (18.10.2026)

## Kontext

- Mitwirkende laden Fotos über und unter Wasser hoch (`cms.md`, «Medien»). Der Core erzeugt daraus Bildgrössen.
- Fotos vom Handy tragen EXIF mit Standort (GPS) und Kamera. Das darf nicht öffentlich werden.
- Das Projekt ist nicht kommerziell. Lizenzen mit Bedingungen für kommerzielle Nutzung wollen wir trotzdem vermeiden.
- Der Host ist Linux (App Service B1). Die Bibliothek muss dort ohne Systempakete laufen.

## Entscheidung

Wir entscheiden uns für **SkiaSharp** (MIT) für Lesen, Drehen und Verkleinern, **MetadataExtractor** (Apache-2.0) für das Aufnahmedatum aus EXIF und **nur WebP** als Ausgabe.

- Grössen 480, 960, 1600 und 2400 px Breite, nie grösser als das Original, Qualität 82.
- SkiaSharp schreibt keine Metadaten. Die Bildgrössen sind damit frei von GPS und Kameradaten.
- Das Original bleibt im privaten Container `media-pending`. Öffentlich werden nur die Bildgrössen.
- WebP können alle aktuellen Browser. Ein zweites Format (JPEG) würde Speicher und Code verdoppeln, ohne Nutzen für unsere Besucher.

## Optionen

### Option A: SkiaSharp (gewählt)

**Vorteile:** MIT-Lizenz, schnell, WebP eingebaut, native Bibliothek für Linux als NuGet (`SkiaSharp.NativeAssets.Linux.NoDependencies`).

**Nachteile:** native Abhängigkeit; HEIC (iPhone) liest SkiaSharp nicht. Die Oberfläche bittet dann um JPEG.

### Option B: ImageSharp

**Vorteile:** rein verwaltet, sehr gute API.

**Nachteile:** Six Labors Split License mit Bedingungen je nach Umsatz und Nutzung. Für ein Hobbyprojekt vermutlich frei, aber eine Bedingung, die wir nicht prüfen wollen.

### Option C: Magick.NET

**Vorteile:** liest fast jedes Format, auch HEIC.

**Nachteile:** grosse native Abhängigkeit, mehr Speicher, mehr Angriffsfläche beim Lesen fremder Dateien.

## Konsequenzen

### Positive Auswirkungen

- Keine Standortdaten in öffentlichen Bildern.
- Ein Format, eine Pipeline, kleine Dateien.

### Negative Auswirkungen / Risiken

- iPhone-Fotos im HEIC-Format müssen vorher als JPEG exportiert werden.
- Ein sehr alter Browser ohne WebP sieht keine Fotos.

## Referenzen

- `specs/cms.md`, «Medien»
- `specs/datastructure.md`, «MediaItem»
- `specs/infrastructure.md`, Container `media` und `media-pending`

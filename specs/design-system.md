# Design-System «Kaltwasser-Nacht»

Das Erscheinungsbild ist **dunkel**. Es erinnert an einen Schweizer See in der Tiefe: dunkles Grün-Schwarz, Licht in Glacier-Cyan, ein Signal in Orange (wie Flossen, Boje, Taucherflagge). Die Werte stammen aus dem Entwurf vom 17.10.2026 (`design/`).

Es gibt im MVP **nur das dunkle Schema**. Ein helles Schema ist nicht geplant.

## Farben

### Flächen und Text

| Token | Wert | Verwendung |
|---|---|---|
| `--color-bg` | `#071417` | Seitenhintergrund |
| `--color-surface` | `#0A1D20` | Karten, Kacheln |
| `--color-surface-raised` | `#0D2427` | hervorgehobene Karte («Beste Wahl»), Hero-Hintergrund |
| `--color-border` | `#1D3E41` | Rahmen von Karten |
| `--color-divider` | `#16312F` | Trennlinien, Balken-Hintergrund |
| `--color-border-strong` | `#2E5552` | Rahmen von Pillen und sekundären Buttons, Achsen |
| `--color-text` | `#E4EFEC` | Haupttext, Titel |
| `--color-text-body` | `#CFE0DC` | Fliesstext |
| `--color-text-muted` | `#A9C2BE` | Nebentext |
| `--color-text-subtle` | `#8FAAA6` | Labels, Einheiten, Fussnoten |

### Akzente

| Token | Wert | Verwendung |
|---|---|---|
| `--color-primary` | `#7FE3D6` | Glacier-Cyan: Links, aktive Pille, Primärbutton, «sehr gut», Datenlinien |
| `--color-primary-hover` | `#B8F2EA` | Hover von Links |
| `--color-signal` | `#FF9A4D` | Signal-Orange: Sprungschicht, «Beste Wahl»-Label, Buddy-Block, Warnungen, Entwurf-Badge |
| `--color-bubble` | `#9FE9DF` | Luftblasen, Hinweis «darunter klarer» |

### Tauch-Index-Skala

Die Stufen unterscheiden sich in der **Helligkeit**, nicht nur im Farbton. Der Index steht immer zusätzlich als Zahl da.

| Token | Wert | Bereich |
|---|---|---|
| `--color-index-high` | `#7FE3D6` | ≥ 8.0 |
| `--color-index-mid` | `#4FB3A9` | 7.0–7.9 |
| `--color-index-low` | `#2F6B66` | < 7.0 (auf Flächen) |
| `--color-index-low-text` | `#5E9C96` | < 7.0 als Text (Kontrast) |

### Illustration (Seequerschnitt, Hero)

| Token | Wert | Schicht |
|---|---|---|
| `--color-lake-epilimnion` | `#1A5053` | warme Oberflächenschicht |
| `--color-lake-thermocline` | `#13393C` | Sprungschicht |
| `--color-lake-hypolimnion` | `#0B272A` | kaltes Tiefenwasser |
| `--color-rock` | `#05161A` | Felswand, Seegrund |
| `--color-wood` | `#B88A5A` | Baumstamm in Skizzen |

## Logo und Favicon

Entwurf vom 17.10.2026, generiert mit Gemini. Die Quellen liegen in `assets/logo/` (PNG und Pixelmator).

| Datei | Verwendung |
|---|---|
| `logo_large2.png` | Logo ohne Text: Berge, Seeschichten, Flosse, oranger Punkt. Im Kopf der Seite neben der Wortmarke (`wwwroot/img/logo-128.webp`, 64 px hoch). |
| `favicon.png` | Vereinfachte Form für kleine Grössen: Welle und Flosse im Kreis. Daraus `favicon.ico` (16, 32, 48 px), `icons/favicon-32.png` und `icons/apple-touch-icon.png` (180 px). |
| `logo_large_preview.jpg` | Idee für Kopf und Wortmarke. Kein Asset für die Seite. |

- Das Logo ist ein Rasterbild. Für scharfe Darstellung in jeder Grösse soll später eine SVG-Fassung entstehen.

## Typografie

| Rolle | Schrift | Gewicht | Grösse |
|---|---|---|---|
| Display (h1) | Bricolage Grotesque | 800 | `clamp(46px, 7.5vw, 104px)`, Zeilenhöhe 0.94, Laufweite −0.035em |
| Titel (h2) | Bricolage Grotesque | 700–800 | 28–52 px, Laufweite −0.02em |
| Grosse Zahlen (Index) | Bricolage Grotesque | 800 | 64–72 px |
| Fliesstext | IBM Plex Sans | 400–600 | 16–19 px, Zeilenhöhe 1.5–1.65 |
| Labels, Daten, Einheiten | IBM Plex Mono | 400–500 | 11–13 px, Grossbuchstaben, Laufweite 0.1–0.14em |

- Alle Schriften stehen unter der SIL Open Font License. Sie werden **selbst gehostet** (`wwwroot/fonts`), nicht von Google Fonts geladen (Datenschutz, Tempo).
- `font-display: swap`.
- Max. Textbreite: 58–70 Zeichen.

## Abstände und Formen

| Token | Wert |
|---|---|
| `--space-gutter` | 24 px (seitlicher Rand, Gitterabstand) |
| `--space-section` | 48–56 px zwischen Abschnitten |
| `--radius-card` | 28 px |
| `--radius-tile` | 20 px |
| `--radius-tile-sm` | 14 px |
| `--radius-pill` | 999 px |
| `--content-max` | 1240 px |

Layout: Raster mit `repeat(auto-fit, minmax(min(100%, 440px), 1fr))` für zweispaltige Bereiche. Auf dem Handy stapelt alles.

## Komponenten

| Komponente | Beschreibung |
|---|---|
| `SiteHeader` | Logo (Kreis mit Bergen, Seeschichten und Flosse, oranger Punkt), Wortmarke «tauchplaetze.ch» in Bricolage Grotesque, Navigation, Sprachwahl |
| `DayPills` | Tagesauswahl. Aktiv: Cyan gefüllt, dunkler Text. Inaktiv: Rahmen `--color-border-strong`. Höhe ≥ 46 px. |
| `BestPickCard` | Seequerschnitt-Illustration oben, darunter Name, Index, Kacheln, Begründung, Aktionen |
| `LakeCrossSection` | SVG: Schichten, Wellenlinie, gestrichelte Sprungschicht in Orange, Temperaturen, Felswand, Tiefenskala |
| `RankingList` | `<ol>` mit Rang, Name, Ort · See, Index-Balken, Begründung, Index |
| `StatTile` | Label (Mono, klein, Grossbuchstaben) und Wert (Bricolage 24–26 px) |
| `TemperatureProfileChart` | SVG: Temperatur (x) über Tiefe (y), Sprungschicht als oranges Band, Werte auf 0/20/30 m |
| `ForecastStrip` | 5 Tage mit Index, Farbe nach Skala, Mini-Balken, Stichwort |
| `SiteSketch` | SVG-Skizze: Ufer, Einstieg (Orange), Tiefenlinien gestrichelt, Wand, Highlights, Nordpfeil |
| `PracticalTile` | Label in Cyan oder Orange (Achtung), Text |
| `BuddyCallout` | Fläche in Signal-Orange, dunkler Text, Buttons dunkel |
| `DataSourceFooter` | Quellen, Sicherheitshinweis, Notfallnummern |
| `PhotoPlaceholder` | gestrichelter Rahmen mit Beschreibung, bis echte Fotos da sind |

## Diagramme

- Datenlinie in `--color-primary`, 3 px, runde Enden.
- Achsen und Hilfslinien in `--color-border-strong` bzw. `--color-divider`.
- Sprungschicht immer als Band in Signal-Orange (ca. 14 % Deckkraft) mit Beschriftung.
- Jedes Diagramm hat eine Textalternative und zeigt die Kernwerte zusätzlich als Text.

## CSS-Startpunkt

```css
:root {
  --color-bg: #071417;
  --color-surface: #0A1D20;
  --color-surface-raised: #0D2427;
  --color-border: #1D3E41;
  --color-divider: #16312F;
  --color-border-strong: #2E5552;
  --color-text: #E4EFEC;
  --color-text-body: #CFE0DC;
  --color-text-muted: #A9C2BE;
  --color-text-subtle: #8FAAA6;
  --color-primary: #7FE3D6;
  --color-primary-hover: #B8F2EA;
  --color-signal: #FF9A4D;
  --color-bubble: #9FE9DF;
  --color-index-high: #7FE3D6;
  --color-index-mid: #4FB3A9;
  --color-index-low: #2F6B66;
  --color-index-low-text: #5E9C96;

  --font-display: "Bricolage Grotesque", system-ui, sans-serif;
  --font-body: "IBM Plex Sans", system-ui, sans-serif;
  --font-mono: "IBM Plex Mono", ui-monospace, monospace;

  --radius-card: 28px;
  --radius-tile: 20px;
  --radius-tile-sm: 14px;
  --radius-pill: 999px;
  --space-gutter: 24px;
  --content-max: 1240px;

  color-scheme: dark;
}

body {
  margin: 0;
  background: var(--color-bg);
  color: var(--color-text);
  font-family: var(--font-body);
}
```

## Admin (MudBlazor)

Das MudBlazor-Theme übernimmt dieselben Tokens: `Background` = `--color-bg`, `Surface` = `--color-surface`, `Primary` = `--color-primary`, `Secondary` = `--color-signal`, `TextPrimary` = `--color-text`. Dark Mode fest eingestellt.

## Was vermeiden

- Verläufe als Hintergrundflächen, Glassmorphism, Emoji als Icons.
- Reines Schwarz oder reines Weiss.
- Rot/Grün als einziges Unterscheidungsmerkmal.
- Stockfotos aus dem Meer (Korallen). Es geht um Schweizer Seen.

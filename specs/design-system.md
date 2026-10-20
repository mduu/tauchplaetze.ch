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
| `--color-chart-surface` | `rgb(127 227 214 / 0.05)` | Hinterlegte Fläche der Mini-Diagramme |
| `--color-chart-grid` | `rgb(127 227 214 / 0.1)` | Hilfslinien der Mini-Diagramme |

### Hinweise

| Token | Wert | Verwendung |
|---|---|---|
| `--color-notice-info` | `#7FE3D6` | Hinweis «Info» |
| `--color-notice-warning` | `#FF9A4D` | Hinweis «Warnung» |
| `--color-notice-closure` | `#FF6F61` | Hinweis «Sperrung», einzige rote Farbe |
| `--color-sun` | `#E8C25A` | Sonne in der Wetter-Grafik (Balken, Symbol) |
| `--color-rain` | `#5AA9E6` | Regen in der Wetter-Grafik (Balken, Tropfen, Achse) |
| `--color-sketch-wall` | `#3B4B49` | Steilwand und Kante in der Skizze |
| `--color-sketch-line` | `#B8740F` | Leine in der Skizze |
| `--color-sketch-plants` | `#2F8F55` | Pflanzenfeld in der Skizze |
| `--color-place-parking` | `#2F6BC4` | Parkplatz auf der Karte (Schild «P», blau wie das Signal), Text `--color-place-parking-text` `#FFFFFF` |
| `--color-depth-line` | `#0B6E99` | Tiefenlinien auf Karte und Skizze (alle 10 m kräftiger), Beschriftung «20 m» auf `--color-depth-label-bg` (`rgb(255 255 255 / 0.75)`) |

Banner: Fläche `--color-surface` mit 10 % der Farbe, Rand links 5 px in der Farbe, sonst Rahmen mit 45 % der Farbe. Label in IBM Plex Mono, Grossbuchstaben, in der Farbe. Text in `--color-text`, bei einer Sperrung fett.

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
| `--color-difficulty-easy` | = `--color-primary` | Schwierigkeit 1–2 |
| `--color-difficulty-medium` | = `--color-sun` | Schwierigkeit 3 |
| `--color-difficulty-hard` | = `--color-signal` | Schwierigkeit 4–5 |

### Karten

- Punkte: Tauchplatz in der Farbe seiner Index-Stufe (`--color-index-*`, ohne Index `--color-text-subtle`), Gewässer in `--color-primary`. Rand `--color-bg`. Im CMS ist der Pin `--color-signal`.
- Beschriftung wie ein Tooltip, dauerhaft sichtbar: Pille in `--color-bg` mit Rahmen `--color-border-strong` und Pfeil auf den Punkt. Name in IBM Plex Sans 600, 13 px. Bei Plätzen folgt der heutige Index in IBM Plex Mono in der Farbe der Stufe (`--color-index-low-text` für tiefe Werte). Ein Klick öffnet die Seite.
- Namen der Plätze (mit Index) erst ab Zoom 13 dauerhaft. Darunter nur die Punkte in der Farbe ihrer Index-Stufe; Name und Index erscheinen beim Darüberfahren oder beim ersten Antippen, das zweite Antippen öffnet den Platz.
- Überdeckung (`map.js`, `declutter`): Nach jeder Bewegung bleiben nur Beschriftungen stehen, die keine wichtigere überdecken. Reihenfolge: der eigene Platz, Einstiege und Orte, dann der bessere Index, die Tiefen zuletzt. Gilt für alle Karten (Verzeichnis, Gewässer, Lage, Editoren). Kein zusätzliches Clustering.
- Tiefen: Beschriftung ab Zoom 15, bis Zoom 16 nur jede zweite Linie (10, 20, 30 m), ab Zoom 17 alle.
- Die Farben liest `map.js` aus den Tokens. Keine Farbwerte im JavaScript.

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
| `--space-gutter` | 24 px, bis 420 px Breite 16 px (seitlicher Rand, Gitterabstand) |
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
| `LakeCrossSection` | SVG: Schichten, Wellenlinie, Temperaturen, Felswand. Mit Ober- und Unterkante (Beste Wahl) die Sprungschicht als Band in Signal-Orange in Tiefe (0 bis 40 m), Kanten als Linien, links die Tiefen «12 m» und «18 m» in Orange (Mono); liegen die Kanten näher als 4 m, eine Beschriftung «13–14 m». Durchmischt: kein Band, «Keine Sprungschicht, der See ist durchmischt». Ohne Werte (Platzhalter) die gestrichelte Linie als Illustration. |
| `RankingList` | `<ol>` mit Rang, Name, Ort · See, Index-Balken, Begründung, Index |
| `StatTile` | Label (Mono, klein, Grossbuchstaben) und Wert (Bricolage 24–26 px) |
| `DischargeTile` | `StatTile` über die ganze Breite der Kacheln. Einordnung in den Farben des Index (sehr gering und gering `--color-index-high`, mittel `--color-index-mid`, stark `--color-index-low-text`), daneben der Wert in `--color-text-muted`. «i» und Legende wie bei `SiteRatingsBlock`. Trendpfeil, steigend in `--color-signal`. Kurve als SVG, serverseitig, 64 px hoch: Linie und Fläche (12 %) in `--color-primary`, Median gestrichelt in `--color-border-strong`, Achse «vor 48 h», «vor 24 h», «jetzt» in Mono. Mit Vorhersage: die Zeitachse reicht bis zum Ende der Vorhersage, die Vorhersage als gestrichelte Linie (5/4) in `--color-primary` ab dem letzten Messwert, eine feine Linie «jetzt» in `--color-border-strong`, Achse «vor 48 h», «jetzt» (an seiner Stelle), Wochentag des letzten Tags; Legende mit gestricheltem Strich, «nicht geprüft» in `--color-signal`. In der Tagesleiste `day-discharge` in Mono 11 px in den Farben der Einordnung. Tagesmittel als 7 Balken in `--color-primary` mit Wert und Wochentag. «Messwert veraltet» in `--color-signal`. |
| `TemperatureProfileChart` | SVG: Temperatur (x) über Tiefe (y), Sprungschicht als oranges Band, Ober- und Unterkante auf der Tiefenachse in `--color-signal` (bei weniger als 2.5 m Abstand eine Beschriftung «13–15 m»); ein Achsenwert, der eine Kante überdecken würde, entfällt. Werte auf 0/10/20/30 m. Dieselbe Regel wie `LakeCrossSection` (`ThermoclineLabels`). |
| `WeatherChart` | SVG, serverseitig, in der Kachel «Die nächsten 5 Tage». Auf dem Desktop reicht die Kachel so weit wie die linke Spalte (Kacheln und Profil), die Grafik ist entsprechend hoch (Temperaturband rund 240 px, Symbole 26 px). Schmal scrollt sie in der Kachel: Raster von 2 Stunden, Symbol nur bei Wechsel mit gepunkteter Linie dazwischen, Wind alle 6 Stunden. Eigene Wettersymbole (Sonne, Mond, Wolke, Nebel, Niesel, Regen, Schauer, Schnee, Gewitter), Windpfeile, Sonnenbalken (`--color-sun`), Temperaturlinie (`--color-signal`), Regenbalken (`--color-rain`), Nacht in `--color-bg`. Legende darunter als Text, ohne eigene Quellenzeile (die Quellen stehen einmal unter dem Bedingungsblock). |
| `ForecastStrip` | 5 Tage mit Index, Farbe nach Skala, Mini-Balken, Stichwort |
| `Finder` (Verzeichnis) | Suchfeld als Pille, Eigenschaften als Chips (gewählt in `--color-primary`), Liste mit Index rechts, Karte mit Gruppen: Kreis in `--color-primary` mit Anzahl in Mono, Rand `--color-bg` |
| `SiteRatingsBlock` | Eine Kachel mit einer Zeile pro bewerteter Skala (Entscheid Marc, 19.10.2026: «brauchen viel Platz und stehen unaligned aus»). Zeile als Raster: Name (Mono, klein, Grossbuchstaben, darf umbrechen), Skala (Kreise in `--color-primary` für alle Skalen ausser der Schwierigkeit, auch für die Strömung; die Schwierigkeit als Balken, eingefärbt nach Wert: 1–2 `--color-difficulty-easy`, 3 `--color-difficulty-medium`, 4–5 `--color-difficulty-hard`, die Nummern der Legende im «i» gleich; Entscheid Marc, 19.10.2026), Zahl und Kurztext, «i» rechts mit der Legende als Popover. Desktop ab 900 px zwei Spalten, darunter eine Spalte; bis 520 px Name und «i» oben, Skala und Wert darunter, Wert und «i» rechtsbündig. Linien zwischen den Zeilen in `--color-border`. |
| `SiteSketch` | Ebene auf der Leaflet-Karte (`sketch.js`): Symbole als runde Schilder (`--color-surface-raised`, Farbe pro Art: Baumstamm `--color-wood`, Besonderheit `--color-signal`, Gefahr `--color-notice-closure`, sonst `--color-primary`), Steilwand dick und Kante gestrichelt in `--color-sketch-wall`, Route gepunktet in `--color-primary`, Leine in `--color-sketch-line`, Pflanzenfeld als Fläche in `--color-sketch-plants`. Die Linienfarben sind dunkel, weil Landeskarte und Luftbild hell sind. |
| `SitePhotos` | Galerie: Titel wie «Der Platz», Abschnitt als Mono-Label in Grossbuchstaben, Raster `repeat(auto-fill, minmax(min(100%, 280px), 1fr))`, Bild 3:2 mit `object-fit: cover`, Rahmen `--color-border`, Radius `--radius-tile`. Legende in `--color-text-body`, Fotograf in `--color-text-subtle`. |
| `PracticalTile` | Label in Cyan oder Orange (Achtung), Text |
| `BuddyCallout` (`.callout`, alle Hinweisboxen: «Kennst du diesen Platz?», «Über» › Prognose, offener Vorschlag, Plätze in der Nähe) | Fläche `--color-surface`, Rahmen 2 px in `--color-signal`, Text `--color-text-body`, Titel in `--color-signal`, Links fett und unterstrichen in `--color-primary`. Keine orange Fläche: Sie blendete und wurde übersehen (Marc, 19.10.2026). Kontrast Text 13:1, Titel 8:1 (WCAG AA) |
| `DataSourceFooter` | Quellen, Sicherheitshinweis, Notfallnummern |
| `SiteTextBlock` | Redaktioneller Text: Titel im Stil von `.tile-label` (Mono 12 px, Grossbuchstaben, `--color-primary`; Gefahren in `--color-signal`), optional Hinweis klein und kursiv in `--color-text-subtle` («Dieser Text ist nur auf Deutsch verfügbar.» bzw. «Automatisch übersetzt.»), Text bis 72 Zeichen breit, 17 px, Zeilenhöhe 1.6. `UnderwaterDescription` mit eingeschränktem Markdown (Absätze, Listen mit «- » oder «* », Fett mit `**`), die übrigen Texte nur Absätze und Zeilenumbrüche (`SiteTextMarkup`). Alles andere bleibt Text, HTML wird escaped. |
| `PhotoPlaceholder` | gestrichelter Rahmen mit Beschreibung, bis echte Fotos da sind |

## Diagramme

- Datenlinie in `--color-primary`, 3 px, runde Enden.
- Achsen und Hilfslinien in `--color-border-strong` bzw. `--color-divider`.
- Sprungschicht immer als Band in Signal-Orange (ca. 14 % Deckkraft) mit Beschriftung und den Tiefen von Ober- und Unterkante.
- Jedes Diagramm hat eine Textalternative und zeigt die Kernwerte zusätzlich als Text.
- **`LineChart`** (Verlauf des Index, Jahr über Jahr, Mini-Diagramme): SVG serverseitig, ohne JavaScript. `preserveAspectRatio="none"` mit `vector-effect: non-scaling-stroke`, damit die Linie bei jeder Breite gleich dick bleibt. Tageswerte als Punkte (Linien der Länge null mit runden Enden, 35 % Deckkraft), geglättete Linie als monotone Kurve (`ChartGeometry`, schiesst nie über Minimum oder Maximum hinaus), 2.5 px. Eine Lücke von mehr als 7 Tagen bricht die Linie ab. Beschriftungen als HTML über dem SVG in Mono 11 px: Achse links für die erste Reihe, rechts für die zweite, Monate oder Wochen unten (schmal nur jede zweite). Index-Diagramme (Kopf des Bedingungsblocks, «Jahr über Jahr») haben eine feste Achse 0–10, 10 ist der beste Wert (Entscheid Marc, 19.10.2026). Sicht, Wind und Algen skalieren auf ihre Werte. Farben pro Reihe (`data-tone`): Index `--color-primary`, Sicht `--color-rain`, Wind `--color-sun`, frühere Jahre `--color-border-strong`. Regelwechsel gestrichelt in `--color-signal`. 180 px hoch (Jahr über Jahr), als Mini-Diagramm 36 px ohne Achsen (Entscheid Marc, 19.10.2026): auf einer hinterlegten Fläche (`--color-chart-surface`, Radius 10 px), zwei feine Hilfslinien (`--color-chart-grid`), Fläche unter der Kurve mit Verlauf in der Linienfarbe von 25 % nach 0 %, Endpunkt als Punkt mit dem letzten Wert rechts daneben in Mono 11 px in der Linienfarbe (Index «Ø 6.4» als Mittel über 7 Tage, Sicht «5.4 m», Wind «30 km/h», Algen als Stufe). In einer Kachel mit Zeitraum darunter («letzte 30 Tage»), im Kopf des Bedingungsblocks 200 px breit neben dem Index mit Start- und Enddatum in Mono 10 px. Die Linienfarben erreichen auf der Fläche mehr als 4.5:1.

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

## Formulare (Bearbeiten-Modus, neuer Platz)

Bausteine in `Components/Forms`, nur Tokens, kein MudBlazor:

| Baustein | Aussehen |
|---|---|
| `FormText`, `FormTextArea`, `FormNumber`, `FormSelect` | Label in Mono, Grossbuchstaben, `--color-text-muted`; Feld mit `--color-surface`, Rand `--color-border`, Radius `--radius-tile-sm`, mindestens 44 px hoch; Fokus mit Rand in `--color-primary` |
| `FormChip` | Eigenschaft als Pille; an: Fläche `--color-primary`, Text `--color-bg` |
| `LevelPicker` | Bewertung als Kachel: Name, «i» mit Legende (wie auf der Platzseite), Knöpfe «keine» und 1 bis 5. Qualität füllt die Knöpfe bis zum Wert in `--color-primary`, Stufe (Schwierigkeit, Strömung) in `--color-signal`. Darunter der Kurztext. |
| `LanguageTabs` | Tabs DE, EN, FR, IT wie bei den Texten, Pfeiltasten wechseln die Sprache |
| `ToolPicker` | Werkzeuge der Karte als Pillen in Gruppen (`FormChip`-Stil), `aria-pressed`; ein zweiter Klick hebt die Wahl auf |
| `CoordinateFields`, `LocationField` | Breite und Länge als Textfelder mit Punkt; `LocationField` zeigt darüber die Karte zum Klicken |
| Karten-Editor | Einstiege als weisse Punkte mit Rand, Orte als Schild wie auf der Platzseite, gewählt mit Ring in `--color-signal`; Listen als Kacheln, das gewählte Element mit Rand in `--color-signal`; «Entfernen» und «Löschen» in `--color-signal` |
| Leiste des Bearbeiten-Modus | klebt oben, `--color-surface-raised`, unterer Rand `--color-signal`, «Bearbeitungsmodus» in `--color-signal` |

## Admin (MudBlazor)

Moderation (`/admin`, Seitenleiste) und Plattform (`/platform`, Navigation oben, Kopf mit Strich in `--color-signal`) nutzen dasselbe Theme.


Das MudBlazor-Theme übernimmt dieselben Tokens: `Background` = `--color-bg`, `Surface` = `--color-surface`, `Primary` = `--color-primary`, `Secondary` = `--color-signal`, `TextPrimary` = `--color-text`. Dark Mode fest eingestellt.

## Was vermeiden

- Verläufe als Hintergrundflächen, Glassmorphism, Emoji als Icons.
- Reines Schwarz oder reines Weiss.
- Rot/Grün als einziges Unterscheidungsmerkmal.
- Stockfotos aus dem Meer (Korallen). Es geht um Schweizer Seen.

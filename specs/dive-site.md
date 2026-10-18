# Tauchplatz-Inhalte

Diese Spec beschreibt die redaktionellen Angaben zu einem Tauchplatz. Die Prognose steht in `forecast.md`. Die Felder stehen in `datastructure.md`.

## Grundsatz

Alle Angaben in dieser Spec sind optional (Verzeichnis zuerst, siehe `cms.md`). Ein Platz ohne diese Angaben nimmt trotzdem an der Prognose teil. Die Seite zeigt nur Abschnitte mit Inhalt.

## Übersicht

| Angabe | Bewertung 1–5 | Text | Bilder | Koordinate |
|---|---|---|---|---|
| Schwierigkeit | ja | – | – | – |
| Steilwand | ja | – | – | – |
| UW-Leben | ja | – | – | – |
| Wracks und Gegenstände | ja | – | – | – |
| Strömung | ja | – | – | – |
| Umgebung (ausserhalb des Wassers) | ja | ja | ja | – |
| Anfahrt und Parkplatz | – | ja | ja | – |
| Einstiege (einer oder mehrere) | – | ja, pro Einstieg | ja, pro Einstieg | ja, pro Einstieg |
| Unter Wasser | – | ja | ja | – |
| Gefahren | – | ja | ja | – |
| Restaurants und Toiletten | – | ja | – | – |

Texte sind übersetzbar (Deutsch Pflicht, sobald ein Text erfasst ist; EN, FR, IT optional). Bilder sind `MediaItem` mit Fotograf, Legende und Alternativtext pro Sprache.

## Bewertungen

Jede Bewertung ist eine ganze Zahl von 1 bis 5 oder leer. Leer heisst «nicht bewertet». Leer heisst nicht «0» oder «keine».

Die Skalen haben zwei Richtungen:

- **Stufe** (Schwierigkeit, Strömung): höher heisst anspruchsvoller. Die Seite zeigt eine Stufe, keine Sterne.
- **Qualität** (Steilwand, UW-Leben, Wracks und Gegenstände, Umgebung): höher heisst mehr oder schöner. Die Seite zeigt Punkte (z.B. ●●●○○).

Die Seite zeigt immer auch die Zahl und den Kurztext der Stufe (Barrierefreiheit, siehe `ux.md`). Die Kurztexte stehen in `DiveSiteStrings.resx` (`Rating_{Scale}_{Value}`, z.B. `Rating_Current_3`).

### Schwierigkeit (`DifficultyRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | sehr einfach | Flacher Einstieg, geringe Tiefe, keine Strömung. Geeignet für Ausbildung. |
| 2 | einfach | Einfacher Einstieg. Interessante Teile in moderater Tiefe. |
| 3 | mittel | Einstieg oder Tiefe verlangt Erfahrung. Orientierung nötig. |
| 4 | anspruchsvoll | Grosse Tiefe, Strömung oder schwieriger Einstieg. Nur mit Erfahrung. |
| 5 | sehr anspruchsvoll | Mehrere Erschwernisse zusammen. Nur für Erfahrene mit guter Planung. |

Filter und Tags fassen die Werte zusammen: 1–2 = Einsteiger, 3 = Fortgeschrittene, 4–5 = Erfahrene (Begriffe aus `ubiquitous-language.md`).

### Steilwand (`WallRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | flach | Flacher Grund, kein Gefälle. |
| 2 | Hang | Sanfter Hang. |
| 3 | steiler Hang | Steiler Hang mit einzelnen Wandstücken. |
| 4 | Wand | Wand über eine längere Strecke. |
| 5 | Steilwand | Senkrechte oder überhängende Wand in grosse Tiefe. |

Ab Wert 3 zeigt die Seite den Tag «Wand».

### UW-Leben (`MarineLifeRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | kaum | Kaum Fische oder Pflanzen. |
| 2 | wenig | Einzelne Fische, wenig Bewuchs. |
| 3 | typisch | Typisch für den See (z.B. Barsche, Hechte, Krebse, Pflanzen). |
| 4 | reich | Viele Fische, dichter Bewuchs. |
| 5 | herausragend | Schwärme, grosse Fische oder seltene Arten regelmässig. |

### Wracks und Gegenstände (`WrecksRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | keine | Keine Wracks, keine Gegenstände. |
| 2 | einzelne | Einzelne kleine Gegenstände. |
| 3 | einige | Mehrere Gegenstände oder ein kleines Wrack. |
| 4 | sehenswert | Ein bekanntes Wrack oder viele Gegenstände (z.B. Statuen). |
| 5 | herausragend | Grosses Wrack oder aussergewöhnliche Objekte. Der Hauptgrund für den Tauchgang. |

Ab Wert 3 zeigt die Seite den Tag «Wrack».

### Strömung (`CurrentRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | keine | Keine Strömung. |
| 2 | schwach | Selten und schwach spürbar. |
| 3 | mässig | Oft spürbar. Tauchgang gegen die Strömung beginnen. |
| 4 | stark | Stark, je nach Wasserstand. Planung und Erfahrung nötig. |
| 5 | sehr stark | Fluss oder Mündung. Nur mit Erfahrung im Strömungstauchen. |

Die Bewertung beschreibt die übliche Lage. Der aktuelle Abfluss kommt aus der Prognose (`forecast.md`).

### Umgebung (`SurroundingsRating`)

| Wert | Kurztext | Bedeutung |
|---|---|---|
| 1 | karg | Strasse, Industrie oder kaum Platz zum Umziehen. |
| 2 | einfach | Zweckmässig, wenig Aufenthaltsqualität. |
| 3 | angenehm | Platz zum Umziehen und Verweilen. |
| 4 | schön | Wiese, Badeplatz oder schöne Aussicht. |
| 5 | herausragend | Landschaft, die allein den Ausflug lohnt. |

## Einstiege und Orte

Ein Platz hat null, einen oder mehrere Einstiege und Orte (`DiveSiteEntryPoint`). Die Redaktion erfasst sie im selben Tab.

- Jeder Eintrag hat eine Koordinate (Pin auf der Karte).
- Art: Wege ins Wasser (`EntryAndExit`, `Entry`, `Exit`) oder Orte an Land (`Parking`, `Toilet`). Weitere Orte an Land kommen als neue Art dazu.
- Optional: Name (z.B. «Treppe beim Bootshaus»), Beschreibung, Bilder.
- Die Reihenfolge legt die Redaktion fest. Der erste Einstieg ist der Hauptzugang.
- Für die Ausbaustufe zählt nur ein beschriebener Weg ins Wasser, kein Parkplatz und keine Toilette.
- Karte: Einstiege als Punkt, Parkplatz als blaues Schild «P», Toilette als Schild «WC», alle mit Name und Art beschriftet.
- `DiveSite.Location` bleibt die Position für Prognose und Karte. Ohne Einstieg zeigt die Karte nur diese Position.

## Hinweise

Ein Platz kann wichtige Hinweise haben (`DiveSiteNotice`, Entscheid Marc, 18.10.2026). Beispiele: «Tauchplatz ist bis 1.4.2027 gesperrt», «Parkplatz bis Sommer 2028 nicht zugänglich».

| Art | Code | Bedeutung | Farbe |
|---|---|---|---|
| Info | `Info` | gut zu wissen | `--color-notice-info` (Glacier-Cyan) |
| Warnung | `Warning` | Einschränkung, Tauchen möglich | `--color-notice-warning` (Signal-Orange) |
| Sperrung | `Closure` | am Platz nicht tauchen | `--color-notice-closure` (Korallenrot) |

- Text pro Sprache, höchstens 500 Zeichen. Deutsch ist Pflicht. Fehlt eine Übersetzung, erscheint der deutsche Text.
- Optional ein Zeitraum: «gültig ab» und «gültig bis» (inklusive, Schweizer Zeit). Ausserhalb ist der Hinweis unsichtbar, ohne neue Version. Ein unscharfes Ende («bis Sommer 2028») steht im Text, das Datum bleibt leer.
- Hinweise sind Teil des Inhalts: Mitwirkende schlagen sie vor, Moderatoren geben sie frei (ADR-008).
- Eine gültige Sperrung nimmt den Platz an diesem Tag aus «Beste Wahl» und Rangliste (`forecast.md`). Verzeichnis und Platzseite zeigen ihn weiter.
- Mehrere Hinweise: schwerster zuerst (Sperrung, Warnung, Info), sonst in der Reihenfolge der Redaktion.

## Tauchskizze

Die Tauchskizze (`SiteSketch`) zeigt den Platz unter Wasser. Alle Skizzen sehen gleich aus, darum zeichnet die Redaktion sie im CMS und lädt kein Bild hoch.

- **Erfassen:** Tab «Skizze» im CMS. Karte (Landeskarte oder Luftbild) mit Tiefenlinien. Art oben wählen, dann auf die Karte klicken: ein Klick setzt ein Symbol, Linien und Flächen entstehen Punkt für Punkt und enden mit Doppelklick oder «Fertig». Ein Klick auf ein Element wählt es: Punkte lassen sich ziehen, dazu Art (gleiche Form), Tiefe und Beschriftung pro Sprache. Escape bricht ab.
- **Speichern:** als Teil der Version (`DiveSiteContent.Sketch`), mit Vorschlag und Freigabe wie bei allen Inhalten. Die Tabelle hält die aktuelle Skizze als JSON (`DiveSite.Sketch`, `datastructure.md`).
- **Symbole (Punkte):** Skulptur, Baumstamm, Wrack, Gegenstand, Felsblock, Besonderheit (frei benannt), Gefahr. Einstiege und Orte kommen aus «Einstiege und Orte».
- **Linien:** Steilwand, Kante, Tauchroute, Leine. **Flächen:** Pflanzenfeld.
- **Regeln:** höchstens 200 Elemente, je höchstens 200 Punkte, alle Punkte höchstens 2 km vom Platz, Tiefe 0 bis 200 m, Beschriftung bis 80 Zeichen. Die Art legt die Form fest (Punkt genau 1, Linie ab 2, Fläche ab 3 Punkten). Sonst Fehler `InvalidSketch`.
- **Beschriftung:** in der Sprache der Seite, sonst Deutsch, sonst der Name der Art. Mit Tiefe «Madonna · 12 m».
- **Tiefe:** Tiefenlinien aus swissBATHY3D, wo vorhanden (22 Seen), alle 5 m bis 40 m, dazu 50 und 60 m. Sie entstehen einmalig mit `tools/bathymetry/contours.cs` und liegen als statische Kacheln bei der App (`data-sources.md`, «Tiefenlinien»). Sonst Tiefenangaben von Hand an Symbolen und Linien.
- **Darstellung:** Auf der Platzseite liegt die Skizze als Ebene «Skizze» auf der Karte «Lage und Einstiege», die Tiefenlinien als Ebene «Tiefenlinien». Beide sind an und lassen sich abschalten. Darunter «Skizze, nicht zur Navigation». Gleiche Zeichnung im CMS und auf der Seite (`wwwroot/js/sketch.js`, Tokens in `design-system.md`).

## Bilder pro Abschnitt

Jedes Bild eines Platzes gehört zu genau einem Abschnitt (`MediaItem.Section`): `Underwater`, `Surroundings`, `Approach`, `EntryPoint`, `Hazards`. Bilder eines Einstiegs verweisen zusätzlich auf den Einstieg (`EntryPointId`). Das Hero-Foto ist meist ein Bild aus `Underwater`.

## Darstellung auf der Tauchplatzseite

Reihenfolge der Abschnitte siehe `ux.md`. Kurz:

1. Hero mit Tags (aus Bewertungen und `Features`).
2. Gültige Hinweise als Banner, schwerster zuerst.
3. Tauchbedingungen und Prognose.
4. Bewertungen als kompakter Block (nur bewertete Skalen).
5. Unter Wasser: Text und Galerie.
6. Gefahren: Text und Bilder. Gut sichtbar, nicht eingeklappt.
7. Einstiege: Karte mit allen Pins, pro Einstieg Text und Bilder.
8. Anfahrt und Parkplatz, Umgebung, Restaurants und Toiletten.

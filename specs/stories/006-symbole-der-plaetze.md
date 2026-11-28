# Story 006: Symbole der Plätze

**Status:** umgesetzt (28.11.2026), noch nicht deployt.

## Ziel

Wer Plätze vergleicht, sieht auf einen Blick, was ein Platz bietet: Defibrillator, Toilette, Parkplatz, Inklusion, Schwierigkeit und mehr. Wie ein Hotelführer zeigt jede Kachel eines Platzes eine Reihe von Symbolen (Entscheid Marc, 27.11.2026).

Anlass: Die Einträge des SUSV (Story 004) stehen auf der Platzseite als kleine Zeilen und gehen unter, etwa «Inklusion» neben «Bergsee».

## Symbole

Ein Symbol erscheint nur, wenn die Daten es belegen. Fehlt es, heisst das «unbekannt», nie «nein». Die Reihenfolge ist fest. Die Tabelle zeigt sie.

| # | Symbol | Bedeutung | Quelle |
|---|---|---|---|
| 1 | Sperrung oder Warnung heute | Am Platz gilt heute ein Hinweis | Hinweise der Redaktion und Einschränkungen des SUSV (`dive-site.md`, «Hinweise»). Ersetzt das heutige Warnzeichen der Listen. |
| 2 | Schwierigkeit | leicht (1–2), mittel (3), schwer (4–5) | Bewertung `DifficultyRating` |
| 3 | Tiefe | maximale Tiefe als Zahl («42 m») | `MaxDepth` |
| 4 | Defibrillator (AED) | Gerät am Platz | bestätigter Eintrag `Defibrillator` des SUSV |
| 5 | Toilette | Toilette am Platz | Eintrag `MobileToilet` des SUSV oder Ort `Toilet` |
| 6 | Parkplatz | Parkplatz erfasst | Ort `Parking` |
| 7 | Flaggenhalter | fester Halter für die Taucherflagge | Ort `FlagHolder` |
| 8 | Inklusion | Inklusions-Tauchplatz | bestätigter Eintrag `InclusiveSite` des SUSV |
| 9 | Ausbildung | geeignet für Ausbildung | Eigenschaft `Training` |
| 10 | Steilwand | Steilwand ab Bewertung 3 | `WallRating` |
| 11 | Wrack | Wracks und Gegenstände ab Bewertung 3 | `WrecksRating` |
| 12 | Fluss | Platz an einem Fluss | Gewässertyp oder Eigenschaft `River` |
| 13 | Bergsee | See ab 700 m ü. M. | `WaterBody.Elevation` (`MountainLakes.FromMetres`) |

- **Nicht dabei:** Restaurant (keine strukturierten Daten, Entscheid Marc, 27.11.2026) und Höhle (kommt in der Schweiz kaum vor). Die Eigenschaft `Cave` bleibt als Filter im Verzeichnis.
- **Anzahl:** Eine Kachel zeigt alle zutreffenden Symbole, ohne feste Grenze (Entscheid Marc, 27.11.2026: mehr als 6). Schmal brechen sie in eine zweite Zeile um.

## Stellen

| Stelle | Darstellung |
|---|---|
| Platzseite, Hero | Die Symbolleiste gross, mit Text neben jedem Symbol. Sie ersetzt die Tags «Bergsee» und «Inklusion». |
| Verzeichnis (Finder), Treffer | Symbole ohne Text, unter Name und Gewässer. Ersetzt die heutigen Tags der Eigenschaften. |
| Gewässerseite, Liste der Plätze | wie im Verzeichnis |
| Startseite, «Beste Wahl» und Rangliste | wie im Verzeichnis |

Die Eigenschaften des SUSV auf der Platzseite (Story 004, `SusvFeatures`) bleiben. Sie tragen den Link zum SUSV. Ihre Symbole sind der Anfang dieser Leiste.

## Gestaltung

- Eigene Symbole als SVG im Stil der Wettersymbole (`design-system.md`, `WeatherChart`): Linien, 20 px, `currentColor`. Keine Emoji (`design-system.md`, «Was vermeiden»).
- Farbe: neutral in `--color-text-muted`. Sperrung und Warnung in ihrer Farbe (`--color-notice-closure`, `--color-notice-warning`), Schwierigkeit in der Farbe der Stufe.
- Jedes Symbol hat einen Text für Screenreader und einen Tooltip («Defibrillator (AED) am Platz»). Die Leiste ist eine Liste (`ul`) mit `aria-label` «Ausstattung und Eigenschaften».
- **Legende:** im Verzeichnis unter den Filtern, aufklappbar («Was bedeuten die Symbole?»).
- Komponenten `SiteSymbolBar` und `SiteSymbolLegend` in `design-system.md`.

## Daten

Der Core liefert pro Platz `SiteSymbols`: Hinweis von heute (nur Sperrung oder Warnung), Schwierigkeit als Stufe (`DifficultyBand`), maximale Tiefe, Höhe des Bergsees und die übrigen Symbole (`SiteSymbol`, Enum in der festen Reihenfolge). Die Regel steht an einer Stelle (`SiteSymbolRules`) und gilt für Verzeichnis, Gewässerseite, Rangliste und Hero. Die Listen laden dafür die Orte (`DiveSiteEntryPoints`) und die bestätigten Projekte des SUSV aller Plätze auf einmal (`SiteSymbolSources`), wie die Hinweise in Story 003 (`SiteRestrictions`).

## Akzeptanzkriterien

1. Jede Kachel eines Platzes zeigt die zutreffenden Symbole in der festen Reihenfolge, auf allen Stellen gleich.
2. Ein Symbol erscheint nur mit Daten. Ein unbestätigter Eintrag des SUSV ergibt kein Symbol.
3. Die Platzseite zeigt die Symbole gross mit Text im Hero.
4. Jedes Symbol hat Tooltip und Text für Screenreader in vier Sprachen.
5. Das Verzeichnis hat eine Legende.
6. Die Ausbaustufe eines Platzes ändert sich nicht.
7. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Entscheide

Marc hat die Vorschläge am 28.11.2026 übernommen:

| Frage | Entscheid |
|---|---|
| Schwierigkeit: ein Symbol mit drei Stufen oder drei Symbole? | Ein Symbol (Flosse) mit 1, 2 oder 3 gefüllten Punkten, Farbe der Stufe |
| Tiefe als Zahl auf der Kachel? | Ja, als kurzer Text «42 m» |
| Ersetzt die Leiste die Tags der Eigenschaften im Verzeichnis ganz? | Ja. Auch das Warnzeichen «Gesperrt» der Listen entfällt, die Leiste zeigt es als erstes Symbol |
| Toilette aus einem Ort «Toilette» der Redaktion oder nur vom SUSV? | Beides |

Die Karte hat keine Popups, nur Beschriftungen mit Name und Index. Sie zeigt keine Symbole, sonst wird sie unruhig.

## Abhängigkeiten

Braucht Story 004 (Projekte des SUSV). Unabhängig von Story 005.

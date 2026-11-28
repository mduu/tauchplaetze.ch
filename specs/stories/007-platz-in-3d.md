# Story 007: Der Platz in 3D

**Status:** ✅ erledigt, deployt am 28.11.2026.

## Ziel

Wer einen Tauchgang plant, sieht den Platz unter Wasser als Block im See: Gelände, Tiefenlinien, die Objekte der Tauchskizze und die Route, der ein Taucher folgt. So wird sichtbar, wo die Wand abfällt, wie tief ein Objekt liegt und wie die Route verläuft (Entscheid Marc, 28.11.2026: die Optik ist wichtig).

Ein Sticker «3D» auf der Karte «Lage und Einstiege» weist auf die Funktion hin.

Vor der Umsetzung entstand eine Vorschau mit Seegrund, Land und Luftbild von Fischbalme (Thunersee) und Beispieldaten für Objekte und Route. Sie liegt als Artifact bei Marc.

## Sticker

- Nur an Plätzen mit Gelände (siehe «Daten»). Ohne Gelände kein Sticker und keine 3D-Ansicht.
- Oben links an der Kachel «Lage und Einstiege» (die Ebenenauswahl bleibt allein oben rechts), leicht schräg, ragt über den Rand. Rund, in den Farben der App: dunkle Fläche mit Rand in Cyan, oben dem Rand entlang «Platz erkunden», in der Mitte ein kleiner Block wie in der 3D-Ansicht mit laufender Route und Blasen, unten die Pille «3D» über dem Rand (Entscheid Marc, 28.11.2026: der holografische Schimmer passte nicht zum Rest der App, das Motiv selbst soll 3D vermitteln).
- Schmal (Handy) kleiner, 78 px, nur Block und Pille «3D», ohne Text im Bogen (Entscheid Marc, 28.11.2026).
- Ein Klick öffnet die 3D-Ansicht an der Stelle der Karte. Ein zweiter Knopf «Karte» führt zurück.
- Mit `prefers-reduced-motion` stehen Route und Blasen still.

## 3D-Ansicht

- **Block:** Der Platz als Ausschnitt von 240 × 240 m um die Koordinate des Platzes. Seitenwände aus Gestein, darüber die Wasserwand halb durchsichtig, oben die Wasseroberfläche mit leichtem Glitzern. Das Land am Ufer hat seine echte Höhe und trägt das Luftbild von swisstopo (SWISSIMAGE): Hang, Strasse, Parkplatz und Bäume sind erkennbar (Entscheid Marc, 28.11.2026).
- **Gelände:** Farbe nach Tiefe, von Türkis im Flachen zu fast Schwarz in der Tiefe (Tokens der Seegrafik). Im Flachen spielen Lichtmuster (Caustics), sie verblassen mit der Tiefe. Steile Flächen sind dunkler.
- **Tiefenlinien:** im Gelände gezeichnet, alle 5 m, jede zehnte Linie heller. Gleiche Werte wie die Ebene «Tiefenlinien» der Karte. An den Linien steht dezent in Blau die Tiefe («25m»), an zwei Stellen des Blocks, auf einem halb durchsichtigen dunklen Schild (Entscheid Marc, 28.11.2026).
- **Einstiege:** Jeder Einstieg ins Wasser trägt die Taucherflagge «Alpha» (weiss am Mast, blau mit Schwalbenschwanz), quer zum Ufer gestellt, mit Etikett. Liegt die Koordinate im Wasser, steht die Flagge am nächsten Ufer in höchstens 40 m; ohne Ufer in der Nähe schwimmt sie auf einer Boje (Entscheid Marc, 28.11.2026: die Flagge schwebte über dem Wasser und war nicht die Alpha-Flagge).
- **Objekte der Skizze** (`SiteSketch`): Jede Art hat eine eigene einfache Form, etwa Boot, Stamm, Skulptur, Gefahr mit Netz. Über jedem Objekt steht ein Etikett mit Name und Tiefe, verbunden mit einer feinen Linie.
  - Mit Tiefe: Das Objekt steht in dieser Tiefe. Die Tiefe erscheint in Cyan.
  - **Ohne Tiefe:** Das Objekt liegt auf dem Grund. Die Tiefe kommt aus dem Gelände und erscheint in Orange mit «≈» (Tooltip «Tiefe aus dem Gelände»).
  - Linien (Steilwand, Kante, Leine) und Flächen (Pflanzen, Bojenfeld) liegen auf dem Gelände.
- **Route:** Die Linie `Route` der Skizze, 1.5 bis 2 m über Grund, geglättet, an der Oberfläche höchstens bis 0.3 m. Darstellung kräftig, damit sie als Route erkennbar ist (Entscheid Marc, 28.11.2026):
  - ein Kern in `--color-signal` mit Strichen, die in Richtung der Route fliessen,
  - ein Glühen um den Kern, durch das in Abständen helle Pulse laufen,
  - kleine Pfeile, die der Route entlang wandern,
  - hinter dem Taucher eine kurz nachleuchtende Spur und Funken, die verglühen,
  - auf dem Grund ein schwacher Schatten der Route.
- **Taucher:** Eine Figur, die als Mensch erkennbar ist (Entscheid Marc, 28.11.2026: keine Form, die an eine Bombe erinnert): Rumpf mit Jacket, Flasche auf dem Rücken in `--color-signal`, Kopf mit Maske und Atemregler, Arme am Körper, Beine mit Knien und breiten Flossen in `--color-signal`. Sie bewegt sich mit gleichmässigem Tempo entlang der Route. Die Beine schlagen im Wechsel, Ober- und Unterschenkel leicht versetzt. Ein Ring auf dem Grund unter dem Taucher pulsiert. Alle 3 bis 4 Sekunden steigt ein Schwall Blasen auf, dazwischen einzelne Blasen. Die Blasen wachsen beim Aufsteigen leicht und hinterlassen an der Oberfläche kleine Ringe.
- **Anzeige:** oben links die Tiefe des Tauchers und die Strecke («72 von 284 m Route»). Unter dem Block das Tiefenprofil der Route mit einer Marke, die mit dem Taucher wandert.
- **Bedienung:** frei drehbar, auch von unten (Entscheid Marc, 28.11.2026). Mit der Maus: Ziehen dreht, rechte Maustaste verschiebt, Ctrl/⌘ und Mausrad zoomen; das Mausrad allein scrollt die Seite. Auf Touch: ein Finger dreht, zwei Finger zoomen und verschieben (Entscheid Marc, 28.11.2026, nach dem Test auf dem iPhone: Drehen mit zwei Fingern war ungewohnt). Über der Ansicht scrollt die Seite darum nicht; schmal ist die Ansicht niedriger (höchstens 58 % der Höhe des Bildschirms), damit darüber und darunter Platz zum Scrollen bleibt. Knöpfe «Pause» und «Abspielen», «Drehen» (der Block kreist langsam), Ansichten «Schräg», «Wand» (frontal auf die steilste Stelle bei der Route, unter der Oberfläche) und «Von oben». Der Wechsel zwischen den Ansichten ist ein kurzer Kameraflug. Die Ansicht «Schräg» ist nah am Platz, die vordere Ecke des Blocks darf angeschnitten sein (Entscheid Marc, 28.11.2026).
- **Symbol für freies Drehen:** eine Kugel mit gebogenen Pfeilen in vier Richtungen. Es steht auf dem Knopf «Drehen» und im Hinweis unten rechts im Block (Maus: «Ziehen dreht · Ctrl/⌘ + Mausrad zoomt», Touch: «Ein Finger dreht, zwei Finger zoomen»). Der Hinweis verschwindet beim ersten Ziehen und leuchtet kurz auf, wenn jemand ohne Ctrl/⌘ scrollt. Mit «weniger Bewegung» steht das Symbol still.
- **Legende:** Tiefenskala, Route, Objekt mit Tiefe, Tiefe aus Gelände. Quellenangabe «swissBATHY3D, swissALTI3D, SWISSIMAGE © swisstopo» und «Skizze, nicht zur Navigation».
- **Ohne Route:** Gelände und Objekte ohne Taucher, ohne Profil.
- **Weniger Bewegung** (`prefers-reduced-motion`): keine Animation, der Taucher steht an einer festen Stelle, keine Blasen, kein Kameraflug.
- **Ohne WebGL:** Die Karte bleibt, kein Sticker.

## Daten

- **Bibliothek:** three.js r147 (globaler Build `three.min.js` und `OrbitControls.js` aus `examples/js`, MIT-Lizenz) unter `wwwroot/lib/three`. Ab r148 gibt es `examples/js` nicht mehr; ein Upgrade braucht ES-Module.
- **Gelände:** aus dem 1-m-Raster von swissBATHY3D (`data-sources.md`, «Tiefenlinien»), nicht aus den Tiefenlinien. Das Skript `tools/bathymetry` erzeugt pro Platz ein Raster: 2 m Abstand, Höhe in Dezimetern als `Int16`, 121 × 121 Punkte, rund 29 KB. Ablage als statische Datei bei der App (`wwwroot/terrain/{e}_{n}.bin` mit `index.json`, `data-sources.md`, «Höhenraster für die 3D-Ansicht»).
- **Land:** aus dem Höhenmodell swissALTI3D (STAC `ch.swisstopo.swissalti3d`, 2 m, gleiche Höhenbasis wie swissBATHY3D), im selben Raster wie der Seegrund (Entscheid Marc, 28.11.2026). Nullpunkt ist die Seehöhe aus dem Index: darüber Land, darunter Seegrund. Die Uferlinie ergibt sich daraus, wo das Land die Seehöhe erreicht. Das Skript lädt nur die Kacheln um die Plätze. swissALTI3D ist der nackte Boden: Bäume und Häuser zeigt nur das Luftbild.
- **Luftbild:** SWISSIMAGE als Kacheln von swisstopo (WMTS), wie die Ebene «Luftbild» der Karte. Der Browser lädt die Kacheln, die Seite selbst ruft swisstopo nicht ab.
- **Skizze und Route:** aus der veröffentlichten Version (`DiveSite.Sketch`), dieselben Daten wie die Ebene «Skizze» der Karte. Kein neues Feld.
- **Tiefe aus dem Gelände:** im Browser aus dem Raster (bilinear), nicht gespeichert. Die Skizze bleibt, wie die Redaktion sie erfasst hat.

## Was vorbereitet wird und was der Browser rechnet

Kein fertiges 3D-Modell pro Platz. Vorbereitet werden nur die Daten, die Szene baut der Browser bei jedem Öffnen neu (wie in der Vorschau, in wenigen Millisekunden).

| Teil | Wann | Wo |
|---|---|---|
| Höhenraster (Seegrund und Land, 121 × 121 Punkte zu 2 m, `Int16` in Dezimetern, rund 29 KB) | einmal pro Platz, neu nur bei neuem Platz oder verschobener Koordinate | Skript, Ablage in `wwwroot/terrain` |
| Luftbild | beim Öffnen | Kacheln von swisstopo (WMTS), lädt der Browser wie die Ebene «Luftbild» |
| Skizze, Objekte, Route | beim Öffnen | aus der veröffentlichten Version, wie die Ebene «Skizze». Eine geänderte Skizze braucht keine neue Vorbereitung |
| Tiefe aus dem Gelände, Glättung der Route, Netz, Animation | beim Öffnen | im Browser |

- **Erste Stufe:** Das Skript (`tools/bathymetry`) rechnet die Raster für alle Plätze an Seen mit swissBATHY3D, wie heute die Tiefenlinien. Ein neuer Platz bekommt seine 3D-Ansicht nach dem nächsten Lauf des Skripts und dem Deploy. Bis dahin fehlt der Sticker.
- **Später:** ein Command im Core (`GenerateSiteTerrain`), den der Worker nach dem Speichern eines neuen Platzes startet. Ablage in Blob Storage statt bei der App. Dafür braucht der Worker die Kacheln von swisstopo (je rund 0.5 bis 1 MB). Erst nötig, wenn neue Plätze häufig sind.
- **Grenzen:** Plätze an Flüssen und an Seen ohne swissBATHY3D haben keine 3D-Ansicht.

## Aufwand und Risiken

Schätzung für die Umsetzung mit Claude Code (28.11.2026). Die Vorschau enthält Szene, Shader und Animation schon, die Umsetzung übernimmt sie.

| Schritt | Claude Code | Marc |
|---|---|---|
| Skript: Raster aus swissBATHY3D (lokal schon im Cache) und swissALTI3D, Index, Ablage | 1 bis 2 h, dazu rund 30 bis 60 min Download der Kacheln um rund 370 Plätze (umgesetzt: 236 Plätze mit 232 Rastern) | – |
| 3D-Ansicht: Skript aus der Vorschau, three.js bei der App, Laden erst beim Klick, Sticker auf der Karte | 2 bis 3 h | – |
| Texte in vier Sprachen, Tests (Raster lesen, Tiefe aus dem Gelände), Specs, Echolot | 1 bis 2 h | – |
| Ansehen auf Handy und Desktop, Wünsche zur Optik, zweite Runde | 1 bis 2 h | 1 bis 2 h |

Zusammen rund ein Arbeitstag mit zwei bis drei Runden Feedback. Den grössten Teil der Zeit braucht das Ansehen und Abstimmen der Optik auf echten Geräten.

- Risiko Handy: schwache Grafik. Das Netz hat rund 29 000 Dreiecke (121 × 121 Punkte), das ist wenig. Die Ansicht rendert nur, solange sie sichtbar ist.
- Risiko Grösse: three.js lädt erst beim Klick, rund 150 KB komprimiert, bei der App abgelegt.
- Kosten im Betrieb: keine. Die Raster sind statisch, die Luftbilder lädt der Browser bei swisstopo.

## Technik

- Statische Seite wie bisher (Static SSR, Output Cache). Die 3D-Ansicht ist ein eigenes Skript, das erst beim Klick auf den Sticker lädt, zusammen mit three.js (MIT-Lizenz, bei der App abgelegt, keine CDN).
- Rendern nur, solange die Ansicht sichtbar ist (IntersectionObserver) und der Tab aktiv ist. Pixelverhältnis höchstens 2.
- Keine Abfrage bei swisstopo aus der Seite (`AGENTS.md`).
- Texte (Sticker, Knöpfe, Anzeige, Legende) aus den Ressourcen in vier Sprachen.

## Akzeptanzkriterien

1. An Plätzen mit Gelände klebt der Sticker «3D» auf der Karte «Lage und Einstiege». Ohne Gelände fehlt er.
2. Der Klick öffnet den Platz als Block mit Gelände, Tiefenlinien alle 5 m und Wasseroberfläche.
3. Objekte mit Tiefe stehen in ihrer Tiefe. Objekte ohne Tiefe liegen auf dem Grund und zeigen die Tiefe aus dem Gelände mit «≈» in Orange.
4. Die Route ist als fliessende Spur erkennbar. Ein Taucher folgt ihr, mit Blasen bis zur Oberfläche.
5. Tiefe des Tauchers und Tiefenprofil wandern mit.
6. Der Block dreht frei, auch von unten. Drehen, Verschieben, Zoomen und die Ansichten gehen mit Maus und Touch. Mit der Tastatur: Tab erreicht den Block und die Knöpfe, Pfeiltasten drehen, Plus und Minus zoomen, die Ansichten sind Knöpfe.
7. Das Land zeigt das Luftbild.
8. Mit «weniger Bewegung» bleibt alles still.
9. Die Platzseite lädt ohne Klick kein zusätzliches Skript.
10. Alle Texte in vier Sprachen.
11. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Entscheide

| Frage | Entscheid |
|---|---|
| Land am Ufer aus swissALTI3D oder vereinfacht? | swissALTI3D (Entscheid Marc, 28.11.2026) |
| Land hinter einem steilen Hang abschneiden? | Nein. Bei Fischbalme steigt der Hang auf gut 50 m und verdeckt die Wand nicht |
| Überhöhung der Tiefe? | Keine, echte Proportionen |
| Taucher als Figur oder als leuchtender Punkt? | Figur, als Mensch erkennbar (Entscheid Marc, 28.11.2026) |
| Neue Plätze: Raster beim Erfassen oder mit dem Skript? | Skript, wie bei den Tiefenlinien |

Noch offen, nicht umgesetzt:

| Frage | Vorschlag |
|---|---|
| Knopf «Vollbild»? | Ja, als Ergänzung zur Ansicht an der Stelle der Karte |
| Symbol «3D» in den Listen (Story 006)? | Ja, als Symbol |
| Raster für Plätze, die nur in Prod angelegt werden? | Später ein Job, der aus Prod liest |

## Abhängigkeiten

Baut auf der Tauchskizze und den Tiefenlinien auf (`dive-site.md`, «Tauchskizze»). Mit dem Symbol «3D» in den Listen auch auf Story 006, sonst unabhängig von den Stories 001 bis 006.

# Story 002: SUSV-Einträge importieren und bestätigen

**Status:** geplant (24.11.2026). Entscheid: ADR-013.

## Ziel

Ein Job übernimmt täglich die Einschränkungen und Projekte, die der SUSV pro Tauchplatz veröffentlicht. Die Moderation bestätigt jeden Eintrag einmal und ordnet ihn Plätzen zu. Diese Story zeigt noch nichts auf den öffentlichen Seiten. Das folgt in den Stories 003 und 004.

## Zusage und Bedingungen

- **Zusage:** Die Geschäftsführung des SUSV erlaubt die Übernahme der Publikationen zu Einschränkungen (Antwort auf die Anfrage von Marc, November 2026). Sie wünscht, dass die Massnahmen des SUSV an den Plätzen sichtbar sind.
- **Quellenangabe:** Jeder Eintrag trägt das Logo des SUSV und einen Link auf seine Seite bei susv.ch. Die Seite «Über» nennt den SUSV bei den Datenquellen.
- **Logo:** Der SUSV hat das Logo geliefert und die Nutzung erlaubt (25.11.2026). Korrekt ist die Fassung mit dem Namen des Verbands in drei Sprachen und den Webadressen. Wo der Platz fehlt, ist das Signet ohne Namen erlaubt. Das Logo bleibt unverändert. Das Signet gibt es als SVG im Kopf von susv.ch. Die gelieferten Dateien liegen in `assets/partner/susv/` (`susv-signet.jpg`, `susv-logo-mit-namen.jpg`).
- **Abruf:** Der SUSV erlaubt den regelmässigen Abruf seiner Seite (25.11.2026). Die Quelle ändert nicht täglich.
- **Vollständigkeit:** Der SUSV veröffentlicht nur, was ihm gemeldet wird. Die Liste der Einschränkungen ist nie vollständig.
- **Texte:** unverändert, gekürzt nur am Satzende. Fotos übernehmen wir nicht.

## Bestätigung durch die Moderation

Ein Eintrag erscheint erst nach der Bestätigung (siehe «Seite `/admin/susv`»).

- **Plätze:** Die Moderation wählt einen oder mehrere veröffentlichte Plätze. Der Core schlägt die Plätze im Umkreis von 500 m um die Koordinate vor, den nächsten zuerst.
- **Art** (nur `Restriction`, Pflicht): Info, Warnung oder Sperrung, wie bei den Hinweisen. Die Quelle kennt keine Art.
- **Zeitraum** (nur `Restriction`, optional): «gültig ab» und «gültig bis» wie bei den Hinweisen. Dazu «jedes Jahr»: Dann zählen nur Tag und Monat, und der Zeitraum darf über den Jahreswechsel gehen (Winterbetrieb). «Jedes Jahr» braucht beide Daten.
- **Ignorieren:** Ein Eintrag ohne Bezug zu einem Platz bleibt unsichtbar und steht nicht mehr in der Liste der offenen Einträge.
- Die Moderation kann Plätze, Art und Zeitraum später ändern.

### Nach der Bestätigung

- Ändert der SUSV Titel oder Text, geht die Änderung ohne Freigabe online. Plätze, Art und Zeitraum bleiben. Der Eintrag ist als «geändert seit der Prüfung» markiert, bis die Moderation ihn ansieht.
- Entfernt der SUSV den Eintrag, verschwindet er von der Platzseite.
- Die Moderation bekommt in beiden Fällen eine Mail.

## Seite `/admin/susv`

Die Moderation bestätigt jeden Eintrag einmal (ADR-013).

- **Liste:** zuerst offene Einträge (`Pending`), dann bestätigte mit «geändert seit der Prüfung», dann die übrigen. Filter nach Thema und Status. Die Navigation der Moderation zeigt die Zahl der offenen und geänderten Einträge.
- **Pro Eintrag:** Thema, Titel, Text, Datum der Quelle, Link auf susv.ch, kleine Karte mit der Koordinate und den vorgeschlagenen Plätzen (Umkreis 500 m, mit Distanz).
- **Bestätigen:** Plätze wählen (Vorschlag angekreuzt, Suche nach weiteren). Bei einer Einschränkung dazu Art (Pflicht), «gültig ab», «gültig bis» und «jedes Jahr». Der Eintrag ist danach sofort auf den Platzseiten.
- **Ignorieren:** für Einträge ohne Bezug zu einem Platz. Lässt sich zurücknehmen.
- **Geändert seit der Prüfung:** Die Seite zeigt alten und neuen Text nebeneinander. «Gesehen» nimmt die Markierung weg. Art, Zeitraum und Plätze lassen sich dabei ändern.
- **Entfernte Einträge** stehen 30 Tage in der Liste, mit «bei der Quelle entfernt». Danach löscht der Job sie.
- «Jetzt abgleichen» (nur Admin) startet den Import. Läuft schon einer, startet nichts.
- Nur `Moderator` und `Admin`. Der Core prüft die Rechte selbst. Bestätigen und Ändern leeren den Output Cache der betroffenen Plätze, der Startseite und des Verzeichnisses.

Mail an die Moderation und Rollen: `cms.md`, «Benachrichtigungen» und «Rollen».

## Quelle

Die Karten auf susv.ch laden ihre Einträge als JSON. Diese Schnittstelle nutzt der Import.

### Themen der Quelle (Stand 24.11.2026)

| Thema | Code | Filter | Einträge | Seite bei susv.ch |
|---|---|---|---|---|
| Tauchverbote und Einschränkungen | `Restriction` | 15547 | 23 | `/de/tauchen/aktuelles/tauchverbote-und-einschraenkungen/` |
| Defibrillatoren | `Defibrillator` | 12630 | 14 | `/de/tauchen/bereiche/tauchplatzfoerderung/defibrillatoren/` |
| Mobile Toiletten | `MobileToilet` | 12629 | 5 | `…/tauchplatzfoerderung/mobile-toiletten/` |
| Förderprojekte | `SupportProject` | 12628 | 5 | `…/tauchplatzfoerderung/abgeschlossene-projekte/` |
| Inklusions-Tauchplätze | `InclusiveSite` | 70259 | 13 | `…/tauchplatzfoerderung/inklusions-tauchplaetze/` |

Ein neues Thema beim SUSV erscheint nicht von selbst. Es braucht einen neuen Code und eine Stelle auf der Platzseite. Cleanups haben keine Einträge pro Platz.

### Endpunkt

- `POST https://susv.ch/{lang}/api/v1/map-result` mit `{"mapFilter":[{Filter}],"lang":"{lang}"}`. Antwort: `content.entries` und `content.groups`.
- Die Website nutzt den Endpunkt für ihre Karten. Er ist nicht als öffentliche API beschrieben.
- Felder pro Eintrag, die wir lesen: `id`, `title`, `teaserText`, `date`, `lat`, `lng`, `detailUrl`. Bilder (`imgUrl`, `coverUrl`) übernehmen wir nicht.
- Der volle Text steht nur auf der Seite unter `detailUrl` (HTML).

### Eigenheiten (Abruf vom 24.11.2026)

- **Ein Call pro Thema.** Ein Eintrag kann zu mehreren Themen gehören (derselbe Platz mit Defibrillator und Förderprojekt). Ein Call mit mehreren Filtern liefert ihn nur einmal.
- **Koordinate des Platzes, nicht des Geräts.** Einschränkung und Defibrillator am selben Platz haben dieselbe Koordinate. Wir setzen darum keinen Pin für Defibrillator oder Toilette.
- **Sprachen:** Deutsch, Französisch und Italienisch sind gepflegt. Englisch liefert meist den deutschen Text. Wir holen `de`, `fr` und `it`.
- **Titel:** Ein Titel kann Tabulatoren enthalten oder nur aus einem allgemeinen Wort bestehen. Der Job ersetzt Leerraum durch ein Leerzeichen. Bei den Projekten zeigt die Platzseite den Titel nicht.
- **Keine Art, kein Zeitraum:** Beides steht nur im Text. Die Moderation setzt es (ADR-013).
- **Abgeschlossene Projekte:** Die Seite nennt 19 Plätze als Text. Nur 5 davon sind Einträge der Karte. Die übrigen übernimmt der Job nicht.

## Der Job

Command `ImportSusvEntries`, aufgerufen von `SusvWorker` (`DailyWorker`, ADR-006).

- **Zeitplan:** täglich 04:30 Lokalzeit.
- **Lock:** `locks/susv`. Hält ein anderer Lauf den Lock, endet der zweite sofort.
- **Calls:** 5 Themen × 3 Sprachen = 15 Calls. Dazu die Detailseite eines Eintrags pro Sprache, wenn er neu ist oder sich Titel, Kurztext oder Datum geändert haben. Jeden Montag holt der Job alle Detailseiten, weil sich der Text ohne diese Felder ändern kann (rund 180 Calls).
- **Text:** der Haupttext der Detailseite ohne HTML, höchstens 2000 Zeichen. Lässt er sich nicht lesen, gilt der Kurztext.
- **Abgleich:** Upsert nach `(SourceId, Topic)`. Fehlt ein Eintrag in zwei Läufen nacheinander, gilt er als entfernt (`RemovedAt`). Erscheint er wieder, ist er wieder da, mit seiner Bestätigung.
- **Schutz:** Liefert ein Thema keinen Eintrag, obwohl es vorher welche hatte, gilt der Call als gescheitert. So entfernt eine leere Antwort nichts.
- **Englisch:** über die automatische Übersetzung aus dem Deutschen (`cms.md`, «Automatisch übersetzen»), nur bei neuem oder geändertem Text.
- **Rohdaten:** `forecast-raw/susv/{date}/{topic}-{lang}.json`.
- **Mail:** eine Mail pro Lauf an die Moderation, wenn Einträge neu, geändert oder entfernt sind (`cms.md`, «Benachrichtigungen»).
- **Ausfall:** Der letzte Stand bleibt. Echolot: verspätet ab 36 h ohne erfolgreichen Lauf, gestört ab 72 h (`ux.md`).

Entität: `SusvEntry` (`datastructure.md`). Slice und Handler: `cms.md`, «Slices im Code».

## Akzeptanzkriterien

1. Der Job holt täglich alle fünf Themen in `de`, `fr` und `it` und speichert neue und geänderte Einträge.
2. Fällt die Quelle aus oder liefert ein Thema eine leere Antwort, bleibt der letzte Stand. Kein Eintrag gilt deswegen als entfernt.
3. Ein Eintrag gilt erst als entfernt, wenn er in zwei Läufen nacheinander fehlt.
4. Die Moderation sieht offene Einträge mit vorgeschlagenen Plätzen und kann bestätigen, ändern und ignorieren. Mitwirkende haben keinen Zugang.
5. Eine Einschränkung lässt sich nur mit einer Art bestätigen. «Jedes Jahr» braucht beide Daten.
6. Neue, geänderte und entfernte Einträge lösen eine Mail pro Lauf an die Moderation aus.
7. Das Echolot zeigt den Stand des Imports: verspätet ab 36 h, gestört ab 72 h.

## Abhängigkeiten und offene Punkte

| Punkt | Nächster Schritt |
|---|---|
| **Abgeschlossene Projekte ohne Daten:** Die Seite des SUSV nennt 19 Plätze als Text, nur 5 sind Einträge der Karte. Art und Jahr der Massnahme fehlen. | Der SUSV klärt intern, ob und wie er die übrigen nachträgt (Antwort vom 25.11.2026). Bis dahin erfasst die Redaktion sie von Hand. |

Hängt von keiner anderen Story ab. Die Stories 003 und 004 bauen darauf auf.

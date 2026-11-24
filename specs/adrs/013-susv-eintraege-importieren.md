# ADR-013: Hinweise und Projekte des SUSV importieren

## Status

**Akzeptiert** (24.11.2026, Entscheid Marc)

## Kontext

- Der SUSV (Schweizer Unterwasser-Sport-Verband) veröffentlicht Tauchverbote und Einschränkungen pro Tauchplatz. Dazu kommen Projekte der Tauchplatzförderung: Defibrillatoren, mobile Toiletten, Förderprojekte und Inklusions-Tauchplätze.
- Der SUSV hat zugesagt, dass tauchplaetze.ch diese Publikationen übernehmen darf (November 2026). Er wünscht, dass seine Massnahmen an den Plätzen sichtbar sind.
- Die Karten auf susv.ch laden ihre Einträge als JSON. Die Schnittstelle ist nicht als öffentliche API beschrieben. Stand 24.11.2026: 23 Einschränkungen und 36 Projekte.
- Ein Eintrag hat Titel, Koordinate, Thema und Link. Er hat keine Art (Verbot, Einschränkung, Info), keinen Zeitraum und keinen Bezug zu einem unserer Plätze. Der Zeitraum steht nur im Fliesstext («jeweils vom 15. Oktober bis 15. Dezember»).
- Eine Sperrung nimmt einen Platz aus «Beste Wahl» und Rangliste (`forecast.md`). Ein Fehler hier ist für Besucher und für den SUSV sichtbar.
- Hinweise der Redaktion (`DiveSiteNotice`) sind Teil der Version eines Platzes (ADR-008). Importierte Daten gehören nicht in die Versionen.

## Entscheidung

Wir entscheiden uns für **einen täglichen Import mit einmaliger Bestätigung durch die Moderation**.

- Ein Job holt die Einträge aller fünf Themen und speichert sie als `SusvEntry`, getrennt von den Versionen der Plätze.
- Ein Eintrag erscheint erst, wenn die Moderation ihn bestätigt hat. Sie ordnet ihn einem oder mehreren Plätzen zu. Bei einer Einschränkung setzt sie dazu Art und Zeitraum.
- Nach der Bestätigung gehen Änderungen der Quelle ohne Freigabe online. Ein entfernter Eintrag verschwindet. Die Moderation bekommt eine Mail.
- Jeder Eintrag steht an der Stelle der Platzseite, die zu seinem Thema passt. Er trägt das Logo des SUSV und einen Link auf seine Seite bei susv.ch.

## Optionen

### Option A: Import mit Bestätigung

**Vorteile:**
- Der SUSV pflegt die Daten an einer Stelle. Wir bleiben aktuell.
- Platz, Art und Zeitraum entscheidet ein Mensch. Die Sperrung wirkt auf die Rangliste.
- Aufwand der Moderation: rund 60 Einträge zum Start, danach wenige pro Jahr.

**Nachteile:**
- Ein neuer Eintrag erscheint erst nach der Bestätigung.
- Neue Entitäten, ein Job und eine Seite in der Moderation.

### Option B: Import ohne Bestätigung

**Beschreibung:** Der Job ordnet nach Koordinate zu. Jede Einschränkung erscheint als Warnung ohne Zeitraum.

**Vorteile:** kein Aufwand für die Moderation, neue Einträge sind am nächsten Tag online.

**Nachteile:**
- Die Koordinate genügt nicht immer: Plätze liegen nahe beieinander, und ein Eintrag kann eine Flussstrecke mit mehreren Plätzen betreffen.
- Ohne Art gibt es keine Sperrung. Ein Platz mit Tauchverbot kann als «Beste Wahl» erscheinen.
- Ein saisonales Verbot steht das ganze Jahr im Banner.

### Option C: Von Hand als Hinweis der Redaktion erfassen

**Vorteile:** kein neuer Code.

**Nachteile:** Änderungen beim SUSV kommen nur an, wenn jemand sie bemerkt. Die Texte laufen auseinander.

### Option D: Nur auf susv.ch verlinken

**Vorteile:** kein Aufwand.

**Nachteile:** Die Platzseite zeigt das Verbot nicht. Die Rangliste kennt es nicht.

## Konsequenzen

### Positive Auswirkungen

- Die Platzseite zeigt Verbote und Einschränkungen des Verbands, mit Quelle.
- Die Arbeit des SUSV ist an jedem betroffenen Platz sichtbar.
- Der Verlauf der Versionen bleibt frei von importierten Daten.

### Negative Auswirkungen / Risiken

- Die Schnittstelle kann sich ohne Ankündigung ändern. Dann bleibt der letzte Stand stehen, und das Echolot zeigt die Quelle als verspätet. Der SUSV soll vom täglichen Abruf wissen.
- Ändert der SUSV einen Text («wieder offen»), bleiben Art und Zeitraum, bis die Moderation sie anpasst. Die Mail weist darauf hin.
- Englische Texte fehlen in der Quelle meist. Sie entstehen durch die automatische Übersetzung (`cms.md`).

## Referenzen

- `specs/stories/002-susv-import-und-moderation.md`, `003-susv-einschraenkungen-im-banner.md`, `004-susv-projekte-nach-thema.md`
- `specs/datastructure.md`, `SusvEntry`
- ADR-008

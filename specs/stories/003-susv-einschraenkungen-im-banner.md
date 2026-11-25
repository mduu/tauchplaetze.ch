# Story 003: Einschränkungen des SUSV im Banner

**Status:** geplant (24.11.2026).

## Ziel

Wer einen Tauchplatz ansieht, sieht ein Tauchverbot oder eine Einschränkung des SUSV direkt unter dem Hero, mit Logo und Link zum SUSV. Ein gesperrter Platz erscheint nicht als «Beste Wahl».

Wunsch des SUSV (November 2026): Die Publikationen zu Einschränkungen sollen auf tauchplaetze.ch stehen.

## Stelle auf der Platzseite

| Thema | Code | Stelle | Text |
|---|---|---|---|
| Tauchverbot oder Einschränkung | `Restriction` | Banner unter dem Hero, zusammen mit den Hinweisen der Redaktion | Titel und Text des SUSV |

## Regeln im Banner

- Eine bestätigte, gültige Einschränkung steht im Banner bei den Hinweisen der Redaktion. Reihenfolge: schwerste Art zuerst, bei gleicher Art die Hinweise der Redaktion vor den Einträgen des SUSV.
- Label wie bei einem Hinweis («Gesperrt · bis 15.12.»), daneben das Logo des SUSV. Darunter der Titel und der Text, höchstens 500 Zeichen, gekürzt am Satzende. Danach «Mehr beim SUSV».
- **Eine gültige Sperrung des SUSV wirkt wie eine Sperrung der Redaktion:** Der Platz fehlt an diesem Tag in «Beste Wahl» und Rangliste (`forecast.md`).
- `HasDivingBan` und `DivingBanNote` bleiben Sache der Redaktion. Der Import setzt sie nicht.
- Sprache: Text des SUSV in der Sprache der Seite (`de`, `fr`, `it`). Englisch übersetzt der Core automatisch. Fehlt eine Sprache, erscheint der deutsche Text.
- **Mehrere Plätze:** Ein Eintrag kann zu mehreren Plätzen gehören, zum Beispiel ein Verbot auf einer Flussstrecke.

## Kennzeichnung

Gilt für jeden Eintrag des SUSV, auch in Story 004 (Komponente `SusvBadge`, `design-system.md`).

- **Logo und Link:** Jeder Eintrag trägt das Logo des SUSV und einen Link auf seine eigene Seite bei susv.ch (`DetailUrl`), in der Sprache der Seite, sonst auf Deutsch. Der Link öffnet in einem neuen Tab. Bei einer Einschränkung heisst er «Mehr beim SUSV».
- Im Banner steht das Signet ohne Namen, denn der Platz ist knapp (erlaubt vom SUSV, Story 002).
- **Kein Eintrag heisst nicht «erlaubt»:** Die Liste des SUSV ist nie vollständig (Story 002). Die Seite schreibt darum nirgends, dass an einem Platz keine Einschränkung gilt.

Aufbau der Platzseite: `ux.md`, «Tauchplatzseite», Abschnitt 2. Rangliste: `forecast.md`, «Rangliste».

## Akzeptanzkriterien

1. Eine bestätigte, heute gültige Einschränkung steht im Banner, nach Schwere eingereiht mit den Hinweisen der Redaktion.
2. Ausserhalb ihres Zeitraums ist sie unsichtbar. Ein Zeitraum «jedes Jahr» gilt auch über den Jahreswechsel.
3. Eine gültige Sperrung nimmt den Platz an diesem Tag aus «Beste Wahl» und Rangliste. Verzeichnis und Platzseite zeigen ihn weiter.
4. Der Text erscheint in der Sprache der Seite, sonst auf Deutsch. Er ist höchstens 500 Zeichen lang und endet an einem Satzende.
5. Der Link «Mehr beim SUSV» führt auf die Seite des Eintrags bei susv.ch.
6. Ein Eintrag, den die Quelle entfernt hat, steht nicht mehr im Banner.
7. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Abhängigkeiten und offene Punkte

Keine offenen Punkte. Das Logo ist geklärt (Story 002).

Braucht Story 002.

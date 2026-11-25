# Story 004: Projekte des SUSV auf der Platzseite

**Status:** geplant (24.11.2026).

## Ziel

Wer einen Tauchplatz ansieht, sieht, was der SUSV dort für die Tauchenden getan hat: Defibrillator, mobile Toilette, verbesserter Einstieg, Inklusions-Tauchplatz. Jeder Eintrag steht dort, wo er auf der Seite Sinn ergibt (Entscheid Marc, 24.11.2026).

Wunsch des SUSV (November 2026): Die Seite soll vermerken, wo der SUSV einen Platz verbessert hat.

## Themen und ihre Stelle

| Thema | Code | Stelle | Text |
|---|---|---|---|
| Defibrillator | `Defibrillator` | Kachel «Notfall» | «Defibrillator (AED) am Platz» |
| Mobile Toilette | `MobileToilet` | «Restaurants und Toiletten» | «Mobile Toilette am Platz» |
| Förderprojekt | `SupportProject` | «Lage und Einstiege», unter der Liste der Einstiege | «Der SUSV hat diesen Platz verbessert.» |
| Inklusions-Tauchplatz | `InclusiveSite` | Tag im Hero, Filter im Verzeichnis, Zeile bei «Lage und Einstiege» | «Inklusions-Tauchplatz: geeignet für Tauchende mit Behinderung.» |

- **Feste Texte bei Projekten:** Die Texte der Themen ausser `Restriction` kommen aus den Ressourcen, nicht aus dem Titel der Quelle. Die Titel der Quelle sind für die Karte des SUSV geschrieben, nicht für eine Zeile auf der Platzseite.
- **Abschnitt ohne anderen Inhalt:** Ein Eintrag zeigt seinen Abschnitt auch, wenn die Redaktion dort nichts erfasst hat.
- **Karte:** kein Pin. Die Koordinate der Quelle ist die des Platzes, nicht die des Geräts. Den genauen Ort einer Toilette erfasst die Redaktion als Ort (`Toilet`).
- **Verzeichnis nur:** Hat ein Platz nur Einträge des SUSV, bleibt der Hinweis «Dieser Platz ist erst im Verzeichnis».
- **Kennzeichnung:** Logo und Link wie in Story 003. Der Link führt auf die Seite des Eintrags bei susv.ch.
- **Filter «Inklusion»** im Verzeichnis: Plätze mit bestätigtem Eintrag `InclusiveSite`.

Aufbau der Platzseite: `ux.md`, «Tauchplatzseite», Abschnitte 1, 4, 6 und 7.

## Akzeptanzkriterien

1. Ein bestätigter Defibrillator steht in der Kachel «Notfall».
2. Eine bestätigte mobile Toilette steht bei «Restaurants und Toiletten», auch ohne Text der Redaktion.
3. Förderprojekt und Inklusions-Tauchplatz stehen unter der Liste der Einstiege.
4. Ein Inklusions-Tauchplatz trägt das Tag «Inklusion» im Hero. Das Verzeichnis filtert danach.
5. Jeder Eintrag trägt Logo und Link zum SUSV.
6. Die Ausbaustufe eines Platzes ändert sich durch Einträge des SUSV nicht.
7. Alle Texte stehen in vier Sprachen in den Ressourcen.
8. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Abhängigkeiten und offene Punkte

| Punkt | Nächster Schritt |
|---|---|
| **Abgeschlossene Projekte ohne Daten** (Story 002): 14 Plätze stehen beim SUSV nur als Text. | Der SUSV klärt intern, ob und wie er sie nachträgt (Story 002). Bis dahin erfasst die Redaktion sie von Hand. |
| **Flaggenhalter aus der Quelle:** Der SUSV hat an vielen Plätzen Halter für die Taucherflagge montiert (Hinweis vom 25.11.2026). Die Quelle führt sie nicht als Thema. Die Redaktion erfasst sie als Ort (`FlagHolder`, `dive-site.md`). | Den SUSV nach einer Liste mit Koordinaten fragen. Dann setzt ein Import die Orte. |
| **Absprachen mit Behörden:** Die Quelle führt sie nur, soweit sie unter den Einschränkungen stehen (Story 003). | Den SUSV fragen, ob es dafür ein eigenes Thema geben soll. |

Braucht Story 002 und die Kennzeichnung aus Story 003.

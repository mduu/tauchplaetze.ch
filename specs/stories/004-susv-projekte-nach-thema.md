# Story 004: Projekte des SUSV auf der Platzseite

**Status:** ✅ erledigt, deployt am 27.11.2026.

## Ziel

Wer einen Tauchplatz ansieht, sieht, was der SUSV dort für die Tauchenden getan hat: Defibrillator, mobile Toilette, verbesserter Einstieg, Inklusions-Tauchplatz. Jeder Eintrag steht dort, wo er auf der Seite Sinn ergibt (Entscheid Marc, 24.11.2026).

Wunsch des SUSV (November 2026): Die Seite soll vermerken, wo der SUSV einen Platz verbessert hat.

## Themen und ihre Stelle

| Thema | Code | Stelle | Eigenschaft | Tooltip |
|---|---|---|---|---|
| Defibrillator | `Defibrillator` | «Der Platz» und Kachel «Notfall» | «Defibrillator (AED)» | «Defibrillator (AED) am Platz» |
| Mobile Toilette | `MobileToilet` | «Der Platz» und «Restaurants und Toiletten» (Satz mit `SusvBadge`, auch ohne Text der Redaktion) | «Mobile Toilette» | «Mobile Toilette am Platz» |
| Förderprojekt | `SupportProject` | «Der Platz» | «Platz verbessert» | «Der SUSV hat diesen Platz verbessert.» |
| Inklusions-Tauchplatz | `InclusiveSite` | «Der Platz», Tag im Hero, Filter im Verzeichnis | «Inklusions-Tauchplatz» | «Inklusions-Tauchplatz: geeignet für Tauchende mit Behinderung.» |

- **Eigenschaften statt Sätze** (Entscheid Marc, 28.11.2026): Die Projekte stehen als Fusszeile der Kachel «Der Platz», je als Chip mit Symbol und kurzem Namen (`SusvFeatures`, `design-system.md`). Vorne steht das Signet des SUSV mit «Angaben des SUSV». Jeder Chip verlinkt die Seite des Eintrags bei susv.ch. «Der Platz» fasst die Eigenschaften des Platzes zusammen. Texte und Karte sind das ausführliche Briefing. Darum dürfen Angaben doppelt stehen: Der Defibrillator steht zusätzlich in der Kachel «Notfall», die mobile Toilette als Satz bei «Restaurants und Toiletten». Vorher standen die Projekte als ganze Sätze an verschiedenen Stellen (Entscheid Marc, 24.11.2026 und 27.11.2026).

- **Feste Texte bei Projekten:** Die Texte der Themen ausser `Restriction` kommen aus den Ressourcen, nicht aus dem Titel der Quelle. Die Titel der Quelle sind für die Karte des SUSV geschrieben, nicht für eine Eigenschaft auf der Platzseite.
- **Abschnitt ohne anderen Inhalt:** Ein Eintrag zeigt seinen Abschnitt auch, wenn die Redaktion dort nichts erfasst hat.
- **Karte:** kein Pin. Die Koordinate der Quelle ist die des Platzes, nicht die des Geräts. Den genauen Ort einer Toilette erfasst die Redaktion als Ort (`Toilet`).
- **Verzeichnis nur:** Hat ein Platz nur Einträge des SUSV, bleibt der Hinweis «Dieser Platz ist erst im Verzeichnis».
- **Kennzeichnung:** Logo und Link wie in Story 003. Der Link führt auf die Seite des Eintrags bei susv.ch.
- **Filter «Inklusion»** im Verzeichnis: Plätze mit bestätigtem Eintrag `InclusiveSite`.

Aufbau der Platzseite: `ux.md`, «Tauchplatzseite», Abschnitte 1, 4, 6 und 7.

## Akzeptanzkriterien

1. Alle bestätigten Projekte stehen als Eigenschaften in der Fusszeile der Kachel «Der Platz», auch ohne Bewertung.
2. Ein bestätigter Defibrillator steht zusätzlich in der Kachel «Notfall», eine bestätigte mobile Toilette zusätzlich als Satz bei «Restaurants und Toiletten», auch ohne Text der Redaktion.
3. Die Eigenschaften tragen das Signet des SUSV und «Angaben des SUSV».
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

## Umsetzung

- Pro Thema zeigt die Seite höchstens eine Zeile, auch wenn mehrere Einträge zum Platz gehören. Der Link führt auf den ältesten Eintrag (kleinste Nummer der Quelle).
- Das Tag «Inklusion» im Hero hat den Stil des Tags «Bergsee».
- Filter im Verzeichnis: URL-Parameter `f=inclusive`.

# Story 005: Tauchen und Schifffahrt

**Status:** geplant (24.11.2026).

## Ziel

Wer einen Tauchplatz ansieht, sieht auf der Karte, wo die Schifffahrt das Tauchen verbietet, und versteht, dass Tauchen an vielen Plätzen nur geduldet ist.

Wunsch des SUSV (November 2026): Das Thema aufnehmen und die Lage um Hafen oder Bojenfeld sichtbar machen, zum Beispiel mit einem roten Kreis im Massstab.

## Regeln

Die Binnenschifffahrtsverordnung (BSV, SR 747.201.1) verbietet das Sporttauchen an bestimmten Orten (Art. 77 Abs. 3). An vielen Plätzen ist das Tauchen dort nur geduldet. Der SUSV wünscht, dass die Seite das zeigt. Entscheid Marc, 24.11.2026: Die Karte zeichnet automatisch ein, was mit Daten belegt ist. Den Rest erklärt ein Text.

| Verbot nach Art. 77 Abs. 3 BSV | Auf der Karte | Daten |
|---|---|---|
| im Umkreis von 100 m um bewilligte Landungsstellen der Kursschiffe | Kreis mit 100 m Radius, im Massstab | `ShippingFeature`, Art `LandingStage` |
| auf den Fahrlinien der Kursschiffe | Linie | Art `FerryRoute` |
| bei Hafeneinfahrten | Hafen als schraffierte Fläche, dazu ein Kreis mit 50 m Radius um die Mitte | Art `Harbour` |
| in der Nähe zugelassener Liegeplätze | Bojenfeld als Fläche, nur wo die Redaktion es in die Skizze zeichnet, dazu ein Kreis mit 50 m Radius um die Mitte | Tauchskizze, `MooringField` |
| in engem Fahrwasser | nicht auf der Karte | – |

- **Abstände praktisch halten** (Entscheid Marc, 25.11.2026): Jeder Abstand gilt ab der Mitte des Objekts, nicht ab seinem Rand. Sonst entstehen am Ufer Flächen, in denen niemand mehr tauchen könnte, und das entspricht nicht der Praxis. Nennt die Verordnung 100 m (Landungsstellen), ist der Radius 100 m. Sonst gilt 50 m. Mit diesem Wert arbeitet auch der SUSV (Antwort vom 25.11.2026).
- **Ebene «Schifffahrt»** auf der Karte «Lage und Einstiege»: an, abschaltbar wie «Tiefenlinien». Sie zeigt die Objekte im Umkreis von 500 m um den Platz und seine Einstiege. Ohne Objekt fehlt die Ebene.
- **Zeile unter der Karte**, nur mit Ebene: «Nahe am Platz verkehren Kursschiffe. Sporttauchen ist an den markierten Stellen verboten (Art. 77 BSV).» mit Link «Tauchen und Schifffahrt». Liegt der Platz oder ein Einstieg im Kreis einer Landungsstelle, nennt die Zeile sie beim Namen und steht in der Farbe einer Warnung: «Dieser Platz liegt weniger als 100 m von der Schiffstation {Name}.»
- **Kein Banner und keine Wirkung auf die Rangliste.** Die Regel gilt dauernd und ist keine Sperrung des Platzes. Eine konkrete Einschränkung kommt als Hinweis der Redaktion oder des SUSV.
- **Unter der Ebene steht:** «Orientierung, nicht rechtsverbindlich.» Die Daten sind nicht die amtliche Bewilligung.
- **Seite «Tauchen und Schifffahrt»** (`ux.md`): der ganze Text mit allen fünf Verboten.
- Vor dem Bau die aktuelle Fassung der BSV prüfen. Gelesen ist der Stand 1.1.2022.

Daten und Import: `data-sources.md`, «Schifffahrt». Entität: `ShippingFeature` (`datastructure.md`).

## Akzeptanzkriterien

1. Die Karte der Platzseite zeigt die Ebene «Schifffahrt», wenn ein Objekt im Umkreis von 500 m liegt: Kreis von 100 m um Landungsstellen, Fahrlinien, Häfen als Fläche.
2. Um die Mitte eines Hafens und eines Bojenfelds liegt ein Kreis von 50 m.
3. Die Redaktion kann ein Bojenfeld als Fläche in die Tauchskizze zeichnen.
4. Unter der Karte steht die Zeile zur Schifffahrt. Liegt der Platz oder ein Einstieg im Kreis einer Landungsstelle, nennt sie die Schiffstation.
5. Der Text «Tauchen und Schifffahrt» ist in vier Sprachen online und nennt alle fünf Verbote.
6. Die Regel ändert weder Banner noch Rangliste.
7. Das Echolot hat einen Eintrag in den Neuigkeiten.

## Abhängigkeiten und offene Punkte

| Punkt | Nächster Schritt |
|---|---|
| **Ort des Textes:** eigene Seite (Vorschlag in `ux.md`) oder Abschnitt auf «Über». | Entscheid Marc. |
| **Wortlaut** des Textes. Der SUSV begrüsst den Verweis auf die BSV und auf die Eigenverantwortung. Er ergänzt: Viele lokale Sonderregeln sind nirgends zusammengefasst. Wer zum ersten Mal an einem Platz taucht, fragt vorher bei einem Club oder Tauchgeschäft am Ort. | Diesen Punkt in den Text aufnehmen. Marc stimmt den Wortlaut mit dem SUSV ab. |
| **BSV und BAV-Daten:** Gelesen ist die BSV im Stand 1.1.2022. Die Nutzungsbedingungen der BAV-Haltestellen sind nicht geprüft. | Vor dem Bau prüfen. |

Hängt von keiner anderen Story ab. Der Text verlinkt auf «Partner» (Story 001).

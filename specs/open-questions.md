# Offene Punkte

Stand: 17.10.2026. Erledigte Punkte wandern als Entscheid in die passende Spec oder in ein ADR.

## Vor dem Bau (blockierend)

| # | Frage | Nächster Schritt |
|---|---|---|
| 1 | **Dürfen wir die Daten der Alplakes API nutzen?** Unter welchen Bedingungen (Quellenangabe, Frequenz)? | Mail an Eawag (Alplakes) am 17.10.2026 gesendet (`doc/mails/2026-10-17-eawag-alplakes.md`). Antwort ausstehend. |
| 21 | Dürfen wir die Abfluss- und Seepegelvorhersagen des BAFU nutzen? Es gibt nur ein JSON der Grafiken, keine API. | Mail an `BAFU (Hydrologie)` am 17.10.2026 gesendet (`doc/mails/2026-10-17-bafu-hydrologie.md`). Antwort ausstehend. Blockiert nur die Flussprognose in Phase 1. |

## Erledigt am 17.10.2026

- #6 (Umkreis ohne gewählten Ort): ganze Schweiz, `features.md`.
- #4 (Marke): «tauchplaetze.ch».
- #9 (eine bestehende Plattform): vorerst keine Zusammenarbeit. Dort gibt es nur vereinzelte Daten.
- #10 (Swiss Dive): vorerst nicht anschreiben.
- #12 (Bilder): öffentlicher Blob-Container mit Cloudflare davor, `infrastructure.md`.
- #13 (maschinelle Übersetzung): Claude API mit Glossar, Phase 1, `cms.md`.
- #16 (Azure SQL Free Offer): für das Projekt nicht entscheidend. Bei der Einrichtung prüfen.
- #17 (erste Gewässer): Zürichsee, Zugersee und der Rhein, `roadmap.md`.
- #18 bis #20 (Fotos, Einstiege, Rechtliches): als Aufgaben in `roadmap.md`.
- #5 (Tagesauswahl): 5 Tage, `forecast.md`.
- #2 und #3 (Abdeckung und Reichweite von Alplakes): Spike 0.5, `data-sources.md`.
- #7 und #8 (Flüsse, Bergseen ohne Modell): Datenstufen in `forecast.md`.
- #11 (Kartenkacheln): swisstopo, `data-sources.md`.
- #14 (MeteoSchweiz über Alplakes): Alplakes hat keinen Proxy mehr. ADR-007.
- #22 (Name der Entität): `WaterBody`, URL `/gewaesser/{slug}`, `datastructure.md`, `i18n.md`.
- #15 (Sprache der Kommentare): Deutsch, `architecture.md`.

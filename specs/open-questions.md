# Offene Punkte

Stand: 17.10.2026. Erledigte Punkte wandern als Entscheid in die passende Spec oder in ein ADR.

## Vor dem Bau (blockierend)

| # | Frage | Nächster Schritt |
|---|---|---|
| 1 | **Dürfen wir die Daten der Alplakes API nutzen?** Unter welchen Bedingungen (Quellenangabe, Frequenz)? | Mail an Eawag (Kontakt in der API-Beschreibung: Eawag (Alplakes)). Vorhaben kurz erklären, Abfragemenge nennen (ca. ein Call pro Platz und Tag), nicht kommerziell, Quellenangabe zusichern. |
| 21 | Dürfen wir die Abfluss- und Seepegelvorhersagen des BAFU nutzen? Es gibt nur ein JSON der Grafiken, keine API. | Mail an `BAFU (Hydrologie)`. Blockiert nur die Flussprognose in Phase 1. |

## Erledigt am 17.10.2026

- #5 (Tagesauswahl): 5 Tage, `forecast.md`.
- #2 und #3 (Abdeckung und Reichweite von Alplakes): Spike 0.5, `data-sources.md`.
- #7 und #8 (Flüsse, Bergseen ohne Modell): Datenstufen in `forecast.md`.
- #11 (Kartenkacheln): swisstopo, `data-sources.md`.
- #14 (MeteoSchweiz über Alplakes): Alplakes hat keinen Proxy mehr. ADR-007.
- #22 (Name der Entität): `WaterBody`, URL `/gewaesser/{slug}`, `datastructure.md`, `i18n.md`.
- #15 (Sprache der Kommentare): Deutsch, `architecture.md`.

## Fachlich

| # | Frage | Vorschlag |
|---|---|---|
| 4 | Projektname und Wortmarke: «tauchplaetze.ch» oder ein eigener Name? | Domain als Name verwenden. Im Entwurf stand der Arbeitsname «seegrund». |
| 6 | Standard-Standort für den Umkreis, wenn die Person keinen wählt? | Kein Umkreis: Rangliste aller Plätze der Schweiz. Mit Auswahl: Umkreis. |
| 9 | Kooperation mit eine bestehende Plattform? | Betreiber kontaktieren (über Impressum). Optional: deren Meldungen mit Erlaubnis verlinken oder einbinden. |
| 10 | Kooperation mit  (Tauchführer «Swiss Dive»)? | Später prüfen. Seine Führer sind die Referenz für Schweizer Plätze. |

## Technisch

| # | Frage | Vorschlag |
|---|---|---|
| 12 | Bilder direkt aus Blob (öffentlicher Container hinter Cloudflare) oder über einen Endpunkt der App? | Öffentlicher Container mit Cloudflare davor. Einfacher und günstiger. |
| 13 | Maschinelle Übersetzung redaktioneller Inhalte: welcher Dienst? | Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext. Kosten pro Platz sehr gering. Erst in Phase 1. |
| 16 | Status des Azure SQL Free Offer (GA oder Preview)? | In der FAQ von Microsoft Learn prüfen. Für das Projekt nicht entscheidend. |

## Inhaltlich

| # | Frage | Vorschlag |
|---|---|---|
| 17 | Welcher See für das MVP? | Ein See, den Marc gut kennt und für den er viele Fotos hat. |
| 18 | Fotos aus tauchfotos.ch: welche Auswahl, welche Auflösung liegt vor? | Originale sichten, pro Platz 3–6 starke Bilder. |
| 19 | Aktuelle Fotos von Ein- und Ausstiegen fehlen. | Nach und nach bei eigenen Tauchgängen aufnehmen. Später Beiträge von Redaktoren. |
| 20 | Rechtliches: Impressum, Datenschutz (nDSG), Haftungshinweis zur Prognose. | Texte vor dem Go-Live erstellen. |

# Offene Punkte

Stand: 17.10.2026. Erledigte Punkte wandern als Entscheid in die passende Spec oder in ein ADR.

## Vor dem Bau (blockierend)

| # | Frage | Nächster Schritt |
|---|---|---|
| 1 | **Dürfen wir die Daten der Alplakes API nutzen?** Unter welchen Bedingungen (Quellenangabe, Frequenz, kommerzieller Charakter wegen Partnerplätzen)? | Mail an Eawag (Kontakt in der API-Beschreibung: Eawag (Alplakes)). Vorhaben kurz erklären, Abfragemenge nennen (ca. ein Aufruf pro Platz und Tag), Quellenangabe zusichern. |
| 2 | Welche Seen haben 3D-Modelle, welche nur 1D? Welche Remote-Sensing-Produkte (Secchi, Chlorophyll) gibt es pro See und wie aktuell? | Spike Phase 0.5 |
| 3 | Wie lange reicht die Prognose von Alplakes (Tage)? Wann wird sie aktualisiert? | Spike Phase 0.5, danach Zeitplan des Jobs festlegen |

## Fachlich

| # | Frage | Vorschlag |
|---|---|---|
| 4 | Projektname und Wortmarke: «tauchplaetze.ch» oder ein eigener Name? | Domain als Name verwenden. Im Entwurf stand der Arbeitsname «seegrund». |
| 5 | Wie viele Tage zeigt die Tagesauswahl? | So viele, wie die Daten verlässlich hergeben (vermutlich 3–5). |
| 6 | Standard-Standort für den Umkreis, wenn die Person keinen wählt? | Kein Umkreis: Rangliste aller Plätze der Schweiz. Mit Auswahl: Umkreis. |
| 7 | Flüsse (Verzasca, Rhein) in der Prognose? Alplakes deckt Seen ab. | Flussplätze ins Verzeichnis aufnehmen, Prognose dort nur Wetter und Abfluss. |
| 8 | Bergseen ohne Modell? | Verzeichnis ja, Prognose «keine Daten». |
| 9 | Kooperation mit eine bestehende Plattform? | Betreiber kontaktieren (über Impressum). Optional: deren Meldungen mit Erlaubnis verlinken oder einbinden. |
| 10 | Kooperation mit  (Tauchführer «Swiss Dive»)? | Später prüfen. Seine Führer sind die Referenz für Schweizer Plätze. |

## Technisch

| # | Frage | Vorschlag |
|---|---|---|
| 11 | Kartenkacheln: swisstopo oder OpenStreetMap? | Nutzungsbedingungen von swisstopo prüfen (schönere Karte). Sonst OSM über einen Anbieter mit freiem Kontingent. |
| 12 | Bilder direkt aus Blob (öffentlicher Container hinter Cloudflare) oder über einen Endpunkt der App? | Öffentlicher Container mit Cloudflare davor. Einfacher und günstiger. |
| 13 | Maschinelle Übersetzung redaktioneller Inhalte: welcher Dienst? | Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext. Kosten pro Platz sehr gering. Erst in Phase 1. |
| 14 | MeteoSchweiz direkt (Open Data) oder über die Alplakes API? | Zuerst Alplakes (ein Client). Direkt nur, falls Alplakes etwas fehlt. |
| 15 | Sprache der Code-Kommentare: Englisch (wie feldnah) oder Deutsch (wie bau-ticker)? | Im Dossier gesetzt: Englisch in ASD-STE100. Bei Bedarf in `CLAUDE.md` umstellen. |
| 16 | Status des Azure SQL Free Offer (GA oder Preview)? | In der FAQ von Microsoft Learn prüfen. Für das Projekt nicht entscheidend. |

## Inhaltlich

| # | Frage | Vorschlag |
|---|---|---|
| 17 | Welcher See für das MVP? | Ein See, den Marc gut kennt und für den er viele Fotos hat. |
| 18 | Fotos aus tauchfotos.ch: welche Auswahl, welche Auflösung liegt vor? | Originale sichten, pro Platz 3–6 starke Bilder. |
| 19 | Aktuelle Fotos von Ein- und Ausstiegen fehlen. | Nach und nach bei eigenen Tauchgängen aufnehmen. Später Beiträge von Redaktoren. |
| 20 | Rechtliches: Impressum, Datenschutz (nDSG), Haftungshinweis zur Prognose. | Texte vor dem Go-Live erstellen. |

# Offene Punkte

Stand: 17.10.2026. Erledigte Punkte wandern als Entscheid in die passende Spec oder in ein ADR.

## Vor dem Bau (blockierend)

| # | Frage | Nächster Schritt |
|---|---|---|
| 27 | **Koordinaten in der Telemetrie:** «In meiner Nähe» und der Umkreis der Startseite tragen `lat`/`lng` in der Adresse. Application Insights speichert die Adressen 30 Tage. | Vorschlag: Query-Parameter `lat`, `lng` vor dem Senden entfernen oder runden (Telemetry Processor). Dann den Satz im Datenschutz anpassen. |
| 28 | **Datenschutz vor dem Go-Live prüfen lassen** (nDSG, Bekanntgabe ins Ausland: Cloudflare, Microsoft; Mail über einen Schweizer Anbieter, ADR-012). Der Text auf «Über» ist ein sorgfältiger Entwurf, keine Rechtsberatung. | Marc entscheidet, ob eine Fachperson drüberschaut. |
| 30 | **Health-Check für die Mail-Queue:** `/health/ready` prüft heute nur die App. Soll er die Queue (und die Dead-Letter-Queue) einschliessen? | Mit dem Deployment klären. Die Aspire-Integration für Queues bringt einen Health-Check mit, er ist nur noch nicht an `/health/ready` gebunden. |

## Erledigt am 20.10.2026

- #26 (Mail-Dienst in Prod): Hostpoint SMTP, ADR-012. Der Datenschutz nennt einen Schweizer E-Mail-Anbieter ohne Namen.
- #29 (Data-Protection-Schlüssel in Prod): ohne eigene Konfiguration, App Service legt sie im Dateispeicher der App ab, `infrastructure.md` (IaC).

## Erledigt am 17.10.2026

- #6 (Umkreis ohne gewählten Ort): ganze Schweiz, `features.md`.
- #4 (Marke): «tauchplaetze.ch».
- #9 (eine bestehende Plattform): vorerst keine Zusammenarbeit. Dort gibt es nur vereinzelte Daten.
- #10 (Swiss Dive): vorerst nicht anschreiben.
- #12 (Bilder): öffentlicher Blob-Container mit Cloudflare davor, `infrastructure.md`.
- #13 (maschinelle Übersetzung): Claude API mit Glossar, Phase 1, `cms.md`.
- #16 (Azure SQL Free Offer): für das Projekt nicht entscheidend. Bei der Einrichtung prüfen.
- #17 (erste Gewässer): zuerst Zürichsee, Zugersee und der Rhein. Seit 19.10.2026 alle Seen mit Plätzen, die Alplakes abdeckt (Entscheid Marc), `data-sources.md`.
- #18 bis #20 (Fotos, Einstiege, Rechtliches): als Aufgaben in `roadmap.md`.
- #5 (Tagesauswahl): 5 Tage, `forecast.md`.
- #2 und #3 (Abdeckung und Reichweite von Alplakes): Spike 0.5, `data-sources.md`.
- #7 und #8 (Flüsse, Bergseen ohne Modell): Datenstufen in `forecast.md`.
- #11 (Kartenkacheln): swisstopo, `data-sources.md`.
- #14 (MeteoSchweiz über Alplakes): Alplakes hat keinen Proxy mehr. ADR-007.
- #22 (Name der Entität): `WaterBody`, URL `/gewaesser/{slug}`, `datastructure.md`, `i18n.md`.
- #15 (Sprache der Kommentare): Deutsch, `architecture.md`.

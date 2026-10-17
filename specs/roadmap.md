# Roadmap

Grundsatz: früh etwas Echtes zeigen. Zuerst **zwei Seen und ein Fluss richtig schön**, dann skalieren.

## Phase 0 – Fundament

Ziel: lauffähiges Gerüst, alle Konventionen verankert.

- Solution `src/Tauchplaetze.slnx` mit `Core`, `Web`, `ServiceDefaults`, `AppHost`, `Core.Tests`, `Web.Tests`.
- Handler-Contracts, `HandlerRegistration`, `ICoreModule` mit Assembly-Scan.
- `TauchplaetzeDbContext` mit NetTopologySuite, erste Migration (`WaterBodies`, `DiveSites`, Übersetzungen, Revisionen, Medien, Identity).
- ArchitectureTests (Fassadengrenze, `internal` Handler, Queries ohne `SaveChanges`).
- Lokalisierung: Request Localization mit Sprachpräfix, vier `.resx`-Sätze, Test auf gleiche Schlüssel.
- Layouts `PublicLayout` und `AdminLayout` mit Design Tokens, selbst gehostete Schriften.
- Aspire mit SQL Server und Azurite.
- CI-Workflow (Build, Tests).
- `AGENTS.md` (mit `CLAUDE.md` als Verweis) und `specs/` im Repository.

## Phase 0.5 – Spike Datenquellen ✅

Erledigt am 17.10.2026. Ergebnis: `doc/spikes/0.5-datenquellen.md`.

Ziel: Gewissheit, dass die Prognose mit echten Daten trägt.

- `AlplakesClient` für `simulations/metadata`, `simulations/depthtime` und Remote Sensing.
- Smoke Test: Temperaturprofil für «Riff Williams» (Zürichsee) für die nächsten Tage holen. Sprungschicht berechnen.
- Prüfen: Welche Seen haben 3D-Modelle, welche nur 1D? Welche Remote-Sensing-Daten gibt es pro See?
- **Parallel:** Eawag anschreiben (Nutzung, Quellenangabe, Abfragefrequenz).
- **Go/No-Go:** Ohne geklärten Datenzugang keine öffentliche Prognose.

## Phase MVP – zwei Seen und ein Fluss, richtig schön

Ziel: Reaktion der Szene testen.

- Gewässer-Stammdaten schweizweit, Seen und Flüsse aus swissTLM3D (Seed). Zuordnung zu Alplakes, BAFU-Stationen und SwissMetNet für die grossen Seen.
- Redaktion: Platz erfassen (Schnellerfassung mit Karte), bearbeiten, Revisionen, Medien-Upload mit Bildgrössen.
- Öffentliche Seiten: Start «Wo tauchen?», Tauchplatzseite, Verzeichnis mit Karte, Gewässerseite, Über/Impressum/Datenschutz.
- Prognose-Job mit `rules-1.0` und Datenstufen, Rohdaten in Blob Storage, Status im Admin. Quellen: Alplakes, Open-Meteo, SwissMetNet, BAFU (ADR-007).
- SEO-Grundlagen: Titel, Meta, `hreflang`, Sitemap, JSON-LD.
- Deploy auf Azure (Bicep, GitHub Actions, Cloudflare, Redirect `sichtweiten.ch`).
- **Inhalt:** Zürichsee und Zugersee vollständig (je 8 bis 12 Plätze auf Stufe 2 mit Fotos aus tauchfotos.ch). Dazu der Rhein als erster Fluss. Alle weiteren bekannten Plätze als Stufe 0. Entscheid Marc, 17.10.2026: Marc taucht dort am meisten und kann die Prognose am besten beurteilen. Beide Seen haben ein 3D-Modell (Datenstufe 3).
- **Inhalt pro Platz:** 3–6 starke Fotos aus den Originalen von tauchfotos.ch. Fotos der Ein- und Ausstiege nach und nach bei eigenen Tauchgängen.
- **Rechtliches vor dem Go-Live:** Impressum, Datenschutz (nDSG), Haftungshinweis zur Prognose.
- **Validierung:** Link an alte Kontakte, Clubs, Shops, Facebook- und WhatsApp-Gruppen. Erfolgskriterien in `vision.md`.

## Phase 1 – alle grossen Seen

- Alle grossen Seen mit Plätzen auf Stufe 1 bis 2. Flussplätze mit BAFU-Stationen.
- In-situ-Messungen aus Eawag Datalakes. BAFU-Abflussprognosen, falls die Nutzung geklärt ist.
- Meldungen (ein Klick nach dem Tauchgang).
- Tauchshops und Füllstationen (kostenlos, ohne Hervorhebung).
- Buddy-Gruppen (WhatsApp) pro Region.
- Weitere Redaktoren mit Einladung.
- Optional: maschineller Übersetzungsvorschlag für redaktionelle Inhalte.
- Videos aus tauchvideos.ch.
- Neue Fotos der Ein- und Ausstiege, nach und nach.

## Phase 2 – Modell und Komfort

- Eigenes Sichtmodell (ML.NET, LightGBM) mit Meldungen, Secchi-Messungen und historischen Labels aus Marcs Logs und Fotos. Schattenbetrieb, dann Wechsel bei klarer Verbesserung.
- Benachrichtigungen («gute Bedingungen am Walensee»).
- PWA mit Favoriten offline.
- Native Apps nur, wenn die Web-App trägt.

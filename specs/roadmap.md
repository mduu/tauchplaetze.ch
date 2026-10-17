# Roadmap

Grundsatz: früh etwas Echtes zeigen. Zuerst **ein See richtig schön**, dann skalieren.

## Phase 0 – Fundament

Ziel: lauffähiges Gerüst, alle Konventionen verankert.

- Solution `src/Tauchplaetze.slnx` mit `Core`, `Web`, `ServiceDefaults`, `AppHost`, `Core.Tests`, `Web.Tests`.
- Handler-Contracts, `HandlerRegistration`, `ICoreModule` mit Assembly-Scan.
- `TauchplaetzeDbContext` mit NetTopologySuite, erste Migration (`Lakes`, `DiveSites`, Übersetzungen, Revisionen, Medien, Identity).
- ArchitectureTests (Fassadengrenze, `internal` Handler, Queries ohne `SaveChanges`).
- Lokalisierung: Request Localization mit Sprachpräfix, vier `.resx`-Sätze, Test auf gleiche Schlüssel.
- Layouts `PublicLayout` und `AdminLayout` mit Design Tokens, selbst gehostete Schriften.
- Aspire mit SQL Server und Azurite.
- CI-Workflow (Build, Tests).
- `CLAUDE.md` und `specs/` im Repository.

## Phase 0.5 – Spike Datenquellen

Ziel: Gewissheit, dass die Prognose mit echten Daten trägt.

- `AlplakesClient` für `simulations/metadata`, `simulations/depthtime`, `insitu/secchi`.
- Smoke Test: Temperaturprofil für «Riff Williams» (Zürichsee) für die nächsten Tage holen. Sprungschicht berechnen.
- Prüfen: Welche Seen haben 3D-Modelle, welche nur 1D? Welche Remote-Sensing-Daten gibt es pro See?
- **Parallel:** Eawag anschreiben (Nutzung, Quellenangabe, Abfragefrequenz).
- **Go/No-Go:** Ohne geklärten Datenzugang keine öffentliche Prognose.

## Phase MVP – ein See, richtig schön

Ziel: Reaktion der Szene testen.

- Seen-Stammdaten der grossen Seen mit Alplakes-Zuordnung (Seed).
- Redaktion: Platz erfassen (Schnellerfassung mit Karte), bearbeiten, Revisionen, Medien-Upload mit Bildgrössen.
- Öffentliche Seiten: Start «Wo tauchen?», Tauchplatzseite, Verzeichnis mit Karte, Seeseite, Über/Impressum/Datenschutz.
- Prognose-Job mit `rules-1.0`, Rohdaten in Blob Storage, Status im Admin.
- SEO-Grundlagen: Titel, Meta, `hreflang`, Sitemap, JSON-LD.
- Deploy auf Azure (Bicep, GitHub Actions, Cloudflare, Redirect `sichtweiten.ch`).
- **Inhalt:** ein See vollständig (8 bis 12 Plätze auf Stufe 2 mit Fotos aus tauchfotos.ch). Alle weiteren bekannten Plätze als Stufe 0.
- **Validierung:** Link an alte Kontakte, Clubs, Shops, Facebook- und WhatsApp-Gruppen. Erfolgskriterien in `vision.md`.

## Phase 1 – alle grossen Seen

- Alle grossen Seen mit Plätzen auf Stufe 1 bis 2.
- Meldungen (ein Klick nach dem Tauchgang).
- Partnerplätze für Tauchshops.
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

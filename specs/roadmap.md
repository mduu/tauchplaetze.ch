# Roadmap

Grundsatz: früh etwas Echtes zeigen. Zuerst **zwei Seen und ein Fluss richtig schön**, dann skalieren.

## Phase 0 – Fundament ✅

Erledigt bis 18.10.2026.

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

Ziel: Reaktion der Szene testen. Stand 22.10.2026: ✅ umgesetzt, 🟡 teilweise, ⏳ offen.

- ✅ Gewässer-Stammdaten schweizweit (Seed). Zuordnung zu Alplakes und BAFU-Stationen für die grossen Seen. SwissMetNet ist nicht angebunden (⏳), der Regen der letzten Tage kommt aus Open-Meteo.
- ✅ Redaktion: Platz erfassen (Schnellerfassung mit Karte), bearbeiten, Revisionen, Medien-Upload mit Bildgrössen.
- ✅ Öffentliche Seiten: Start «Wo tauchen?», Tauchplatzseite, Verzeichnis mit Karte, Gewässerseite, Über/Impressum/Datenschutz.
- ✅ Prognose-Job mit Datenstufen (heute `rules-1.6`), Rohdaten in Blob Storage, Status auf `/platform/prognose`. Quellen: Alplakes, Open-Meteo, BAFU (ADR-007).
- ✅ SEO-Grundlagen: Titel und Beschreibungen mit Daten des Tages, Canonical, `hreflang`, Open Graph, JSON-LD, `robots.txt` und Sitemap pro Sprache (`/sitemap.xml`), 24.10.2026.
- ✅ Deploy auf Azure (Bicep, GitHub Actions mit what-if und Freigabe, Deploy aus dem Paket, Health Check wartet auf die neue Version). Domains `tauchplaetze.ch`, `www.tauchplaetze.ch`, `sichtweiten.ch` und `www.sichtweiten.ch` mit Zertifikat (21.10.2026). Mail über Hostpoint mit SPF, DKIM und DMARC. DNS bleibt bei Hostpoint, Cloudflare ist ein späterer Ausbau.
- ⏳ **Inhalt:** Zürichsee und Zugersee vollständig (je 8 bis 12 Plätze auf Stufe 2 mit Fotos aus dem eigenen Archiv). Dazu der Rhein als erster Fluss. Alle weiteren bekannten Plätze als Stufe 0. Entscheid Marc, 17.10.2026: Marc taucht dort am meisten und kann die Prognose am besten beurteilen. Beide Seen haben ein 3D-Modell (Datenstufe 3).
- ⏳ **Inhalt pro Platz:** 3–6 starke Fotos aus den Originalen des eigenen Archivs. Fotos der Ein- und Ausstiege nach und nach bei eigenen Tauchgängen.
- 🟡 **Rechtliches:** Impressum, Datenschutz (nDSG) und Haftungshinweis sind online. Die Prüfung des Datenschutzes ist offen (`open-questions.md` #28).
- ✅ **Sicherheit:** Security-Review von Infrastruktur und Software (20.10.2026), Befunde behoben: Rate Limits der Konto-Formulare, keine Konto-Enumeration, Security-Header, Rechte der Versionsansicht, Schwärzen von Versionen.
- ⏳ **Validierung:** Link an alte Kontakte, Clubs, Shops, Facebook- und WhatsApp-Gruppen. Erfolgskriterien in `vision.md`.

## Phase 1 – alle grossen Seen

- 🟡 Alle grossen Seen mit Prognose (51 Seen, 19.10.2026) und Flussplätze mit BAFU-Stationen umgesetzt. Die Dokumentation der Plätze (Stufe 1 bis 2) ist offen.
- ✅ BAFU-Abflussvorhersage (Zugang per HTTPS, 20.10.2026), auch im Index (`rules-1.4`). ⏳ In-situ-Messungen aus Eawag Datalakes.
- ⏳ Rückfall für die gemessenen Abflüsse direkt beim BAFU (hydrodaten.admin.ch bzw. derselbe Zugang wie die Vorhersage), falls existenz.ch ausfällt. Anlass: Ausfall am 21.10.2026 ab 18:30 UTC, alle Abrufe mit Timeout. Vorher beim BAFU klären, welche Messdateien der Zugang enthält.
- ✅ Meldungen nach dem Tauchgang (Sicht, Temperatur, Kommentar nach Freigabe), vorgezogen und gebaut am 21.10.2026 (`dive-site.md`, «Meldungen»).
- ⏳ Tauchshops und Füllstationen (kostenlos, ohne Hervorhebung).
- ⏳ Buddy-Gruppen (WhatsApp) pro Region.
- ✅ Registrierung für alle: Selbstregistrierung mit Schutz gegen Bots, Rollen vergeben (Admins ernennen Moderatoren) und Mails bei Freigabe oder Ablehnung (`cms.md`). In Prod offen seit 23.10.2026.
- ✅ Texte automatisch übersetzen (Claude API mit Glossar), im CMS und im Bearbeiten-Modus (22.10.2026, `cms.md`).
- ⏳ Videos: YouTube-Links pro Platz, eingebettet auf der Platzseite.
- Später möglich: Kommentare unter Plätzen, eventuell nur für registrierte Konten und mit Freigabe (`features.md`).
- Neue Fotos der Ein- und Ausstiege, nach und nach.

## Zusätzlich umgesetzt (nicht im ursprünglichen Plan)

- ✅ Kachel «Strömung» an Seen aus dem 3D-Modell (19.10.2026).
- ✅ Kachel «Abfluss» an Flussplätzen mit Einordnung, Trend, 48 Stunden, 7 Tagesmitteln und BAFU-Vorhersage; Kachel «Wassertemperatur» am Fluss (19.–20.10.2026).
- ✅ Regeln `rules-1.3` bis `rules-1.6`: Sicht begrenzt den Index, Zuflüsse nach Nähe zur Mündung, Strömungsabzug am Fluss, Sprungschicht erst ab 1 m Tiefe (20.–21.10.2026).
- ✅ Index-Verlauf: Mini-Diagramme in den Kacheln, Verlauf im Kopf der Platzseite, «Jahr über Jahr» auf der Gewässerseite, Neuberechnung der Vergangenheit auf `/platform` (20.10.2026).
- ✅ Startseite: höchstens 2 Plätze pro Gewässer, Distanz zum gewählten Ort, Bergsee-Notiz, gemerkter Ort, Standort auf Klick (20.10.2026).
- ✅ Plattform: Konto-Detailseite (Bestätigen, Sperren, Löschen, letzte Anmeldung), Übersicht der Prognose-Läufe, Mail-Queue, Inhalt von Prod nach lokal (ADR-011).
- ✅ Seite «Über» beschreibt alle Teilwerte des Index mit den Zahlen der aktuellen Regel.

## Phase 2 – Modell und Komfort

- ⏳ Eigenes Sichtmodell (ML.NET, LightGBM) mit Meldungen, Secchi-Messungen und historischen Labels aus Marcs Logs und Fotos. Schattenbetrieb, dann Wechsel bei klarer Verbesserung.
- ⏳ Benachrichtigungen («gute Bedingungen am Walensee»).
- ⏳ PWA mit Favoriten offline.
- ⏳ Native Apps nur, wenn die Web-App trägt.
- ⏳ Rückrechnung des Index für die Jahre vor dem Start, einmalig pro Gewässer: Sentinel-3 und Sentinel-2 von Alplakes (seit 2016), Simstrat-Hindcast, Wetter aus dem Archiv von Open-Meteo. Vorher die Nutzung und die Zahl der Calls mit Eawag und Open-Meteo klären (Entscheid Marc, 19.10.2026). Grundlage für Phase 2 (Trainingsdaten) und füllt die Ansicht «Jahr über Jahr» (die Ansicht selbst ist gebaut).

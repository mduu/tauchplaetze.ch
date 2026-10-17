# tauchplaetze.ch – Projektdossier

Dieses Dossier ist der Startpunkt für die Umsetzung von **tauchplaetze.ch**. Es enthält Vision, Fachlichkeit, Architektur, UX und Entscheide. Claude Code liest es in jeder Session als Grundlage.

Stand: 17.10.2026 · Autor: Marc · Status: Entwurf vor dem ersten Code

## Worum es geht

tauchplaetze.ch ist ein digitaler Tauchplatzführer für die Schweizer Seen. Er beantwortet eine Frage: **«Wo soll ich tauchen gehen?»**

Dazu verbindet die Plattform zwei Dinge:

1. **Ein Verzeichnis aller Tauchplätze.** Ein Platz ist schon mit Name, Koordinate und See erfasst. Details kommen nach und nach dazu.
2. **Eine tägliche Tauchprognose für jeden Platz**, so genau, wie die Daten es erlauben (Datenstufe). Sie stammt aus offenen Daten: Seemodelle und Satellit (Eawag Alplakes), Wetter (MeteoSchweiz über Open-Meteo) und Abfluss (BAFU). Sie braucht keine Meldungen aus der Community. Meldungen verbessern sie nur.

Die Plattform ist ein Leidenschaftsprojekt und nicht kommerziell. Der Betrieb soll unter CHF 20 pro Monat kosten.

## Domains

| Domain | Rolle |
|---|---|
| `tauchplaetze.ch` | Hauptdomain der Plattform |
| `sichtweiten.ch` | Einstieg direkt in die Prognose (Redirect auf `/prognose`) |
| `tauchfotos.ch` | Bestehendes Fotoarchiv (Quelle für Bildmaterial) |
| `tauchvideos.ch` | Bestehendes Videoarchiv (Quelle für Videos) |
| `tauchbolde.ch` | Website der eigenen Tauchgruppe (nicht Teil dieser Plattform) |

## Inhalt des Dossiers

```
README.md                 dieses Dokument
CLAUDE.md                 Vorlage für das Repository (Regeln für Claude Code)
specs/
├── vision.md             Produkt, Zielgruppe, Abgrenzung, Erfolgskriterien
├── ubiquitous-language.md  Fachbegriffe mit Übersetzungen DE/EN/FR/IT
├── features.md           Features nach Phase (MVP, später)
├── datastructure.md      Entitäten, Felder, Beziehungen, EF-Hinweise
├── cms.md                Redaktion: Erfassen, Ausbaustufen, Revisionen, Medien
├── forecast.md           Tauch-Index: Eingaben, Formel, Job, späteres ML-Modell
├── data-sources.md       Alplakes, MeteoSchweiz, BAFU: Endpunkte, Recht, Ausfälle
├── architecture.md       Stack, Projektstruktur, Vertical Slices, CQS, Coding Guidelines, Tests
├── i18n.md               Mehrsprachigkeit (DE Standard, EN, FR, IT)
├── ux.md                 Seiten, Navigation, Zustände, Barrierefreiheit
├── design-system.md      Dunkles Farbschema, Typografie, Komponenten
├── design/               Mockups aus dem Entwurf (Startseite, Tauchplatz)
├── seo.md                URLs, Sitemap, strukturierte Daten, Domains
├── infrastructure.md     Azure-Ressourcen, Kosten, CI/CD, lokale Entwicklung
├── roadmap.md            Phasen und Meilensteine
├── open-questions.md     Offene Punkte und nächste Abklärungen
└── adrs/                 Architecture Decision Records
```

## Lesereihenfolge

1. `specs/vision.md`: was wir bauen und warum.
2. `specs/architecture.md`: wie der Code aufgebaut ist.
3. Die Spec des Bereichs, an dem gerade gearbeitet wird.
4. `specs/open-questions.md`: was noch nicht entschieden ist.

## Kernentscheide auf einen Blick

| Thema | Entscheid | ADR |
|---|---|---|
| Frontend | Blazor Web App, öffentliche Seiten Static SSR, Admin Interactive Server | [001](specs/adrs/001-blazor-ssr-und-interactive-server.md) |
| Code-Struktur | Vertical Slices, Commands und Queries getrennt, Core plus Fassade | [002](specs/adrs/002-vertical-slices-und-cqs.md) |
| Datenbank | Azure SQL Database (Free Offer), Medien in Blob Storage | [003](specs/adrs/003-azure-sql-free.md) |
| Prognose | Zuerst regelbasierter Tauch-Index, ML-Modell erst mit Labels | [004](specs/adrs/004-prognose-regelbasiert-zuerst.md) |
| Sprachen | Deutsch Standard, dazu EN, FR, IT ab dem ersten Release | [005](specs/adrs/005-mehrsprachigkeit.md) |
| Hosting | Azure App Service (Linux B1), Background Worker im selben Host | [006](specs/adrs/006-app-service-mit-worker.md) |
| Wetter und Abfluss | Open-Meteo (Prognose), MeteoSchweiz SwissMetNet (Messung), BAFU über existenz.ch | [007](specs/adrs/007-wetter-und-abflussquellen.md) |
| Design | Dunkles Farbschema («Kaltwasser-Nacht») | [design-system.md](specs/design-system.md) |

## Bezug zu bestehenden Projekten

Die Konventionen folgen **feldnah** und **bau-ticker** (beide private Repositories von Marc):

- Vertical Slices unter `Features/{Slice}/Commands` und `Features/{Slice}/Queries`.
- Ein Use Case pro Datei: Record plus Handler mit der Methode `Handle`.
- Eigene Contracts `ICommandHandler<TCommand, TResult>` und `IQueryHandler<TQuery, TResult>`, kein MediatR.
- Core-Projekt als einziger Zugang zu Datenbank, Storage und externen Diensten. Web und Worker sind Fassaden.
- xUnit ohne Mocking Library, Testcontainers. Verhalten als Black-Box testen, nicht Mapping.

Details stehen in `specs/architecture.md`.

## So startet die erste Session

1. Neues Repository anlegen. `CLAUDE.md` und `specs/` aus diesem Dossier hineinkopieren.
2. Claude Code mit diesem Auftrag starten: «Lies README.md und specs/architecture.md. Setze das Solution-Gerüst nach Phase 0 in specs/roadmap.md auf.»
3. Vor dem Bau der Prognose: Eawag wegen der Datennutzung anschreiben (siehe `specs/open-questions.md`).

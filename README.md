<p align="center">
  <a href="https://tauchplaetze.ch"><img src="src/Tauchplaetze.Web/wwwroot/img/logo-128.png" alt="tauchplaetze.ch" width="96" height="96"></a>
</p>

<h1 align="center">tauchplaetze.ch</h1>

<p align="center">
  <strong>Wo tauchen wir heute?</strong><br>
  Tauchplatz-Führer für Schweizer Seen und Flüsse mit täglicher Tauchprognose.
</p>

<p align="center">
  <a href="https://tauchplaetze.ch">tauchplaetze.ch</a> ·
  <a href="specs/vision.md">Vision</a> ·
  <a href="specs/roadmap.md">Roadmap</a>
</p>

<p align="center">
  <a href="https://github.com/mduu/tauchplaetze.ch/actions/workflows/ci.yml"><img src="https://github.com/mduu/tauchplaetze.ch/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
</p>

---

## Was es ist

- **Verzeichnis:** Alle bekannten Tauchplätze der Schweiz mit Karte, Einstiegen, Bewertungen und Fotos. Ein Platz ist schon mit Name, Koordinate und Gewässer vollständig.
- **Tauchprognose:** Pro Platz und Tag ein Tauch-Index von 0 bis 10, mit Sicht, Wind, Zuflüssen, Sprungschicht, Strömung und Wassertemperatur.
- **Offene Daten:** Seemodelle und Satellit, Wetter und Abfluss. Die Prognose braucht keine Meldungen, Meldungen verbessern sie nur.
- **Gemeinsam gepflegt:** Alle können Inhalte vorschlagen, Moderatoren geben sie frei. Jede Version bleibt nachvollziehbar.

Das Projekt ist nicht kommerziell.

## Schnellstart

Voraussetzungen: [.NET 10 SDK](https://dotnet.microsoft.com/download) und Docker.

```bash
cd src/Tauchplaetze.AppHost && dotnet run     # App mit SQL Server, Azurite und Mailpit (Aspire)
cd src && dotnet build && dotnet test --no-build  # Build und Tests (Docker für die SQL-Tests)
```

Lokale Einstellungen wie Admin-Konto und Zugangsdaten stehen in [`specs/infrastructure.md`](specs/infrastructure.md), Abschnitt «Umgebungen».

## Projektstruktur

```
src/
├── Tauchplaetze.Core/            Fachlogik: Commands, Queries, Datenbank, Storage, Datenquellen
├── Tauchplaetze.Web/             Fassade: öffentliche Seiten, CMS, Plattform, Worker
├── Tauchplaetze.AppHost/         Aspire für die lokale Entwicklung
├── Tauchplaetze.ServiceDefaults/ Telemetrie, Health Checks, Resilience
└── *.Tests/                      xUnit, Testcontainers
deploy/                           Bicep für Azure
specs/                            Specs und ADRs, die Quelle der Wahrheit
doc/                              Go-live, Mails, Spikes
```

## Dokumentation

Die Specs beschreiben, was gilt. Zuerst [`vision.md`](specs/vision.md) und [`architecture.md`](specs/architecture.md), dann die Spec des Bereichs.

| Bereich | Spec |
|---|---|
| Produkt, Ziele, Abgrenzung | [vision.md](specs/vision.md), [features.md](specs/features.md), [roadmap.md](specs/roadmap.md) |
| Fachbegriffe (DE/EN/FR/IT) | [ubiquitous-language.md](specs/ubiquitous-language.md) |
| Architektur, Coding Guidelines, Tests | [architecture.md](specs/architecture.md) |
| Datenmodell | [datastructure.md](specs/datastructure.md) |
| Tauchplatz, Bewertungen | [dive-site.md](specs/dive-site.md) |
| Redaktion, Versionen | [cms.md](specs/cms.md) |
| Prognose, Tauch-Index | [forecast.md](specs/forecast.md) |
| Datenquellen | [data-sources.md](specs/data-sources.md) |
| Sprachen | [i18n.md](specs/i18n.md) |
| Seiten, Design | [ux.md](specs/ux.md), [design-system.md](specs/design-system.md) |
| URLs, Domains, SEO | [seo.md](specs/seo.md) |
| Hosting, Betrieb | [infrastructure.md](specs/infrastructure.md), [doc/go-live.md](doc/go-live.md) |
| Entscheide | [adrs/](specs/adrs/README.md) |
| Offene Punkte | [open-questions.md](specs/open-questions.md) |

Für Coding Agents gilt [`AGENTS.md`](AGENTS.md).

## Datenquellen

Seemodelle und Satellit: [Eawag Alplakes](https://www.alplakes.eawag.ch) · Wetter: MeteoSchweiz und DWD über [Open-Meteo](https://open-meteo.com) · Abfluss und Abflussvorhersage: [Bundesamt für Umwelt BAFU](https://www.hydrodaten.admin.ch), Messwerte über [existenz.ch](https://api.existenz.ch) · Karten und Tiefenlinien: [swisstopo](https://www.swisstopo.admin.ch). Bedingungen und Nutzung: [`data-sources.md`](specs/data-sources.md).

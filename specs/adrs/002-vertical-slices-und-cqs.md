# ADR-002: Vertical Slices, CQS und Core plus Fassade

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Marc baut bei feldnah und bau-ticker mit Vertical Slices und getrennten Commands und Queries. Die Struktur hat sich bewährt.
- Claude Code findet sich in einer festen, wiederholten Struktur schneller zurecht.
- Das Projekt hat einen Hintergrund-Job (Prognose) und später evtl. ein CLI. Beide sollen dieselbe Logik nutzen wie die Website.

## Entscheidung

Wir entscheiden uns für **Vertical Slices mit CQS und einem Core-Projekt hinter Fassaden**:

- Ein Use Case pro Datei in `Features/{Slice}/Commands/` oder `Features/{Slice}/Queries/`.
- Eigene Contracts `ICommandHandler<TCommand, TResult>` und `IQueryHandler<TQuery, TResult>` mit der Methode `Handle`. Kein MediatR.
- Nur `Tauchplaetze.Core` greift auf Datenbank, Storage und externe Dienste zu. `Tauchplaetze.Web` ruft nur Handler auf.
- Handler sind `internal`.
- Architekturtests (ArchUnitNET) prüfen die Regeln.

## Optionen

### Option A: Klassische Schichten (Controller, Service, Repository)

**Nachteile:**
- Ein Feature verteilt sich über viele Ordner.
- Repositories über EF Core bringen keinen Mehrwert.

### Option B: Vertical Slices mit MediatR

**Nachteile:**
- Zusätzliche Abhängigkeit (Lizenzänderung bei MediatR).
- Indirektion ohne Nutzen. Direkte Injektion des Handlers ist klarer.

### Option C: Vertical Slices, eigene Contracts, Core plus Fassade (gewählt)

**Vorteile:**
- Gleiche Struktur wie feldnah und bau-ticker.
- Worker und spätere CLI nutzen dieselben Handler.
- Grenzen sind testbar.

## Konsequenzen

### Positive Auswirkungen

- Ein Use Case ist schnell gefunden und isoliert testbar.
- Neue Slices ändern keine zentrale Datei (Registrierung per Assembly-Scan und `ICoreModule`).

### Negative Auswirkungen / Risiken

- Etwas Wiederholung zwischen ähnlichen Handlern. Akzeptiert. Gemeinsame Logik als `internal` Klasse im Slice.

## Referenzen

- `architecture.md`
- feldnah: `specs/architecture.md`
- bau-ticker: `specs/architecture.md`, ADR-007

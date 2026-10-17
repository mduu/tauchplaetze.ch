# Claude Code Instructions

**tauchplaetze.ch** is a dive site guide for Swiss lakes with a daily dive forecast for each site. It answers one question: "Where should I go diving?"

Stack: .NET 10, Blazor Web App (Static SSR for public pages, Interactive Server for the admin area), EF Core on Azure SQL, Azure Blob Storage, xUnit, Shouldly and NSubstitute. Solution: `src/Tauchplaetze.slnx`.

This file gives the short rules that always apply. The specs give the details. Before you change an area, read its spec:

| Area | Spec |
|---|---|
| Product goals, scope | `specs/vision.md` |
| Domain terms (DE/EN/FR/IT) | `specs/ubiquitous-language.md` |
| Architecture, coding guidelines, testing | `specs/architecture.md` |
| Entities, EF configuration | `specs/datastructure.md` |
| Admin area, content levels, revisions | `specs/cms.md` |
| Forecast, dive index, jobs | `specs/forecast.md` |
| External data (Alplakes, MeteoSwiss, BAFU) | `specs/data-sources.md` |
| Languages, resources | `specs/i18n.md` |
| UX, pages, colours | `specs/ux.md`, `specs/design-system.md` |
| URLs, sitemap, domains | `specs/seo.md` |
| Hosting, deployment | `specs/infrastructure.md` |
| Decisions | `specs/adrs/` |

## Key Principles

- **Vertical slices and CQS**: one use case is one file in `Features/{Slice}/Commands/` or `Features/{Slice}/Queries/`. The file has a record and a handler with the method `Handle`. A query never has side effects. Use `Unit` when a command has no return value.
- **Core and facades**: only `Tauchplaetze.Core` accesses the database, Blob Storage and external services. `Tauchplaetze.Web` (pages, admin, worker host) calls handlers only. Business logic in a facade is a bug.
- **Directory first**: a dive site with name, coordinate and lake is complete enough for the forecast. All other fields are optional.
- **Never call external data sources from a page request.** The worker imports data. Pages read our own database.
- **Simple and fast public pages**: Static SSR, output cache, no SignalR circuit for visitors.

## Language

- UI: German (default), English, French and Italian (ADR-005).
- Code, identifiers, API fields, log messages: English.
- Specs, ADRs: German.
- Formats: Swiss (dd.MM.yyyy, °C with one decimal, metres).
- **Each user-facing string comes from a `.resx` file.** Put a new or changed key into `*.resx` (German), `*.en.resx`, `*.fr.resx` and `*.it.resx` with real translations in the same change. Use the glossary in `specs/ubiquitous-language.md` for dive terms.
- Look at each changed surface in all four languages. French and Italian text is often longer.

## Writing

- Write English text (comments, commits, this file) in ASD-STE100 Simplified Technical English.
- Write German text (specs) with the same principles: short sentences, one statement in each sentence, active voice, no filler words.
- These rules do not apply to UI text and resource strings.
- Commits and PR titles: Conventional Commits, `type(scope): description`.

## Coding

- Write fields in camelCase. Do not use a `_` prefix. Use modern C#. Put braces on each `if`/`else` branch.
- Do not use an `Async` suffix on method names.
- Handler classes are `internal`. Facades inject the contract (`ICommandHandler<,>`, `IQueryHandler<,>`).
- Comments:
  - First make the code clearer, for example with Extract Method.
  - Write a comment only when the code cannot show the information.
  - Write comments on members as XML documentation (`///`). Usually, do not comment private members.

## Testing

Test behaviour: business rules, edge cases, the dive index calculation, state transitions, content levels. Do not test mapping, CRUD roundtrips or mock calls on thin wrappers. Mark the phases with `// Arrange`, `// Act` and `// Assert`.

## Running

Run `cd src/Tauchplaetze.AppHost && dotnet run`. Aspire starts SQL Server and Azurite in containers.

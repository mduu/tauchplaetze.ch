# Agent Guide

**tauchplaetze.ch** is a dive site guide for all Swiss lakes and rivers with a daily dive forecast for each site. It answers one question: "Where should I go diving?" The project is not commercial. Start with `README.md`.

Stack: .NET 10, Blazor Web App (Static SSR for public pages, Interactive Server for the admin area), EF Core on Azure SQL, Azure Blob Storage, xUnit, Testcontainers. Solution: `src/Tauchplaetze.slnx`.

The rules are in `specs/architecture.md`. Read the applicable section before you change an area. Domain terms are in `specs/ubiquitous-language.md`. Each area has a spec:

| Area | Spec |
|---|---|
| Product goals, scope | `specs/vision.md` |
| Domain terms (DE/EN/FR/IT) | `specs/ubiquitous-language.md` |
| Architecture, coding guidelines, testing | `specs/architecture.md` |
| Entities, EF configuration | `specs/datastructure.md` |
| Dive site content, rating scales | `specs/dive-site.md` |
| Admin area, content levels, revisions | `specs/cms.md` |
| Forecast, dive index, data levels, jobs | `specs/forecast.md` |
| External data sources | `specs/data-sources.md` |
| Languages, resources | `specs/i18n.md` |
| UX, pages, colours | `specs/ux.md`, `specs/design-system.md` |
| URLs, sitemap, domains | `specs/seo.md` |
| Hosting, deployment | `specs/infrastructure.md` |
| Decisions | `specs/adrs/` |

## Build and Test

```bash
cd src && dotnet build && dotnet test --no-build
```

The handler tests with a database need Docker (Testcontainers). Run the app with `cd src/Tauchplaetze.AppHost && dotnet run`. Aspire starts SQL Server and Azurite in containers.

Use the `review` skill (`.claude/skills/review/`) before each push.

## Key Principles

- **Vertical slices and CQS**: one use case is one file in `Features/{Slice}/Commands/` or `Features/{Slice}/Queries/`. The file has a record and a handler with the method `Handle`. A query never has side effects. Use `Unit` when a command has no return value.
- **Core and facades**: only `Tauchplaetze.Core` accesses the database, Blob Storage and external services. `Tauchplaetze.Web` (pages, admin, worker host) calls handlers only. Business logic in a facade is a bug.
- **Directory first**: a dive site with name, coordinate and water body is complete. All other fields are optional. A site can have exact data, simple data or no data (data level). It can have much or no documentation (content level).
- **Never call external data sources from a page request.** The worker imports data. Pages read our own database.
- **Simple and fast public pages**: Static SSR, output cache, no SignalR circuit for visitors.

## Language

- Talk to Marc in German (Swiss spelling, «ss»). This applies to all answers, questions and summaries in the chat.
- Write identifiers, types, API fields and log messages in English.
- Write comments, specs, ADRs and commit messages in German. Use the English IT terms (Cache, Lock, Query, Retry). The list is in `specs/architecture.md`, section "Sprache".
- UI: German (default), English, French and Italian (ADR-005). **Each user-facing string comes from a `.resx` file.** Put a new or changed key into `*.resx` (German), `*.en.resx`, `*.fr.resx` and `*.it.resx` with real translations in the same change. Use the glossary in `specs/ubiquitous-language.md` for dive terms.
- Look at each changed surface in all four languages. French and Italian text is often longer.
- Formats: Swiss (dd.MM.yyyy, °C with one decimal, metres).
- Do not change user-facing text when you only edit wording in code or docs.

## Commits and Pull Requests

- Write commit messages and PR titles as Conventional Commits: `type(scope): description`. The scope is the slice name in lower case.
- Keep the PR description short. It becomes the squash commit message. Do not put HTML, Claude links or session IDs in it. Do not add a `Co-Authored-By` trailer. The details are in `specs/architecture.md`, section "Commits und Pull Requests".

## Coding

- Write fields in camelCase. Do not use a `_` prefix. Use modern C#. Put braces on each `if`/`else` branch.
- Do not use an `Async` suffix on method names.
- Handler classes are `internal`. Facades inject the contract (`ICommandHandler<,>`, `IQueryHandler<,>`).

## Comments

The details are in `specs/architecture.md`, section "Code-Dokumentation".

- First make the code clearer, for example with Extract Method. Give the method a name that tells what it does.
- Comments are the exception. When in doubt, do not write a comment. Design and history go into the specs and ADRs.
- Write a comment only when the code cannot show the information and the comment adds real value.
- Keep each comment as short as possible.
- Write comments on members as XML documentation (`///`). Do not write comments in method bodies.
- Usually, do not comment private members.

## Testing

The details are in `specs/architecture.md`, section "Testing".

- Write a test only when it checks logic: business rules, edge cases, calculations (dive index), state transitions, content levels and data levels. A test must find a real defect.
- Do not write tests for a coverage quota. Do not test mapping, CRUD roundtrips or calls on thin wrappers. Do not write the same test twice.
- Test the system under test (SUT) as a black box. Give the input through its public API and check the output. Do not check private members, intermediate states or the order of internal calls. A test must stay green after a refactoring that keeps the behaviour.
- The SUT is a use case (test it through its handler or HTTP) or one unit that holds the rule itself (for example a calculator or a parser).
- Use xUnit `Assert` and small handwritten test doubles. Do not use a mocking library.
- Write the test name as a German sentence that states the rule.
- Mark the phases with `// Arrange`, `// Act` and `// Assert`.

## Writing

- Write English text (`AGENTS.md`) in ASD-STE100 Simplified Technical English. Use the skill in `.claude/skills/asd-ste100/`.
- Write German text with the same principles: short sentences, one statement in each sentence, active voice, no filler words. The details are in `specs/architecture.md`, section "Schreiben".

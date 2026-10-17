---
name: review
description: Prüft die Änderungen vor einem Push gegen specs/architecture.md – mit Schwerpunkt auf Fassade, Seiteneffekten, Idempotenz, Persistenz und Mehrsprachigkeit; prüft, ob die Specs nachgezogen sind, ob die Bedingungen der Datenquellen eingehalten sind, und schätzt die Kostenwirkung im Betrieb (Azure, externe Dienste). Vor jedem Push ausführen (ein Hook erinnert, wenn der Stand nicht geprüft ist) und immer, wenn ein Review verlangt ist. Auch auf Marcs Wunsch mit /review.
---

# Review vor dem Push

Prüft alles, was mit dem nächsten Push hinausginge, gegen die Regeln in
[`specs/architecture.md`](../../../specs/architecture.md). Am Ende steht ein Urteil: **bestanden**
oder **blockiert**. Bei «blockiert» legt Claude Marc die Befunde vor, statt zu pushen.

## Fortschritt

Vor dem Start die Liste zeigen, nach jedem Schritt neu:

```
👷‍♂️ Umfang bestimmen (in Arbeit)
⏳ Bauen und testen (offen)
⏳ Code prüfen (offen)
⏳ Specs und Datenquellen prüfen (offen)
⏳ Seiten ansehen (offen)
⏳ Kosten prüfen (offen)
⏳ Urteil (offen)
```

Erledigt ✅, aktuell 👷‍♂️, offen ⏳, gescheitert ❌.

## 1. Umfang bestimmen

```bash
git fetch -q
git log --oneline @{upstream}..HEAD        # was hinausginge
git diff --stat @{upstream}...HEAD
git status --short                          # Uncommittetes geht nicht mit – nennen, nicht prüfen
```

Ohne Upstream: gegen `origin/main`. Die geänderten Dateien den Bereichen zuordnen – davon hängt
ab, welche Prüfungen unten greifen:

| Bereich | Erkennbar an |
| :-- | :-- |
| **Persistenz** | `Core/Data/**` (Entities, Configurations, Migrations, `TauchplaetzeDbContext`), Blob-Pfade (`forecast-raw/…`, Medien), JSON-Spalten (`Snapshot`, `SourceStatus`), Enum-Namen in der Datenbank |
| **Fassade** | alles unter `Tauchplaetze.Web/**`, `*Worker.cs`, `Core/Features/**/*Module.cs`, `ArchitectureTests.cs` |
| **Datenquellen** | `Core/Features/Forecasts/**` (Clients, Mapper, `DiveIndexCalculator`, `ThermoclineDetector`), `appsettings.json` (`Forecast:*`), `TestData/**` |
| **Öffentliche Seiten** | `Web/Features/**/*.razor` ausserhalb von `/admin`, `Components/Layout/**`, `wwwroot/**`, Output Cache, SEO (Sitemap, `hreflang`, JSON-LD) |
| **Redaktion** | `Web/Features/Editorial/**`, `Core/Features/Editorial/**` |
| **Texte** | `Web/Localization/*.resx` |
| **Konfiguration** | `appsettings*.json`, `*Options.cs`, `deploy/**`, `Tauchplaetze.AppHost/**` |

Bei mehr als etwa 30 geänderten Dateien oder Änderungen in mehreren Bereichen: den Code-Teil
(Schritt 3) von einem **frischen Subagenten** prüfen lassen, der nur den Diff, `specs/architecture.md`
und die betroffenen Specs bekommt – unabhängig vom Kontext, in dem die Änderung entstand. Seine
Befunde selbst nachprüfen, bevor sie zählen.

## 2. Bauen und testen

```bash
cd src && find . -maxdepth 3 -type d \( -name obj -o -name bin \) -exec rm -rf {} + && dotnet build && dotnet test --no-build
```

Die Tests laufen wie in der CI: ohne Aspire, ohne Azurite, ohne Live-Aufruf einer Datenquelle. Die
Handler-Tests mit Datenbank brauchen **Docker** (Testcontainers mit SQL Server). Laufen sie nicht
(übersprungen oder Docker aus), ist das zu nennen – grün heisst hier nur grün, wenn sie liefen.
Rot heisst **blockiert** – ohne Ausnahme. Sauber heisst ohne `obj/` und `bin/`: Ein inkrementeller
Build kann Razor-Fehler verdecken (Erfahrung aus bau-ticker, 03.10.2026).

## 3. Code prüfen

Den Diff lesen, nicht nur die Dateinamen. Für jeden Befund: Datei:Zeile, was, warum, Schwere
(**Blocker** / **Hinweis**).

### Fassade

`Tauchplaetze.Web` ist eine Fassade (ADR-002, `specs/architecture.md`, «Prinzipien»). Nur der Core greift auf Datenbank, Blob Storage und externe Dienste zu, und nur über Commands und Queries.

- **Kein Datenbank- oder Storage-Zugriff im Web:** kein `TauchplaetzeDbContext`, keine Entity aus `Tauchplaetze.Core.Data`, kein Azure SDK, kein `HttpClient` auf eine Datenquelle. Seiten, Admin-Komponenten, Endpoints und Worker rufen **nur Handler** und bilden das Ergebnis ab. Verstoss = **Blocker**.
- Neue Fachlogik gehört als Handler in `Core/Features/{Slice}/Commands|Queries/`, nicht in eine `.razor`-Datei, einen Endpoint oder den `ForecastWorker`. Commands tragen die Benutzer-ID als Wert, nicht den `ClaimsPrincipal`.
- Handler-Klassen sind `internal`. Öffentlich ist nur, was die Fassade braucht: Contract, Command, Query, Ergebnis, DTO.
- Eine Regel in `ArchitectureTests` gelockert, übersprungen oder um eine Ausnahme erweitert: nur mit Begründung im Code und Entscheid von Marc – sonst **Blocker**.
- **Keine externe Quelle aus einer Seitenanfrage.** Seiten lesen die eigene Datenbank. Verstoss = **Blocker**.

### Seiteneffekte (CQS)

- Eine **Query** schreibt nichts: kein `SaveChanges`, kein Blob-Upload, kein Zähler, kein Aufräumen beim Lesen. Verstoss = **Blocker**.
- Ein **Command** hat seine Seiteneffekte dort, wo man sie erwartet – keine versteckten externen Calls in einem Helfer mit harmlosem Namen.
- Externe Calls (Alplakes, Wetter, Abfluss, Mail) im Diff einzeln nennen: Wie oft pro Lauf? Was kostet ein Retry die Quelle?

### Idempotenz

Für jeden neuen oder geänderten Command, Worker und jede Admin-Aktion fragen: **Was passiert beim zweiten Mal?** – Retry, Doppelklick, zweiter Prognose-Lauf am selben Tag, paralleler Lauf auf zwei Instanzen.

- `RunForecast` schreibt per Upsert (`DiveSiteId`, `Date`). Ein zweiter Lauf erzeugt keine doppelten Rows.
- Der Lauf hält den Lock (Blob Lease). Zwei Läufe gleichzeitig sind ausgeschlossen.
- Eine Speicherung im CMS ohne Änderung erzeugt keine neue Revision.
- Nicht idempotent, ohne begründete Ausnahme im Code = **Blocker**.

### Breaking Changes

Nichts darf brechen, was schon draussen ist: Links, gespeicherte Daten, laufende Konfiguration.

- **URLs** (Sprachpräfix, Slugs, `/prognose`): Ein geänderter Slug braucht einen Eintrag in `SlugRedirects` (301). Eine umbenannte Route braucht einen Redirect. Suchmaschinen und geteilte Links zeigen auf die alten Adressen.
- **Configuration Keys** (`Section__Key` im Betrieb) umbenannt = Breaking Change.
- **`ModelVersion`:** Eine geänderte Regel oder ein geändertes Gewicht im Index erhöht die Version (`rules-1.1` …). Sonst lassen sich alte und neue Werte nicht unterscheiden.

### Persistenz (besonders heikel)

Gespeicherte Daten überleben den Code, der sie geschrieben hat.

- **Alte Daten lesbar?** Jede Änderung an einer gespeicherten Form (Column, JSON-Snapshot einer Revision, Enum-Name, Blob-Pfad) muss Rows lesen können, die der alte Code geschrieben hat. Besonders die Revisionen: `RestoreRevision` muss einen alten Snapshot wiederherstellen können. Ein Test mit einem Snapshot im **alten** Format gehört dazu.
- **Leer ist nicht unbekannt:** Ein neues Feld, das bei alten Rows fehlt, darf nicht wie «keins» behandelt werden, wenn es «nicht erhoben» heisst. Bei der Prognose: Ein fehlender Wert ist `null`, nie `0`.
- **EF-Migrationen:** Keine verlustbehaftete Operation (Column löschen, Typ verengen, NOT NULL ohne Default auf befüllter Tabelle) ohne Übergang. Die Migration ist Generated Code, aber ihr Inhalt wird gelesen.
- **Concurrency:** Kann derselbe Datensatz gleichzeitig vom Worker und im CMS geschrieben werden? Dann `rowversion` oder Lock.
- **Rohdaten:** Antworten der Quellen liegen unverändert in Blob Storage. Alte Läufe müssen sich neu berechnen lassen.
- Fehlt etwas davon = **Blocker**.

### Mehrsprachigkeit

- Jeder sichtbare Text kommt aus `.resx`. Ein neuer oder geänderter Key steht in `*.resx`, `*.en.resx`, `*.fr.resx` und `*.it.resx` mit **echter** Übersetzung (kein deutscher Platzhalter, kein leerer Wert). Fehlt eine = **Blocker**.
- Fachbegriffe nach [`specs/ubiquitous-language.md`](../../../specs/ubiquitous-language.md).
- Formate schweizerisch (`dd.MM.yyyy`, °C mit einer Dezimale, Meter).

### Coding Guidelines und Tests

Nach architecture.md, für die geänderten Stellen (nicht den ganzen Bestand):

- Kein `Async`-Suffix, keine `_`-Felder, Klammern bei `if`/`else`, `CancellationToken`
  durchgereicht, Code englisch, Kommentare deutsch und sparsam (Extract Method), keine Kommentare
  in Method Bodies, XML-Doc auf Interfaces Pflicht.
- IT-Begriffe in Kommentaren, Specs und Commit-Nachrichten englisch im Original (Cache, Lock,
  Query, Retry …, nicht «Zwischenspeicher», «Sperre», «Abfrage», «Wiederholung»); Fachbegriffe
  aus `specs/ubiquitous-language.md` und Oberflächentexte bleiben in der Sprache der Leser
  (architecture.md, «Sprache»).
- Das SUT stimmt (architecture.md, «Was ist das SUT?»): Use Case über Handler oder HTTP, eine `internal` Klasse nur direkt, wenn sie selbst das SUT ist (etwa `DiveIndexCalculator`, `ThermoclineDetector`).
- Neue Logik hat Tests, die sich lohnen: Black-Box, `// Arrange` `// Act` `// Assert`,
  deutscher Satz als Name, keine Mapping-Tests, kein Test, der Interna prüft.
- Ein behobener Fehler hat einen Test, der ihn beim Namen nennt.
- Kein Test doppelt (architecture.md, «Kein Test doppelt»): Ist die Regel schon getestet? Varianten derselben Regel gehören in eine Theory.
- Eine neue oder geänderte Query mit Geodaten oder komplexer Übersetzung läuft gegen SQL Server (Testcontainers), nicht nur gegen einfache Testdaten im Speicher.
- Eine Änderung an einem Client einer Datenquelle hat einen Contract Test mit einer gespeicherten echten Antwort (`TestData/{quelle}/*.json`).
- Verstösse hier sind **Hinweise**, ausser bei fehlenden Tests für neue Fachlogik, Index-Regeln,
  Persistenz oder Idempotenz – das ist ein **Blocker**.

## 4. Specs und Datenquellen prüfen

Die Specs müssen den neuen Stand beschreiben, im selben Push.

| Geändert | Muss nachgezogen sein |
| :-- | :-- |
| Entity, Column, Key, Blob-Pfad, Migration | `specs/datastructure.md` |
| Datenquelle, Endpunkt, Client, Abfragefrequenz | `specs/data-sources.md` |
| Index-Regel, Gewicht, Teilwert, Job, Horizont | `specs/forecast.md` |
| Redaktion, Ausbaustufe, Revision | `specs/cms.md`, `specs/dive-site.md` |
| Seite, Zustand, Darstellung, Farbe | `specs/ux.md`, `specs/design-system.md` |
| URL, Sitemap, Redirect | `specs/seo.md` |
| Sprache, Format, Ressourcen | `specs/i18n.md` |
| Fachbegriff neu oder umbenannt | `specs/ubiquitous-language.md` |
| Architektur, Regeln | `specs/architecture.md` |
| Erledigter offener Punkt | `specs/open-questions.md` (Punkt entfernen, Entscheid in die passende Spec oder ins ADR) |
| Entscheid, der ein ADR berührt | Nachtrag im ADR |

**Ist eine Datenquelle betroffen**, zusätzlich prüfen:

- Bleibt die Nutzung im Rahmen der Bedingungen der Quelle (`data-sources.md`, ADR-007)? Das Projekt ist **nicht kommerziell**. Eine neue Quelle ohne geklärte Nutzung = **Blocker**.
- Quellenangabe auf jeder Seite, die Werte der Quelle zeigt.
- Abrufe schonend und gekennzeichnet: höchstens 2 Anfragen pro Sekunde und Quelle, `User-Agent` mit Kontakt, Retry mit Backoff. Keine höhere Frequenz ohne Grund.
- Datenalter sichtbar: Ein alter Wert erscheint nie als aktueller.

Fehlende Spec-Nachführung = **Blocker** (Specs sind in diesem Projekt die Quelle der Wahrheit).

## 5. Seiten ansehen

Ändert der Push sichtbar eine Seite (Layout, Texte, Farben, Reihenfolge, Zustände), die App
starten (`cd src/Tauchplaetze.AppHost && dotnet run`) und die betroffenen Seiten **in allen vier
Sprachen** ansehen. Französisch und Italienisch sind oft länger. Dazu die Zustände prüfen, die
die Änderung berührt: Platz ohne Daten, Prognose ohne Index (Ausblick), altes Satellitenbild,
leere Abschnitte. Mobil (schmal) und Desktop. Lässt sich das nicht ausführen, ist das ein
**Blocker** mit klarer Ansage, was fehlt.

## 6. Kosten prüfen

Ziel: Betrieb unter CHF 20 pro Monat (`specs/vision.md`, `specs/infrastructure.md`).

| Prüfen | Worauf achten |
| :-- | :-- |
| **Azure** | Neue Ressource oder höhere Stufe in `deploy/`? Mehr Datenbanklast (Free Offer: vCore-Sekunden, Auto-Pause)? Ein Output Cache, der nicht mehr greift? Neue Blobs, deren Volumen wächst? |
| **Externe Dienste** | Mehr Abrufe pro Lauf (pro Platz, pro See, pro Gitterpunkt)? Ein neuer See oder viele neue Plätze: Abrufe pro Tag hochrechnen. Kontingente der Quellen (Open-Meteo, Alplakes). |
| **Kartenkacheln** | Mehr Kachelabrufe, ein Anbieter mit Kontingent? |

Im Bericht eine Zeile **Kostenwirkung**: «keine» oder eine Schätzung je Monat (einmalig und
laufend), mit der Herleitung in einem Satz. Mehr als etwa **CHF 2 pro Monat** zusätzlich:
**Hinweis an Marc**, mit Zahl.

## 7. Urteil

Bericht an Marc, kurz:

- **Umfang:** Commits, Bereiche.
- **Build und Tests:** grün/rot mit Zahlen, SQL-Tests gelaufen ja/nein.
- **Blocker** mit Datei:Zeile und Vorschlag.
- **Hinweise** (gesammelt, knapp).
- **Specs, Datenquellen, Seiten:** nachgezogen bzw. angesehen ja/nein.
- **Kostenwirkung:** keine oder Schätzung je Monat.

Kleine, eindeutige Blocker (fehlender Test, fehlender Spec-Absatz, fehlende Übersetzung,
vergessene Klammer) darf der Review selbst beheben – als eigener Commit, danach den Review
wiederholen. Alles, was eine Entscheidung braucht, Marc vorlegen statt selbst entscheiden.

**Bestanden** heisst: keine Blocker, Build und Tests grün, Specs nachgezogen. Dann den geprüften
Stand vermerken:

```bash
git rev-parse HEAD > "$(git rev-parse --git-common-dir)/tauchplaetze-review-ok"
```

Den Vermerk **in einem eigenen Tool Call** setzen und erst danach pushen – nie im selben Befehl wie
`git push`. Der Hook prüft vor dem Befehl. Stehen beide in einem Tool Call, ist der Vermerk zu
diesem Zeitpunkt noch nicht geschrieben, und er meldet fälschlich «kein bestandener Review».

**Nach dem Push die CI abwarten** und das Ergebnis Marc melden – lokal grün heisst nicht CI
grün (andere SDK-Version, Linux):

```bash
gh run list --limit 3                     # die neuen Läufe
gh run watch <id> --exit-status           # warten; rot → gh run view <id> --log-failed
```

Rot ist sofort zu beheben oder Marc vorzulegen – nie stehen lassen.

Der Vermerk gilt nur für diesen Commit. Der Hook `.claude/hooks/require-review.sh` **blockiert
nicht**: Pusht Claude einen Stand ohne Vermerk, erscheint eine Erinnerung bei Marc und bei Claude.
Claude weist Marc dann darauf hin und holt den Review nach. Will Marc trotz Blockern pushen,
entscheidet er das ausdrücklich im Gespräch – Claude pusht nie still über Blocker hinweg.

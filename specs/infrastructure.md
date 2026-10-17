# Infrastruktur und Deployment

Ziel: Betrieb unter **CHF 20 pro Monat**. Region: **Switzerland North**.

## Azure-Ressourcen

| Ressource | Name (Vorschlag) | SKU | Bemerkung |
|---|---|---|---|
| Resource Group | `rg-tauchplaetze-prod` | – | |
| App Service Plan | `asp-tauchplaetze-prod` | Linux, Basic B1 | eine Instanz, «Always On» für den Worker |
| Web App | `app-tauchplaetze-prod` | .NET 10 | System-assigned Managed Identity |
| SQL Server (logisch) | `sql-tauchplaetze-prod` | – | nur Entra-Authentifizierung |
| SQL Database | `sqldb-tauchplaetze` | General Purpose Serverless, **Free Offer** | siehe unten |
| Storage Account | `sttauchplaetzeprod` | Standard LRS | Container `media`, `forecast-raw`, `models`, `locks` |
| Log Analytics + Application Insights | `appi-tauchplaetze-prod` | Pay-as-you-go | tägliches Limit setzen, Sampling aktiv |

Ausserhalb von Azure:

| Dienst | Zweck | Kosten |
|---|---|---|
| Cloudflare (Free) | DNS, Proxy, CDN, Redirect Rules für `sichtweiten.ch` und `www` | 0 |
| GitHub | Repository, Actions | 0 (privat, freies Kontingent) |
| Domains | bereits im Besitz von Marc | bestehend |

## Azure SQL Free Offer

Quelle: Microsoft Learn, «Azure SQL Database free offer».

- Pro Datenbank und Monat: **100'000 vCore-Sekunden** Serverless-Compute, **32 GB** Daten, 32 GB Backup.
- Bis zu 10 Datenbanken pro Subscription. Gilt für die Lebensdauer der Subscription. Die Menge erneuert sich jeden Monat.
- Unabhängig vom Subscription-Typ. Ausgeschlossen ist nur «Azure for Students Starter».
- Erstellen: Azure SQL Hub (`aka.ms/azuresqlhub`) → «Create a database» → **«Start free»**. Das Banner «Free offer applied!» muss erscheinen. Die Kostenschätzung muss null zeigen.
- **Region:** Die erste Gratis-Datenbank legt die Region für alle weiteren Gratis-Datenbanken der Subscription fest. Darum gleich `Switzerland North` wählen.
- **Verhalten am Limit** (Einstellung «Behavior when free limit reached»):
  - «Auto-pause the database until next month»: Datenbank pausiert bis Monatsende. Point-in-time Restore 7 Tage, nur LRS-Backup.
  - «Continue using database for additional charges»: läuft weiter, Mehrverbrauch zu normalen Serverless-Preisen. **Nicht umkehrbar.**
- **Start:** Auto-Pause-Variante. Alert auf die Metrik «Free amount remaining» bei unter 10'000 vCore-Sekunden. Zeigt der Verbrauch, dass das Limit eng wird: auf «Continue» umstellen.
- Output Cache und tägliche Vorberechnung halten den Verbrauch tief. Nach einer Pause dauert die erste Anfrage spürbar länger. Der Cache überbrückt das.
- Offene Datenbankverbindungen (z.B. ein offenes Query-Tool) verhindern Auto-Pause. Nach der Arbeit trennen.
- Ob das Angebot GA oder Preview ist, steht nicht auf der Seite. In der FAQ prüfen.

## Blob Storage

| Container | Inhalt | Zugriff |
|---|---|---|
| `media` | Originale und Bildgrössen | öffentlich lesbar über Cloudflare oder über Endpunkt der App (Entscheid in `open-questions.md`) |
| `forecast-raw` | Rohdaten der Läufe als JSON | privat |
| `models` | ML-Modelle (Phase 2) | privat |
| `locks` | Blob Leases für Job-Sperren | privat |

Lifecycle-Regel: `forecast-raw` nach 90 Tagen in die Cool-Stufe.

## Kosten (Schätzung, vor dem Start im Azure Pricing Calculator prüfen)

| Posten | ca. pro Monat |
|---|---|
| App Service B1 Linux | CHF 10–15 |
| Azure SQL Free Offer | CHF 0 |
| Blob Storage (einige GB) | unter CHF 1 |
| Application Insights (mit Limit) | CHF 0–3 |
| Cloudflare, GitHub | CHF 0 |
| **Total** | **ca. CHF 11–19** |

Nicht verwenden: Azure Front Door (allein teurer als das ganze Budget), Cosmos DB (unnötig), Premium-SKUs.

## Umgebungen

- **Lokal:** Aspire (`Tauchplaetze.AppHost`) startet SQL Server und Azurite im Container.
- **PROD:** einzige Cloud-Umgebung (wie feldnah). Keine Test-Umgebung in Azure. Ein Health Check nach dem Deploy ist das Sicherheitsnetz.

## IaC

- Azure Bicep in `/deploy` (wie feldnah).
- Infra-Deploy manuell über `workflow_dispatch`.

## CI/CD (GitHub Actions)

| Workflow | Auslöser | Schritte |
|---|---|---|
| `ci.yml` | Pull Request | Restore, Build (`TreatWarningsAsErrors`), Tests (inkl. Testcontainers), Prüfung der `.resx`-Vollständigkeit |
| `deploy.yml` | Push auf `main` | Build, Tests, Publish, Deploy auf App Service (OIDC, kein Secret), EF-Migrationen als Bundle, Health Check |
| `infra.yml` | manuell | Bicep Deploy |

- Anmeldung bei Azure über OpenID Connect (Federated Credential). Keine Secrets im Repository.
- EF-Migrationen: `dotnet ef migrations bundle`, Ausführung im Deploy vor dem Slot-Wechsel bzw. vor dem Neustart.

## Betrieb

- Health Checks: `/health` (App), `/health/ready` (DB erreichbar, letzter Prognose-Lauf jünger als 30 h).
- Alerts: Health Check fehlgeschlagen, Prognose-Lauf `Failed`, Free-Offer-Kontingent unter 10 %.
- Backups: Point-in-time Restore der Datenbank (7 Tage). Medien zusätzlich lokal bei Marc (Originale).

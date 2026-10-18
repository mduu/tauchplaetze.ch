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
| Log Analytics + Application Insights | `appi-tauchplaetze-prod` | Pay-as-you-go | tägliches Limit setzen, Sampling aktiv, Aufbewahrung 30 Tage, IP-Adressen nicht speichern (Standard von Application Insights, nicht ändern) |

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
| `media` | Bildgrössen als WebP ohne Metadaten (ADR-009), `Cache-Control: immutable` | öffentlich lesbar, Cloudflare davor (Entscheid 17.10.2026: einfacher und günstiger als ein Endpunkt der App) |
| `media-pending` | Hochgeladene Fotos bis zur Freigabe, dazu immer das Original (mit EXIF) | privat |
| `revisions` | Inhalt aller Versionen der Plätze als JSON (`{diveSiteId}/{number}.json`) | privat |
| `forecast-raw` | Rohdaten der Läufe als JSON | privat |
| `models` | ML-Modelle (Phase 2) | privat |
| `locks` | Blob Leases für Job-Sperren | privat |

Lifecycle-Regel: `forecast-raw` nach 90 Tagen in die Cool-Stufe. `revisions` bleibt in der Hot-Stufe (kleine Dateien, selten gelesen, aber die History soll schnell laden).

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

- **Lokal:** Aspire (`Tauchplaetze.AppHost`) startet SQL Server und Azurite im Container. Beide behalten ihre Daten in einem Volume. Die Web-App migriert die Datenbank beim Start und legt fehlende Stammdaten an (`Database__PrepareOnStartup`).
- **Admins lokal:** `Accounts:Admins` als User Secret der Web-App, z.B. `dotnet user-secrets set "Accounts:Admins:0:Email" "…"` und `…:0:DisplayName`. Ohne Admin legt der Start keine Plätze an.
- **Speicher für Docker:** SQL Server braucht rund 2 GB im Container. Laufen SQL Server anderer Projekte, stirbt der Container mit Fehler 17138 (17.10.2026). Dann andere SQL-Container stoppen oder Docker mehr Speicher geben.
- **Bild-URLs:** `Media:PublicBaseUri`, die Adresse vor dem Container `media` (in Prod Cloudflare). Ohne Wert gilt die Adresse des Containers, lokal Azurite.
- **Container für Fotos:** Lokal legt der Core `media-pending` und `media` einmal pro Prozess an, `media` mit öffentlichem Lesezugriff auf Blobs. In Prod legt das Deployment `media` an: Storage Account mit `AllowBlobPublicAccess = true`, Container mit Zugriff «Blob». Ohne diese Einstellung scheitert die Freigabe von Fotos.
- **Fotos, Kosten und Missbrauch:** Ein Foto belegt das Original (bis 25 MB, privat) und vier WebP-Grössen (zusammen etwa 1 MB). 1000 Fotos sind rund 5–25 GB, auf Standard LRS etwa CHF 0.10–0.50 im Monat. Grenzen gegen Missbrauch: 30 Uploads pro Stunde und 100 pro Tag und Konto, 25 MB und 50 Megapixel pro Datei, ein Bild gleichzeitig in Bearbeitung. Öffentlich wird ein Foto erst mit der Freigabe einer Version durch die Moderation. Private, nie verwendete Fotos bleiben vorerst liegen (`open-questions.md`).
- **Betreiber für Impressum und Kontakt:** `Operator:Name` und `Operator:Address` (Zeilen mit `\n`) stehen im Impressum. `Operator:Email` ist nur der Empfänger des Kontaktformulars und erscheint nie auf der Website. Name und Adresse stehen in `appsettings.json` (Stand 18.10.2026: Marc Dürst, Privatperson). Die E-Mail steht nie in einer Datei im Repository: lokal als User Secret der Web-App, in Prod als App Setting bzw. Secret der App (`Operator__Email`). Fehlt eine Angabe, zeigt das Impressum «Angaben folgen» bzw. das Formular einen Fehler, und der Start schreibt eine Warnung ins Log. Vor dem Go-Live vollständig.
- **Rate Limit Kontaktformular:** `Contact:MessagesPerHour` (Standard 5) pro Stunde und Client, nur im Arbeitsspeicher der App (bei mehreren Instanzen pro Instanz), IPv6 auf das /64-Netz gekürzt. `CF-Connecting-IP` zählt nur mit `Contact:TrustCloudflareHeader = true`. In Prod: Ingress der App auf die IP-Bereiche von Cloudflare beschränken (Access Restrictions) und erst dann den Schalter setzen. Ohne Beschränkung könnte jede Anfrage den Header fälschen. Die Data-Protection-Schlüssel für die Zeitfalle müssen in Prod über Neustarts bestehen (Blob Storage), sonst sind offene Formulare nach einem Neustart «abgelaufen».
- **Links in Mails:** `PublicSite:BaseUri` (Standard `https://tauchplaetze.ch`). Lokal setzt Aspire die Adresse der Web-App. Die Links kommen nie aus dem Host-Header der Anfrage (Entscheid Marc, 18.10.2026).
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

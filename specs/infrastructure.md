# Infrastruktur und Deployment

Ziel: Betrieb unter **CHF 20 pro Monat**. Region: **Switzerland North** (Entscheid Marc, 20.10.2026; Rückfall West Europe). Serverless und das Free Offer sind dort verfügbar (geprüft 20.10.2026 mit `az sql db list-editions`).

Bicep: `deploy/main.bicep` (Subscription-Scope, legt die Resource Group an), Module in `deploy/modules/`, Werte in `deploy/main.bicepparam`. Schritte zum Go-live: `doc/go-live.md`.

## Azure-Ressourcen

| Ressource | Name (Vorschlag) | SKU | Bemerkung |
|---|---|---|---|
| Resource Group | `rg-tauchplaetze-prod` | – | |
| App Service Plan | `asp-tauchplaetze-prod` | Linux, Basic B1 | eine Instanz, «Always On» für den Worker |
| Web App | `app-tauchplaetze-prod` | .NET 10 | System-assigned Managed Identity |
| SQL Server (logisch) | `sql-tauchplaetze-prod` | – | nur Entra-Authentifizierung |
| SQL Database | `sqldb-tauchplaetze` | General Purpose Serverless, **Free Offer** | siehe unten |
| Storage Account | `sttauchplaetzeprod` | Standard LRS | Container `media`, `forecast-raw`, `models`, `locks` |
| Log Analytics + Application Insights | `log-tauchplaetze-prod`, `appi-tauchplaetze-prod` | Pay-as-you-go | Tageslimit 0.15 GB (unter dem Gratisvolumen von 5 GB im Monat), Sampling 50 % der Traces, Aufbewahrung 30 Tage, IP-Adressen nicht speichern (Standard von Application Insights, nicht ändern) |
| Key Vault | `kv-tauchplaetze-prod` | Standard, RBAC | Secrets der App, Key `dataprotection` |
| Action Group, Alerts, Budget | `ag-tauchplaetze-prod`, `alert-*`, `budget-tauchplaetze-prod` | – | nur mit `ALERT_EMAIL`, siehe «Betrieb» |

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

## Storage Queues

| Queue | Inhalt |
|---|---|
| `mail-jobs` | Mails, die auf den Versand warten (ADR-010) |
| `mail-jobs-deadletter` | Mails nach fünf Fehlversuchen oder einem nicht behebbaren Fehler, Ablauf nach 30 Tagen (`Mail:DeadLetterRetentionDays`, Datenschutz) |

- Im bestehenden Storage Account. Lokal legt Aspire die Ressource `queues` an (Azurite), die Queues entstehen beim ersten Zugriff.
- Kosten: Der Worker pollt alle 5 s, bei leerer Queue bis alle 30 s. Höchstens rund 520 000 Receive-Aufrufe pro Monat, das sind etwa 20 Rappen (Storage-Transaktionen um CHF 0.0004 pro 10 000).
- **Prod-Konfiguration:** Connection `queues` auf den Queue-Endpoint des Storage Accounts (`https://{account}.queue.core.windows.net`), Zugriff über Managed Identity der App mit der Rolle «Storage Queue Data Contributor». Kein Schlüssel in der Konfiguration.
- **SMTP:** `Mail:TimeoutSeconds` (Standard 20). Die Queue verarbeitet 4 Mails pro Durchgang mit 5 Minuten Sichtbarkeit (ADR-010).
- **Health-Check:** `/health/ready` prüft die Queue noch nicht. Offener Punkt #30.
- **Alert vor dem Go-Live:** Log-Suche auf «moved to deadletter» und «awaiting investigation» (Application Insights), Benachrichtigung an Marc.

Lifecycle-Regel: `forecast-raw` nach 90 Tagen in die Cool-Stufe. `revisions` bleibt in der Hot-Stufe (kleine Dateien, selten gelesen, aber die History soll schnell laden).

## Kosten (Listenpreise Switzerland North in CHF, Azure Retail Prices API, 20.10.2026)

| Posten | Preis | ca. pro Monat |
|---|---|---|
| App Service B1 Linux | 0.0165 pro Stunde | CHF 12.05 |
| Azure SQL Free Offer | 0 bis zum Kontingent | CHF 0 |
| oder Azure SQL Basic (5 DTU) | 0.1474 pro Tag | CHF 4.50 |
| Serverless über dem Kontingent (nur mit «Continue») | 0.5253 pro vCore-Stunde | bei dauernd 0.5 vCore rund CHF 190 |
| Blob Storage (einige GB), Storage Queues, Key Vault | | unter CHF 1 |
| Log Analytics mit Tageslimit 0.15 GB | im Gratisvolumen | CHF 0 |
| Alerts (2–3 Metrik-Alerts, 3 Log-Alerts stündlich) | | CHF 0.50–2 |
| Cloudflare, GitHub | | CHF 0 |
| **Total mit Free Offer** | | **ca. CHF 13–15** |
| **Total mit Basic** | | **ca. CHF 18–20** |

**Risiko Free Offer (20.10.2026):** 100 000 vCore-Sekunden sind bei 0.5 vCore rund 55 Stunden online im Monat. Jeder Zugriff hält die Datenbank mindestens 15 Minuten wach (`autoPauseDelay`). Der Abfluss-Job schreibt alle 30 Minuten, dazu kommen Besucher und Prognose-Läufe. Die Datenbank wäre so fast immer online, das Kontingent nach wenigen Tagen aufgebraucht. Mit «Auto-pause» ist die Datenbank dann bis Monatsende nicht erreichbar. Empfehlung: `sqlPlan = 'Basic'` in `main.bicepparam` (eine Zeile, Entscheid Marc offen).

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
- **Inhalt von Prod nach lokal (ADR-011):** In Prod das Secret `Content--ExportKey` im Key Vault (lang, zufällig, z.B. `openssl rand -base64 32`). Ohne Wert ist `/api/platform/content-export` aus (404). Lokal setzt Aspire `Content__ImportEnabled=true`. Für den Abruf als User Secrets der Web-App: `dotnet user-secrets set "Content:SourceBaseUri" "https://tauchplaetze.ch/"` und `dotnet user-secrets set "Content:ExportKey" "…"` (derselbe Wert wie in Prod). Ablauf: in Prod erfassen, lokal auf `/platform/inhalt` «Aus Prod übernehmen». In Prod `Content:ImportEnabled` nie setzen. Der Abruf geht nur über HTTPS. Das Rate Limit des Endpoints (10 pro Stunde) zählt den Client wie das Kontaktformular.
- **BAFU-Abflussvorhersage:** In Prod die Secrets `Hydro--Forecast--Username` und `Hydro--Forecast--Password` im Key Vault, lokal als User Secrets der Web-App (`Hydro:Forecast:Username`, `Hydro:Forecast:Password`), in einem eigenen Terminal gesetzt, nie im Chat. Ohne Werte holt der Abfluss-Job keine Vorhersage. `Hydro__Forecast__BaseUrl` nur über HTTPS.
- **PROD:** einzige Cloud-Umgebung (wie feldnah). Keine Test-Umgebung in Azure. Ein Health Check nach dem Deploy ist das Sicherheitsnetz.

## IaC

- Azure Bicep in `/deploy` (wie feldnah). Infra-Deploy manuell über `infra.yml` (`workflow_dispatch`: what-if, dann Deploy mit Freigabe im Environment `prod`).
- Persönliche Werte (Object ID des SQL-Admins, Adresse für Alerts) kommen aus Umgebungsvariablen (`readEnvironmentVariable` in `main.bicepparam`), in GitHub aus Variablen und Secrets.
- **Keine Secrets im Template.** Bicep legt keine Key-Vault-Secrets an, ein Deploy würde sonst gesetzte Werte überschreiben. Marc setzt sie einmal (`doc/go-live.md`).
- **Zugriff nur über Entra:** SQL nur mit Entra-Anmeldung, Storage ohne Schlüssel (`allowSharedKeyAccess: false`), Deploy ohne Basic-Auth für FTP und SCM. Die Managed Identity der App hat «Storage Blob Data Contributor», «Storage Queue Data Contributor», «Key Vault Secrets User» und «Key Vault Crypto User». Den SQL-Benutzer legt Marc per SQL an (`db_owner`, weil die App migriert).
- **App Settings (prod):** Connection Strings `tauchplaetze` (`Authentication=Active Directory Default`, Timeout 90 s wegen Serverless), `blobs` und `queues` als Endpoint-Adresse (Aspire nimmt dann die Managed Identity). `KeyVault__Uri`: Die App liest die Secrets beim Start selbst (`--` wird `:`), fehlende Secrets fehlen nur als Wert. `DataProtection__KeysBlobUri` (`dataprotection/keys.xml`) und `DataProtection__KeyVaultKeyUri`: Schlüssel der Data Protection in Blob, geschützt mit dem Key (offener Punkt #29 erledigt). `APPLICATIONINSIGHTS_CONNECTION_STRING`. `Database__PrepareOnStartup=true`. Mail über Hostpoint (ADR-012).
- **Secrets im Key Vault:** `Accounts--Admins--0--Email`, `Operator--Email`, `Mail--UserName`, `Mail--Password`, `Hydro--Forecast--Username`, `Hydro--Forecast--Password`, `Content--ExportKey`.
- **Entra-Anmeldung an SQL:** Ab SqlClient 7 braucht sie das Paket `Microsoft.Data.SqlClient.Extensions.Azure` (im Core referenziert).

## CI/CD (GitHub Actions)

| Workflow | Auslöser | Schritte |
|---|---|---|
| `ci.yml` | Push auf `main`, Pull Request | Restore, Build (`TreatWarningsAsErrors`), Tests (inkl. Testcontainers), Prüfung der `.resx`-Vollständigkeit |
| `deploy.yml` | grüne CI auf `main` (`workflow_run`), manuell | Publish, Deploy auf App Service (OIDC, kein Secret), Prüfung von `/alive` und `/de`. Läuft erst, wenn `AZURE_CLIENT_ID` gesetzt ist. |
| `infra.yml` | manuell | what-if (Zusammenfassung im Job), Deploy nach Freigabe |

- Anmeldung bei Azure über OpenID Connect (Federated Credential für `main` und das Environment `prod`). Keine Secrets im Repository.
- **EF-Migrationen beim Start der App** (Entscheid 20.10.2026, statt Bundle im Workflow): Der Start legt ohnehin Gewässer, Admins und Plätze aus dem Seed an (`Database:PrepareOnStartup`). Es gibt eine Instanz. Der Runner von GitHub braucht so keinen Zugang durch die Firewall von SQL. Scheitert eine Migration, startet die App nicht, und der Health Check im Workflow wird rot. Migrationen fügen nur hinzu (Review-Regel), eine ältere App läuft auf dem neueren Schema.

## Betrieb

- Health Checks: `/alive` und `/health` prüfen nur die App, **ohne Datenbank**. App Service fragt `/alive` jede Minute ab. Mit der Datenbank könnte Serverless nie pausieren. `/health/ready` prüft zusätzlich die Datenbank und einen brauchbaren Prognose-Lauf jünger als 30 h (`GetReadiness`). Er weckt die Datenbank, darum fragt ihn kein Dienst regelmässig ab.
- Alerts (`deploy/modules/alerts.bicep`, an `ALERT_EMAIL`): Health Check von App Service unter 100 %; Prognose-Lauf gescheitert; seit 30 h kein brauchbarer Lauf; Mail in der Dead-Letter-Queue (ADR-010); Free-Offer-Kontingent unter 10 000 vCore-Sekunden (nur mit `sqlPlan = 'Free'`). Die Log-Alerts suchen die Texte der Logs aus `ForecastWorker` und `ProcessMailQueue` in `AppTraces` und prüfen stündlich. Budget der Resource Group (Standard 25 im Monat): Mail bei 80 % und bei 100 % der Prognose.
- Backups: Point-in-time Restore der Datenbank (7 Tage). Medien zusätzlich lokal bei Marc (Originale).

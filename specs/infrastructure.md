# Infrastruktur und Deployment

Ziel: Betrieb unter **CHF 20 pro Monat**. Region: **Switzerland North** (Entscheid Marc, 20.10.2026; Rückfall West Europe). Serverless und das Free Offer sind dort verfügbar (geprüft 20.10.2026 mit `az sql db list-editions`).

Bicep: `deploy/main.bicep` (Resource-Group-Scope, die Resource Group legt Marc von Hand an), Module in `deploy/modules/`, Werte in `deploy/main.bicepparam`. Schritte zum Go-live: `doc/go-live.md`.

## Azure-Ressourcen

| Ressource | Name (Vorschlag) | SKU | Bemerkung |
|---|---|---|---|
| Resource Group | `rg-tauchplaetze-prod` | – | |
| App Service Plan | `asp-tauchplaetze-prod` | Linux, Basic B1 | eine Instanz, «Always On» für den Worker |
| Web App | `app-tauchplaetze-prod` | .NET 10 | System-assigned Managed Identity |
| SQL Server (logisch) | `sql-tauchplaetze-prod` | – | nur Entra-Authentifizierung |
| SQL Database | `sqldb-tauchplaetze` | **Basic, 5 DTU, 2 GB** (Entscheid Marc, 20.10.2026); Free Offer als Option `sqlPlan = 'Free'` | siehe unten |
| Storage Account | `sttauchplaetzeprod` | Standard LRS | Container `media`, `forecast-raw`, `models`, `locks` |
| Log Analytics + Application Insights | `log-tauchplaetze-prod`, `appi-tauchplaetze-prod` | Pay-as-you-go | Tageslimit 0.15 GB (unter dem Gratisvolumen von 5 GB im Monat), Sampling höchstens 1 Trace pro Sekunde, Aufbewahrung 30 Tage, IP-Adressen nicht speichern (Standard von Application Insights, nicht ändern) |
| Key Vault | `kv-tauchplaetze-prod` | Standard, RBAC | Secrets der App, als Key-Vault-Referenzen in den App Settings |
| Action Group, Alerts, Budget | `ag-tauchplaetze-prod`, `alert-*`, `budget-tauchplaetze-prod` | – | nur mit `ALERT_EMAIL`, siehe «Betrieb» |
| Dashboard, Workbook | `dash-tauchplaetze-prod`, «tauchplaetze.ch – Betrieb und Nutzung» | – | kostenlos, siehe «Betrieb» |

Ausserhalb von Azure:

| Dienst | Zweck | Kosten |
|---|---|---|
| DNS bei Hostpoint | A-Record auf App Service, CNAME `www`, Mail (MX, SPF, DKIM, DMARC). Entscheid Marc, 21.10.2026: kein Cloudflare vorerst, Weiterleitungen in der App (`seo.md`), App Service Managed Certificates | bestehend |
| Cloudflare (Free), später | Proxy, CDN für Fotos, `restrictToCloudflare` (`doc/go-live.md`, 9b) | 0 |
| GitHub | Repository, Actions | 0 (privat, freies Kontingent) |
| Domains | bereits im Besitz von Marc | bestehend |

## Azure SQL Free Offer

Stand 21.10.2026: Prod nutzt **Basic** (5 DTU, 2 GB, ADR-003, Nachtrag). Der Abschnitt beschreibt die Option Free Offer (`sqlPlan = 'Free'`).

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
- Der Output Cache und die tägliche Vorberechnung würden den Verbrauch tief halten. Nach einer Pause dauert die erste Anfrage spürbar länger. Der Cache überbrückt das.
- Offene Datenbankverbindungen (z.B. ein offenes Query-Tool) verhindern Auto-Pause. Nach der Arbeit trennen.
- Ob das Angebot GA oder Preview ist, steht nicht auf der Seite. In der FAQ prüfen.

## Blob Storage

| Container | Inhalt | Zugriff |
|---|---|---|
| `media` | Bildgrössen als WebP ohne Metadaten (ADR-009), `Cache-Control: immutable` | öffentlich lesbar. Cloudflare davor ist ein späterer Ausbau (Entscheid 17.10.2026: einfacher und günstiger als ein Endpunkt der App; DNS vorerst bei Hostpoint, 21.10.2026) |
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
| **Total mit Basic (Standard)** | | **ca. CHF 18–20** |
| Total mit Free Offer | | ca. CHF 13–15 |

**Risiko Free Offer (20.10.2026):** 100 000 vCore-Sekunden sind bei 0.5 vCore rund 55 Stunden online im Monat. Jeder Zugriff hält die Datenbank mindestens 15 Minuten wach (`autoPauseDelay`). Der Abfluss-Job schreibt alle 30 Minuten, dazu kommen Besucher und Prognose-Läufe. Die Datenbank wäre so fast immer online, das Kontingent nach wenigen Tagen aufgebraucht. Mit «Auto-pause» ist die Datenbank dann bis Monatsende nicht erreichbar. Darum Basic als Standard (Entscheid Marc, 20.10.2026).

Nicht verwenden: Azure Front Door (allein teurer als das ganze Budget), Cosmos DB (unnötig), Premium-SKUs.

## Umgebungen

- **Lokal:** Aspire (`Tauchplaetze.AppHost`) startet SQL Server und Azurite im Container. Beide behalten ihre Daten in einem Volume. Die Web-App migriert die Datenbank beim Start und legt fehlende Stammdaten an (`Database__PrepareOnStartup`).
- **Admins lokal:** `Accounts:Admins` als User Secret der Web-App, z.B. `dotnet user-secrets set "Accounts:Admins:0:Email" "…"` und `…:0:DisplayName`. Ohne Admin legt der Start keine Plätze an.
- **Speicher für Docker:** SQL Server braucht rund 2 GB im Container. Laufen SQL Server anderer Projekte, stirbt der Container mit Fehler 17138 (17.10.2026). Dann andere SQL-Container stoppen oder Docker mehr Speicher geben.
- **Bild-URLs:** `Media:PublicBaseUri`, die Adresse vor dem Container `media` (später Cloudflare, heute leer). Ohne Wert gilt die Adresse des Containers, lokal Azurite.
- **Container für Fotos:** Lokal legt der Core `media-pending` und `media` einmal pro Prozess an, `media` mit öffentlichem Lesezugriff auf Blobs. In Prod legt das Deployment `media` an: Storage Account mit `AllowBlobPublicAccess = true`, Container mit Zugriff «Blob». Ohne diese Einstellung scheitert die Freigabe von Fotos.
- **Fotos, Kosten und Missbrauch:** Ein Foto belegt das Original (bis 25 MB, privat) und vier WebP-Grössen (zusammen etwa 1 MB). 1000 Fotos sind rund 5–25 GB, auf Standard LRS etwa CHF 0.10–0.50 im Monat. Grenzen gegen Missbrauch: 30 Uploads pro Stunde und 100 pro Tag und Konto, 25 MB und 50 Megapixel pro Datei, ein Bild gleichzeitig in Bearbeitung. Öffentlich wird ein Foto erst mit der Freigabe einer Version durch die Moderation. Private, nie verwendete Fotos bleiben vorerst liegen (`open-questions.md`).
- **Betreiber für Impressum und Kontakt:** `Operator:Name` und `Operator:Address` (Zeilen mit `\n`) stehen im Impressum. `Operator:Email` ist nur der Empfänger des Kontaktformulars und erscheint nie auf der Website. Name und Adresse stehen in `appsettings.json` (Stand 18.10.2026: Marc Dürst, Privatperson). Die E-Mail steht nie in einer Datei im Repository: lokal als User Secret der Web-App, in Prod als App Setting bzw. Secret der App (`Operator__Email`). Fehlt eine Angabe, zeigt das Impressum «Angaben folgen» bzw. das Formular einen Fehler, und der Start schreibt eine Warnung ins Log. Vor dem Go-Live vollständig.
- **Rate Limit Seiten:** Gegen Scraper, die die Datenbank vollaufen lassen. Pro Client und ohne Anmeldung ein Token Bucket: `Pages:RateLimit:Burst` (Standard 120) Seiten auf einmal, danach `Pages:RateLimit:PerMinute` (Standard 60) pro Minute. Ein Mensch erreicht das nicht, auch nicht mit vielen Tabs. Dazu über alle Clients höchstens `Pages:RateLimit:Concurrent` (Standard 4) Seiten gleichzeitig, weitere warten in einer Queue (`QueueLimit` 200). Abgewiesen: 429 mit `Retry-After: 60`, Googlebot drosselt dann. Zählt nur Seiten (Razor Components), die nicht aus dem Output Cache kommen, keine statischen Dateien, Health Checks oder Endpoints. Client wie beim Kontaktformular. Die 429 zeigt keine Status-Code-Seite.
- **Rate Limit Meldungen:** «Tauchgang melden» höchstens `Reports:RateLimit:PerHour` (Standard 20) POST pro Stunde und Client, zusätzlich zur Grenze von 10 Meldungen pro Konto und Tag im Core. Abgewiesen leitet auf dieselbe Seite mit `limit=1`.
- **Rate Limit Konto-Formulare:** Anmeldung höchstens `Accounts:RateLimit:SignInPerHour` (Standard 20), Registrierung, «Passwort vergessen» und «Neues Passwort» zusammen `Accounts:RateLimit:FormsPerHour` (Standard 10) pro Stunde und Client. Nur POST zählt. Abgewiesen leitet auf dieselbe Seite mit `limit=1` und einem Hinweis. Zählt im Arbeitsspeicher, Client wie beim Kontaktformular.
- **Rate Limit Kontaktformular:** `Contact:MessagesPerHour` (Standard 5) pro Stunde und Client, nur im Arbeitsspeicher der App (bei mehreren Instanzen pro Instanz), IPv6 auf das /64-Netz gekürzt. `CF-Connecting-IP` zählt nur mit `Contact:TrustCloudflareHeader = true`. In Prod setzt `restrictToCloudflare` (Bicep) beides zusammen: Access Restrictions auf die IP-Bereiche von Cloudflare (Default Deny, SCM-Site offen) und den Schalter. Ohne Beschränkung könnte jede Anfrage den Header fälschen. Die Data-Protection-Schlüssel für die Zeitfalle bestehen in Prod über Neustarts, weil App Service sie unter `$HOME` ablegt (siehe «Data Protection»).
- **Links in Mails:** `PublicSite:BaseUri` (Standard `https://tauchplaetze.ch`). Lokal setzt Aspire die Adresse der Web-App. Die Links kommen nie aus dem Host-Header der Anfrage (Entscheid Marc, 18.10.2026).
- **Inhalt von Prod nach lokal (ADR-011):** In Prod das Secret `Content--ExportKey` im Key Vault (lang, zufällig, z.B. `openssl rand -base64 32`). Ohne Wert ist `/api/platform/content-export` aus (404). Lokal setzt Aspire `Content__ImportEnabled=true`. Für den Abruf als User Secrets der Web-App: `dotnet user-secrets set "Content:SourceBaseUri" "https://tauchplaetze.ch/"` und `dotnet user-secrets set "Content:ExportKey" "…"` (derselbe Wert wie in Prod). Ablauf: in Prod erfassen, lokal auf `/platform/inhalt` «Aus Prod übernehmen». In Prod `Content:ImportEnabled` nie setzen. Der Abruf geht nur über HTTPS. Das Rate Limit des Endpoints (10 pro Stunde) zählt den Client wie das Kontaktformular.
- **Automatisch übersetzen (`cms.md`):** In Prod das Secret `Translation--ApiKey` im Key Vault: ein Schlüssel der Claude API aus einem eigenen Workspace mit Spend-Limit (z.B. 10 USD pro Monat). Lokal optional als User Secret der Web-App (`Translation:ApiKey`). Ohne Wert fehlt der Button. Das Secret vor dem nächsten Infra-Deploy setzen, sonst startet die App nicht.
- **BAFU-Abflussvorhersage:** In Prod die Secrets `Hydro--Forecast--Username` und `Hydro--Forecast--Password` im Key Vault, lokal als User Secrets der Web-App (`Hydro:Forecast:Username`, `Hydro:Forecast:Password`), in einem eigenen Terminal gesetzt, nie im Chat. Ohne Werte holt der Abfluss-Job keine Vorhersage. `Hydro__Forecast__BaseUrl` nur über HTTPS.
- **Run from Package:** `WEBSITE_RUN_FROM_PACKAGE=1`. App Service startet die App aus dem ZIP des Deploys, `wwwroot` ist schreibgeschützt. Ohne das überschrieb der Deploy die DLLs der laufenden App, und `/de` lieferte rund 2 Minuten lang 500 (`Invalid assembly public key`, 21.10.2026). Die App schreibt nichts nach `wwwroot`. Die Data Protection Keys liegen unter `$HOME/ASP.NET`. Der Deploy meldet Erfolg, bevor die neue Version läuft (am 21.10.2026 rund 2½ Minuten später). Darum nennt `/alive` den Commit im Header `X-Build` (`SourceRevisionId` beim Publish), und `deploy.yml` wartet bis zu 10 Minuten, bis der neue Commit antwortet. Kommt er nicht, ist der Lauf rot («Neue Version läuft nicht»).
- **Warm-up:** `WEBSITE_WARMUP_PATH=/de`. Nach einem Start fragt App Service zuerst die Startseite ab, so trifft der Kaltstart keinen Besucher. In den ersten 5 Minuten nach einem Start lag P95 sonst bei 2.2 s statt 0.8 s (22.10.2026).
- **Startzeit:** `WEBSITES_CONTAINER_START_TIME_LIMIT=600`. Der erste Start migriert die leere Datenbank und legt die Stammdaten an, das dauerte am 20.10.2026 länger als die Standardgrenze von 230 s. Danach startet die App in rund 90 s. `Azure.Identity` loggt erst ab Warning.
- **PROD:** einzige Cloud-Umgebung (wie feldnah). Keine Test-Umgebung in Azure. Ein Health Check nach dem Deploy ist das Sicherheitsnetz.

## IaC

- Azure Bicep in `/deploy` (wie feldnah), **Resource-Group-Scope**. Infra-Deploy manuell über `infra.yml` (`workflow_dispatch`): Standard what-if (Environment `prod-app`), Deploy erst nach Freigabe durch den Required Reviewer (Marc) im Environment `prod`.
- **Environments in GitHub** (Entscheid Marc, 20.10.2026): `prod-app` ohne Freigabe für den App-Deploy und das what-if, `prod` mit Marc als Required Reviewer für den Infra-Deploy. Beide nur vom Branch `main`. OIDC mit je einer Federated Credential pro Environment, keine für den Branch.
- **Key Vault mit Purge Protection:** Gelöschte Secrets lassen sich 90 Tage nicht endgültig löschen und bleiben wiederherstellbar. Kostet nichts. Nicht umkehrbar: Der Key Vault lässt sich danach nicht mehr ohne diese Frist entfernen.
- **Von Hand, nicht im Template** (Entscheid Marc, 20.10.2026): die Resource Group, die Rollen der Managed Identity und der SQL-Benutzer der App (`doc/go-live.md`). Der GitHub-Service-Principal hat nur Contributor auf der Resource Group und vergibt keine Rechte.
- Persönliche Werte (Object ID des SQL-Admins, Adresse für Alerts) kommen aus Umgebungsvariablen (`readEnvironmentVariable` in `main.bicepparam`), in GitHub aus Secrets (Logs eines öffentlichen Repositorys sind öffentlich, Variablen sind nicht maskiert). Vor dem ersten Infra-Deploy überspringt `deploy.yml` Deploy und Health, weil die Web App fehlt.
- **Keine Secrets im Template.** Bicep legt keine Key-Vault-Secrets an, ein Deploy würde sonst gesetzte Werte überschreiben. Marc setzt sie einmal.
- **Zugriff nur über Entra:** SQL nur mit Entra-Anmeldung, Storage ohne Schlüssel (`allowSharedKeyAccess: false`), Deploy ohne Basic-Auth für FTP und SCM. Rollen der Managed Identity: «Storage Blob Data Contributor» und «Storage Queue Data Contributor» auf dem Storage Account, «Key Vault Secrets User» auf dem Key Vault. SQL-Benutzer per SQL mit `db_owner`, weil die App migriert.
- **App Settings (prod):** Connection Strings `tauchplaetze` (`Authentication=Active Directory Default`, Timeout 90 s), `blobs` und `queues` als Endpoint-Adresse (Aspire nimmt dann die Managed Identity). `APPLICATIONINSIGHTS_CONNECTION_STRING`. `Database__PrepareOnStartup=true`. Mail-Host, Port und TLS (ADR-012).
- **Secrets als Key-Vault-Referenzen** (`@Microsoft.KeyVault(VaultName=kv-tauchplaetze-prod;SecretName=…)`, `keyVaultReferenceIdentity: SystemAssigned`): `Accounts--Admins--0--Email`, `Operator--Email`, `Mail--UserName`, `Mail--Password`, `Hydro--Forecast--Username`, `Hydro--Forecast--Password`, `Content--ExportKey`, `Translation--ApiKey`. App Service löst sie auf. Fehlt ein Secret oder die Rolle, bleibt der Text stehen: Der Start bricht dann ab und nennt nur den Key (`KeyVaultReferences`).
- **Data Protection ohne eigene Konfiguration** (wie feldnah, Entscheid Marc, 20.10.2026): App Service legt die Schlüssel automatisch unter `$HOME/ASP.NET/DataProtection-Keys` ab. Vertretbar, weil es eine Instanz gibt. Die Schlüssel liegen unverschlüsselt im Dateispeicher der App. Wird die Web App neu angelegt, sind sie weg: Anmeldungen und Links in offenen Mails werden dann ungültig (offener Punkt #29 erledigt).
- **Budget** auf der Resource Group (`deploy/modules/budget.bicep`).
- **Entra-Anmeldung an SQL:** Ab SqlClient 7 braucht sie das Paket `Microsoft.Data.SqlClient.Extensions.Azure` (im Core referenziert).

## CI/CD (GitHub Actions)

| Workflow | Auslöser | Schritte |
|---|---|---|
| `ci.yml` | Push auf `main`, Pull Request | Restore, Build (`TreatWarningsAsErrors`), Tests (inkl. Testcontainers), Prüfung der `.resx`-Vollständigkeit |
| `deploy.yml` | grüne CI eines Pushes auf `main` in diesem Repository (`workflow_run`), manuell | Publish, Deploy auf App Service (OIDC, Environment `prod-app` ohne Freigabe), Prüfung von `/alive` und `/de`. Läuft erst, wenn `AZURE_CLIENT_ID` gesetzt ist. `workflow_run` mit `branches: [main]` prüft nur den Namen: Der Job prüft darum zusätzlich Ereignis `push` und das eigene Repository, sonst könnte ein Pull Request aus einem Fork mit einem Branch «main» deployen. Actions sind auf Commit-SHAs gepinnt. |
| `infra.yml` | manuell | what-if (Zusammenfassung im Job), Deploy nach Freigabe |

- Anmeldung bei Azure über OpenID Connect (Federated Credential für `main` und das Environment `prod`). Keine Secrets im Repository.
- **SQL-Firewall «Azure-Dienste» (0.0.0.0), bewusst offen** (Security-Review 20.10.2026): Die Regel lässt alle Azure-Dienste zum SQL Server, nicht nur unsere App. Die Anmeldung geht nur über Entra (keine Passwörter), das hält Angriffe fern. Eine Regel auf die Outbound-IPs der App wäre enger, doch diese ändern sich bei einem Wechsel des Plans, und ein vergessener Nachtrag nimmt die Website offline. Ohne Auditing und Defender (Kosten).
- **Blob Soft Delete 14 Tage** für Blobs und Container. Ein Fehlgriff oder ein falscher Deploy löscht sonst Revisionen und Fotos endgültig.
- **EF-Migrationen beim Start der App** (Entscheid 20.10.2026, statt Bundle im Workflow): Der Start legt ohnehin Gewässer, Admins und Plätze aus dem Seed an (`Database:PrepareOnStartup`). Es gibt eine Instanz. Der Runner von GitHub braucht so keinen Zugang durch die Firewall von SQL. Scheitert eine Migration, startet die App nicht, und der Health Check im Workflow wird rot. Migrationen fügen nur hinzu (Review-Regel), eine ältere App läuft auf dem neueren Schema.

## Betrieb

- Health Checks: `/alive` und `/health` prüfen nur die App, **ohne Datenbank**. App Service fragt `/alive` jede Minute ab, eine Abfrage der Datenbank jede Minute wäre unnötige Last (und mit der Option Free liesse sie Serverless nie pausieren). `/health/ready` prüft zusätzlich die Datenbank und einen brauchbaren Prognose-Lauf jünger als 30 h (`GetReadiness`). Der Endpoint ist öffentlich. Das Ergebnis gilt 30 s, so fragen viele Aufrufe die Datenbank höchstens zweimal pro Minute.
- Alerts (`deploy/modules/alerts.bicep`, an `ALERT_EMAIL`): Health Check von App Service unter 100 %; Prognose-Lauf gescheitert; seit 30 h kein brauchbarer Lauf; Mail in der Dead-Letter-Queue (ADR-010); Tageslimit der Logs erreicht (`_LogOperation`); Free-Offer-Kontingent unter 10 000 vCore-Sekunden (nur mit `sqlPlan = 'Free'`). Die Log-Alerts suchen die Texte der Logs aus `ForecastWorker` und `ProcessMailQueue` in `AppTraces` und prüfen stündlich. Budget der Resource Group (Standard 25 im Monat): Mail bei 80 % und bei 100 % der Prognose.
- Telemetrie (`Tauchplaetze.ServiceDefaults`): Requests, Exceptions, Logs ab `Information` und ausgehende Aufrufe (Dependencies) gehen an Application Insights, nur mit `APPLICATIONINSIGHTS_CONNECTION_STRING`. Health Checks, statische Dateien (`/_framework`, `/_content`, `/bathymetry`, CSS, JS, Fonts, Bilder) und `/_blazor` erzeugen keinen Trace. Sampling: höchstens 1 Trace pro Sekunde (`TracesPerSecond`). Darunter bleibt jeder Request erhalten, `ItemCount` rechnet gesampelte Requests hoch. Die Werte der Query-Parameter ersetzt OpenTelemetry durch `Redacted`. Seitenaufrufe zählen wir auf dem Server, kein Skript im Browser.
- Abfragen in Log Analytics (`log-tauchplaetze-prod`, Logs):
  - Seitenaufrufe pro Tag: `AppRequests | where Name startswith "GET" and ResultCode == "200" and Url !has "/admin" and Url !has "/platform" | summarize Aufrufe = sum(ItemCount) by bin(TimeGenerated, 1d)`
  - Fehler: `AppRequests | where Success == false | summarize sum(ItemCount) by Name, ResultCode` und `AppExceptions | summarize count() by ProblemId, OuterMessage`
  - Volumen pro Tabelle (Tageslimit 0.15 GB): `union withsource = Tabelle App* | where TimeGenerated > ago(1d) | summarize MB = sum(_BilledSize) / 1e6 by Tabelle`
- **Dashboard und Workbook** (`deploy/modules/dashboard.bicep`, `deploy/modules/workbook.bicep`, Abfragen in `deploy/workbooks/operations.json`): Das Dashboard im Portal ist der Einstieg mit Kennzahlen, Prognose-Läufen, Abfluss-Job, DB, CPU, Menschen und Bots, meistbesuchten Plätzen und Gewässern. Das Workbook zeigt die Details in fünf Tabs: Betrieb, Jobs, Datenbank, Kosten (Cost Management, Budget, Log-Volumen), Nutzung. Die Dauer eines Prognose-Laufs folgt aus der RunId (UUID v7) und dem Log am Ende.
- **Menschen und Bots** (Entscheid Marc, 23.10.2026): ohne Cookies, ohne Browser-SDK, ohne Region. Gezählt werden Seitenaufrufe mit Status 200, keine Personen. Bot heisst: Application Insights erkennt ihn (`SyntheticSource`), der Client ist ein Skript, das System ist veraltet (Android bis 7, iOS bis 14) oder der Browser liegt mehr als 12 Versionen hinter dem neuesten. Ein iPad zählt als Desktop.
- **Tageslimit der Logs:** Reset um 14:00 UTC. Am 22.10.2026 griff das Limit um 03:17 Uhr, bis 16:00 Uhr fehlten alle Logs, auch der Prognose-Lauf. Den grössten Teil machen `AppMetrics` und `AppDependencies` aus.
- Backups: Point-in-time Restore der Datenbank (7 Tage). Medien zusätzlich lokal bei Marc (Originale).

# Go-live: Schritte für Marc

Stand 20.10.2026. Die Infrastruktur steht in `deploy/` (Bicep), die Workflows in `.github/workflows/` (`infra.yml`, `deploy.yml`). Grundlage: `specs/infrastructure.md`.

Geheimnisse (Passwörter, Schlüssel, E-Mail-Adressen) **immer in einem eigenen Terminal** setzen, nie mit `!` im Chat mit Claude: Dort landen Befehl und Ausgabe im Verlauf.

## 0. Entscheid: Datenbank

`deploy/main.bicepparam` hat `sqlPlan = 'Free'` (Spec, ADR-003). Vor dem ersten Deploy prüfen:

- Das Free Offer gibt 100 000 vCore-Sekunden im Monat. Bei 0.5 vCore ist die Datenbank damit rund **55 Stunden im Monat online**. Jeder Zugriff hält sie mindestens 15 Minuten wach.
- Der Abfluss-Job schreibt alle 30 Minuten, dazu kommen die Besucher. Die Datenbank ist so fast immer online. Das Kontingent ist nach wenigen Tagen weg. Mit «Auto-pause» ist die Website dann bis Monatsende **ohne Datenbank**.
- **Empfehlung:** `sqlPlan = 'Basic'` (5 DTU, 2 GB, pausiert nie, rund CHF 4.50 im Monat). Total dann rund CHF 18 im Monat.

## 1. Anmelden

```bash
az login
az account set --subscription d7088f29-7c73-47ea-9b93-961edf4a5431
```

## 2. GitHub-Anmeldung bei Azure (OIDC)

Eine App-Registrierung für GitHub Actions, ohne Passwort. Zwei Federated Credentials: Branch `main` (what-if) und Environment `prod` (Deploy).

```bash
APP_ID=$(az ad app create --display-name "github-tauchplaetze-prod" --query appId -o tsv)
az ad sp create --id "$APP_ID"
for subject in "repo:mduu/tauchplaetze.ch:ref:refs/heads/main" "repo:mduu/tauchplaetze.ch:environment:prod"; do
  az ad app federated-credential create --id "$APP_ID" --parameters "{\"name\":\"$(echo "$subject" | tr ':/' '--')\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"$subject\",\"audiences\":[\"api://AzureADTokenExchange\"]}"
done
```

Rechte auf der Subscription (die Resource Group entsteht erst beim ersten Deploy):

```bash
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
SUB=/subscriptions/d7088f29-7c73-47ea-9b93-961edf4a5431
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role Contributor --scope "$SUB"
# Für die Rollen der Managed Identity (deploy/modules/roles.bicep). Eingeschränkt auf die vier Rollen wäre besser: siehe offene Punkte.
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role "Role Based Access Control Administrator" --scope "$SUB"
```

Nach dem ersten Deploy die Rechte auf die Resource Group `rg-tauchplaetze-prod` einschränken.

In GitHub (Settings → Environments): Environment **`prod`** anlegen, «Required reviewers»: Marc.

In GitHub (Settings → Secrets and variables → Actions):

| Art | Name | Wert |
|---|---|---|
| Variable | `AZURE_CLIENT_ID` | `$APP_ID` |
| Variable | `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| Variable | `AZURE_SUBSCRIPTION_ID` | `d7088f29-7c73-47ea-9b93-961edf4a5431` |
| Variable | `SQL_ADMIN_LOGIN` | Anzeigename des SQL-Admins (Benutzer oder Gruppe) |
| Variable | `SQL_ADMIN_OBJECT_ID` | `az ad signed-in-user show --query id -o tsv` (oder die Gruppe) |
| Variable | `SQL_ADMIN_PRINCIPAL_TYPE` | `User` oder `Group` |
| Secret | `ALERT_EMAIL` | Adresse für Alerts und Budget |

Solange `AZURE_CLIENT_ID` fehlt, überspringt `deploy.yml` den Deploy.

## 3. Infrastruktur anlegen

GitHub → Actions → **Infra** → «Run workflow», zuerst `what-if`, dann `deploy` (Freigabe im Environment `prod`).

Alternativ lokal:

```bash
export SQL_ADMIN_LOGIN="…" SQL_ADMIN_OBJECT_ID="…" ALERT_EMAIL="…"
az deployment sub what-if -l switzerlandnorth -f deploy/main.bicep -p deploy/main.bicepparam
az deployment sub create  -l switzerlandnorth -f deploy/main.bicep -p deploy/main.bicepparam
```

## 4. Secrets im Key Vault

Wer Secrets setzt, braucht die Rolle «Key Vault Secrets Officer» auf `kv-tauchplaetze-prod`:

```bash
az role assignment create --assignee "$(az ad signed-in-user show --query id -o tsv)" --role "Key Vault Secrets Officer" \
  --scope "$(az keyvault show -n kv-tauchplaetze-prod --query id -o tsv)"
```

Dann im eigenen Terminal (Werte nicht in den Chat):

```bash
KV=kv-tauchplaetze-prod
az keyvault secret set --vault-name $KV --name "Accounts--Admins--0--Email" --value "<E-Mail des ersten Admins>"
az keyvault secret set --vault-name $KV --name "Operator--Email"            --value "<Empfänger des Kontaktformulars>"
az keyvault secret set --vault-name $KV --name "Mail--UserName"             --value "<Postfach bei Hostpoint, z.B. noreply@tauchplaetze.ch>"
az keyvault secret set --vault-name $KV --name "Mail--Password"             --value "<Passwort des Postfachs>"
az keyvault secret set --vault-name $KV --name "Hydro--Forecast--Username"  --value "<BAFU-Benutzer>"
az keyvault secret set --vault-name $KV --name "Hydro--Forecast--Password"  --value "<BAFU-Passwort>"
az keyvault secret set --vault-name $KV --name "Content--ExportKey"         --value "$(openssl rand -base64 32)"
```

`--` wird in der App zu `:`. Den Export-Key lokal als `Content:ExportKey` in den User Secrets der Web-App eintragen, damit «Aus Prod übernehmen» funktioniert (ADR-011). Danach die App neu starten: `az webapp restart -g rg-tauchplaetze-prod -n app-tauchplaetze-prod`.

## 5. SQL-Benutzer für die App

Die App meldet sich mit ihrer Managed Identity an. Als SQL-Admin einmal ausführen (Azure Portal → Datenbank → Query Editor, oder `sqlcmd -G`). Für den Zugriff von deinem Rechner vorher deine IP in der Firewall erlauben und danach wieder entfernen.

```sql
CREATE USER [app-tauchplaetze-prod] FROM EXTERNAL PROVIDER;
ALTER ROLE db_owner ADD MEMBER [app-tauchplaetze-prod];
```

`db_owner`, weil die App beim Start migriert (`Database__PrepareOnStartup`).

## 6. Mail über Hostpoint

Die App Settings stehen schon (ADR-012): Server `asmtp.mail.hostpoint.ch`, Port 587 mit STARTTLS, Anmeldung mit der ganzen E-Mail-Adresse. Quelle: [Hostpoint Support, E-Mail-Einstellungen](https://support.hostpoint.ch/en/technical/e-mail/frequently-asked-questions/e-mail-settings-at-a-glance).

- Postfach für den Absender anlegen (z.B. `noreply@tauchplaetze.ch`), Passwort in Schritt 4.
- DNS für `tauchplaetze.ch`: SPF mit Hostpoint (`include:spf.mail.hostpoint.ch`, die genaue Angabe im Control Panel von Hostpoint prüfen), DKIM im Control Panel aktivieren, DMARC zuerst mit `p=none`.

## 7. Erster Deploy der App

Nach Schritt 2–5: GitHub → Actions → **Deploy** → «Run workflow» (oder ein Push auf `main` mit grüner CI). Der Workflow prüft danach `/alive` und `/de`.

Beim ersten Start migriert die App die Datenbank, legt Gewässer, den Admin und die Plätze aus dem Seed an. Die erste Prognose rechnet der Worker zum nächsten Termin.

Prüfen:

- `https://app-tauchplaetze-prod.azurewebsites.net/alive` → `Healthy`
- `https://app-tauchplaetze-prod.azurewebsites.net/health/ready` → nach dem ersten Lauf `Healthy`
- Anmelden mit «Passwort vergessen» für die Admin-Adresse (die Mail kommt über Hostpoint).

## 8. Domain und Cloudflare

1. App Service → Custom domains → `tauchplaetze.ch` und `www.tauchplaetze.ch` hinzufügen. Azure zeigt einen TXT-Eintrag `asuid…` zur Prüfung.
2. Cloudflare DNS:
   - `asuid` und `asuid.www` als TXT (Wert aus Azure).
   - `tauchplaetze.ch` CNAME (Flattening) auf `app-tauchplaetze-prod.azurewebsites.net`, zuerst **ohne** Proxy (grau), bis das Zertifikat steht.
   - `www` CNAME auf `app-tauchplaetze-prod.azurewebsites.net`.
3. App Service → Certificates → «App Service Managed Certificate» für beide Namen, dann Binding SNI SSL.
4. Cloudflare-Proxy einschalten (orange), SSL/TLS «Full (strict)».
5. Redirect Rules in Cloudflare: `www.tauchplaetze.ch/*` und `sichtweiten.ch/*`, `www.sichtweiten.ch/*` → `https://tauchplaetze.ch/$1` (301).
6. Später: `media.tauchplaetze.ch` vor dem Container `media` (infrastructure.md), dann `mediaPublicBaseUri` in `main.bicepparam` setzen.

## 9. Zurück auf eine frühere Version

- App: im Repository den letzten guten Commit wieder deployen (Actions → Deploy → «Run workflow» auf diesem Stand) oder `git revert` und Push.
- Migrationen fügen nur hinzu (Review-Regel). Eine ältere App läuft darum auf dem neueren Schema.
- Datenbank: Point-in-time Restore (7 Tage) im Portal in eine neue Datenbank, dann den Connection String umstellen.

# Go-live: Schritte für Marc

Stand 20.10.2026. Die Infrastruktur steht in `deploy/` (Bicep), die Workflows in `.github/workflows/` (`infra.yml`, `deploy.yml`). Grundlage: `specs/infrastructure.md`.

Geheimnisse (Passwörter, Schlüssel, E-Mail-Adressen) **immer in einem eigenen Terminal** setzen, nie mit `!` im Chat mit Claude: Dort landen Befehl und Ausgabe im Verlauf.

Die Datenbank läuft auf **Basic** (5 DTU, 2 GB, pausiert nie, rund CHF 4.50 im Monat; Entscheid Marc, 20.10.2026). Das Free Offer bleibt als Option (`sqlPlan = 'Free'`), sein Kontingent wäre mit dem Abfluss-Job nach wenigen Tagen aufgebraucht.

## 1. Anmelden und Resource Group anlegen

```bash
az login
az account set --subscription "<Subscription-ID>"
az group create --name rg-tauchplaetze-prod --location switzerlandnorth --tags project=tauchplaetze environment=prod
```

## 2. GitHub-Anmeldung bei Azure (OIDC)

Eine App-Registrierung für GitHub Actions, ohne Passwort. Zwei Federated Credentials, eine pro Environment:

- `prod-app`: Deploy der App und what-if der Infrastruktur, **ohne Freigabe**.
- `prod`: Deploy der Infrastruktur, **mit Freigabe**.

GitHub schickt das Subject mit den festen IDs von Konto und Repository (`repo:mduu@8256317/tauchplaetze.ch@1423266386:…`), nicht mehr nur mit den Namen. Ein Subject ohne IDs schlägt mit `AADSTS700213` fehl (20.10.2026). Die IDs zeigt das Log eines gescheiterten Laufs unter «subject claim».

Eine Credential für den Branch `main` braucht es nicht: Jeder Job mit Azure läuft in einem der beiden Environments.

```bash
APP_ID=$(az ad app create --display-name "github-tauchplaetze-prod" --query appId -o tsv)
az ad sp create --id "$APP_ID"
for env in prod-app prod; do
  az ad app federated-credential create --id "$APP_ID" --parameters "{\"name\":\"github-env-$env\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"repo:mduu@8256317/tauchplaetze.ch@1423266386:environment:$env\",\"audiences\":[\"api://AzureADTokenExchange\"]}"
done
```

Der Service Principal bekommt **nur Contributor auf der Resource Group**. Er vergibt keine Rollen (das Template enthält keine).

```bash
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
RG_ID=$(az group show -n rg-tauchplaetze-prod --query id -o tsv)
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal --role Contributor --scope "$RG_ID"
```

In GitHub (Settings → Environments) zwei Environments anlegen:

| Environment | Required reviewers | Deployment branches | Wofür |
|---|---|---|---|
| `prod-app` | keine | «Selected branches»: nur `main` | Deploy der App nach grüner CI (Entscheid Marc, 20.10.2026), what-if der Infrastruktur |
| `prod` | **du** | «Selected branches»: nur `main` | Deploy der Infrastruktur |

Das Repository ist öffentlich, Required Reviewers sind darum ohne bezahlten Plan verfügbar. «Deployment branches: main» verhindert, dass ein «Run workflow» auf einem anderen Branch deployt.

Vereinfachung (Ein-Personen-Repository): Beide Environments nutzen denselben Service Principal mit Contributor auf der Resource Group. Der what-if-Job in `prod-app` hätte damit dieselben Rechte wie der Deploy. Strenger wäre ein zweiter Service Principal mit «Reader» für `prod-app`, der App-Deploy bräuchte dann «Website Contributor» auf der Web App.

In GitHub (Settings → Secrets and variables → Actions):

| Art | Name | Wert |
|---|---|---|
| Variable | `AZURE_CLIENT_ID` | `$APP_ID` |
| Variable | `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| Variable | `AZURE_SUBSCRIPTION_ID` | `<Subscription-ID>` |
| Secret | `SQL_ADMIN_LOGIN` | Anmeldename des SQL-Admins (Benutzer oder Gruppe) |
| Secret | `SQL_ADMIN_OBJECT_ID` | `az ad signed-in-user show --query id -o tsv` (oder die Gruppe) |
| Variable | `SQL_ADMIN_PRINCIPAL_TYPE` | `User` oder `Group` |
| Secret | `ALERT_EMAIL` | Adresse für Alerts und Budget |

Die Logs eines öffentlichen Repositorys sind öffentlich. GitHub maskiert nur Secrets, keine Variablen. Darum sind Anmeldename und Object-ID des SQL-Admins Secrets.

Solange `AZURE_CLIENT_ID` fehlt, überspringt `deploy.yml` den Deploy.

## 3. Infrastruktur anlegen

GitHub → Actions → **Infra** → «Run workflow». Standard ist `what-if`: Es zeigt nur die Änderungen in der Zusammenfassung. Mit `deploy` wendet der Workflow sie an, nach deiner Freigabe im Environment `prod`.

Alternativ lokal:

```bash
export SQL_ADMIN_LOGIN="…" SQL_ADMIN_OBJECT_ID="…" ALERT_EMAIL="…"
az deployment group what-if -g rg-tauchplaetze-prod -f deploy/main.bicep -p deploy/main.bicepparam
az deployment group create  -g rg-tauchplaetze-prod -f deploy/main.bicep -p deploy/main.bicepparam
```

## 4. Rollen der Managed Identity (einmal, nach dem ersten Infra-Deploy)

Die Web App greift ohne Schlüssel auf Storage und Key Vault zu. Die Rechte vergibst du von Hand:

```bash
RG=rg-tauchplaetze-prod
APP_PRINCIPAL=$(az webapp identity show -g $RG -n app-tauchplaetze-prod --query principalId -o tsv)
STORAGE_ID=$(az storage account show -g $RG -n sttauchplaetzeprod --query id -o tsv)
KV_ID=$(az keyvault show -g $RG -n kv-tauchplaetze-prod --query id -o tsv)

az role assignment create --assignee-object-id "$APP_PRINCIPAL" --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor"  --scope "$STORAGE_ID"
az role assignment create --assignee-object-id "$APP_PRINCIPAL" --assignee-principal-type ServicePrincipal --role "Storage Queue Data Contributor" --scope "$STORAGE_ID"
az role assignment create --assignee-object-id "$APP_PRINCIPAL" --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User"        --scope "$KV_ID"
```

Für dich selbst, um die Secrets zu setzen:

```bash
az role assignment create --assignee "$(az ad signed-in-user show --query id -o tsv)" --role "Key Vault Secrets Officer" --scope "$KV_ID"
```

Wird die Web App neu angelegt, bekommt sie eine neue Identität. Dann diesen Schritt und Schritt 6 wiederholen.

## 5. Secrets im Key Vault

Im eigenen Terminal (Werte nicht in den Chat):

```bash
KV=kv-tauchplaetze-prod
az keyvault secret set --vault-name $KV --name "Accounts--Admins--0--Email" --value "<E-Mail des ersten Admins>"
az keyvault secret set --vault-name $KV --name "Operator--Email"            --value "<Empfänger des Kontaktformulars>"
az keyvault secret set --vault-name $KV --name "Mail--UserName"             --value "<Postfach, z.B. noreply@tauchplaetze.ch>"
az keyvault secret set --vault-name $KV --name "Mail--Password"             --value "<Passwort des Postfachs>"
az keyvault secret set --vault-name $KV --name "Hydro--Forecast--Username"  --value "<BAFU-Benutzer>"
az keyvault secret set --vault-name $KV --name "Hydro--Forecast--Password"  --value "<BAFU-Passwort>"
az keyvault secret set --vault-name $KV --name "Content--ExportKey"         --value "$(openssl rand -base64 32)"
```

Die App Settings verweisen mit `@Microsoft.KeyVault(VaultName=…;SecretName=…)` auf diese Secrets, App Service löst sie auf. Fehlt ein Secret oder die Rolle aus Schritt 4, bleibt der Verweis stehen. Die App startet dann nicht und nennt im Log den Key (nie den Wert). Nach einer Änderung im Key Vault die App neu starten: `az webapp restart -g rg-tauchplaetze-prod -n app-tauchplaetze-prod`.

Den Export-Key lokal als `Content:ExportKey` in den User Secrets der Web-App eintragen, damit «Aus Prod übernehmen» funktioniert (ADR-011).

## 6. SQL-Benutzer für die App

Die App meldet sich mit ihrer Managed Identity an. Als SQL-Admin einmal ausführen (Azure Portal → Datenbank → Query Editor, oder `sqlcmd -G`). Für den Zugriff von deinem Rechner vorher deine IP in der Firewall erlauben und danach wieder entfernen.

```sql
CREATE USER [app-tauchplaetze-prod] FROM EXTERNAL PROVIDER;
ALTER ROLE db_owner ADD MEMBER [app-tauchplaetze-prod];
```

`db_owner`, weil die App beim Start migriert (`Database__PrepareOnStartup`). Das ist bewusst so (Security-Review 20.10.2026): Ein eigener Migrations-Benutzer bräuchte Zugang aus GitHub durch die SQL-Firewall.

## 7. Mail über Hostpoint

Host, Port und TLS stehen in den App Settings, Benutzer und Passwort im Key Vault (ADR-012): Server `asmtp.mail.hostpoint.ch`, Port 587 mit STARTTLS, Anmeldung mit der ganzen E-Mail-Adresse. Quelle: [Hostpoint Support, E-Mail-Einstellungen](https://support.hostpoint.ch/en/technical/e-mail/frequently-asked-questions/e-mail-settings-at-a-glance).

- Postfach für den Absender anlegen (z.B. `noreply@tauchplaetze.ch`), Passwort in Schritt 5.
- DNS für `tauchplaetze.ch`: SPF mit Hostpoint (`include:spf.mail.hostpoint.ch`, die genaue Angabe im Control Panel von Hostpoint prüfen), DKIM im Control Panel aktivieren, DMARC zuerst mit `p=none`.

## 8. Erster Deploy der App

Nach Schritt 2–7: GitHub → Actions → **Deploy** → «Run workflow» (oder ein Push auf `main` mit grüner CI, ohne Freigabe). Der Workflow prüft danach `/alive` und `/de`. Er deployt nur Pushes auf `main` dieses Repositorys, nie einen Pull Request aus einem Fork.

Beim ersten Start migriert die App die Datenbank, legt Gewässer, den Admin und die Plätze aus dem Seed an. Die erste Prognose rechnet der Worker zum nächsten Termin.

Prüfen:

- `https://app-tauchplaetze-prod.azurewebsites.net/alive` → `Healthy`
- `https://app-tauchplaetze-prod.azurewebsites.net/health/ready` → nach dem ersten Lauf `Healthy`
- Anmelden mit «Passwort vergessen» für die Admin-Adresse (die Mail kommt über Hostpoint).

## 9. Domain (DNS bei Hostpoint, ohne Cloudflare)

Entscheid Marc, 21.10.2026: Das DNS bleibt bei Hostpoint, wie bei bau-ticker. Hostpoint pflegt DKIM selbst (drei Schlüssel mit zufälligem Selector), ein Umzug zu Cloudflare müsste sie von Hand kopieren. Die Weiterleitungen (`www`, `sichtweiten.ch`) macht die App (`HostRedirects`, `seo.md`). `restrictToCloudflare` bleibt `false`.

1. Werte holen:
   ```bash
   az webapp show -g rg-tauchplaetze-prod -n app-tauchplaetze-prod --query customDomainVerificationId -o tsv   # Wert für asuid
   dig +short app-tauchplaetze-prod.azurewebsites.net | tail -1                                                # IP für den A-Record
   ```
2. Hostpoint Zone-Editor, je für `tauchplaetze.ch` und `sichtweiten.ch`:
   - TXT `asuid` und `asuid.www` mit dem Wert aus 1.
   - A `@` auf die IP aus 1 (ersetzt den Webspace von Hostpoint). MX, SPF, DKIM und DMARC bleiben unverändert.
   - CNAME `www` auf `app-tauchplaetze-prod.azurewebsites.net`.
3. Custom Domains und Zertifikate (kostenlos, erneuern sich selbst), für alle vier Namen:
   ```bash
   for host in tauchplaetze.ch www.tauchplaetze.ch sichtweiten.ch www.sichtweiten.ch; do
     az webapp config hostname add -g rg-tauchplaetze-prod --webapp-name app-tauchplaetze-prod --hostname $host
     thumb=$(az webapp config ssl create -g rg-tauchplaetze-prod -n app-tauchplaetze-prod --hostname $host --query thumbprint -o tsv)
     az webapp config ssl bind -g rg-tauchplaetze-prod -n app-tauchplaetze-prod --certificate-thumbprint $thumb --ssl-type SNI
   done
   ```
4. GitHub-Variable `PUBLIC_BASE_URL` = `https://tauchplaetze.ch` setzen (Health Check nach dem Deploy über die echte Domain).
5. Prüfen: `https://tauchplaetze.ch/de` (200), `https://www.tauchplaetze.ch/de` und `https://sichtweiten.ch/zuerichsee` (301 auf `tauchplaetze.ch`).

Die IP des A-Records ändert sich nur, wenn die Web App in einen anderen Plan oder eine andere Region zieht. Dann den A-Record nachführen.

## 9b. Später: Cloudflare davor

Bei viel Traffic (Fotos) oder Missbrauch. Die DKIM-Einträge von Hostpoint vorher aus dem Zone-Editor kopieren.

1. App Service → Custom domains → `tauchplaetze.ch` und `www.tauchplaetze.ch` hinzufügen. Azure zeigt einen TXT-Eintrag `asuid…` zur Prüfung.
2. Cloudflare DNS:
   - `asuid` und `asuid.www` als TXT (Wert aus Azure).
   - `tauchplaetze.ch` CNAME (Flattening) auf `app-tauchplaetze-prod.azurewebsites.net`, zuerst **ohne** Proxy (grau), bis das Zertifikat steht.
   - `www` CNAME auf `app-tauchplaetze-prod.azurewebsites.net`.
3. App Service → Certificates → «App Service Managed Certificate» für beide Namen, dann Binding SNI SSL.
4. **Proxy an und Zugriff einschränken direkt nacheinander:**
   1. Cloudflare-Proxy einschalten (orange), SSL/TLS «Full (strict)».
   2. Sofort danach in `main.bicepparam` `restrictToCloudflare = true` und `infra.yml` (what-if, deploy). Das setzt die Access Restrictions auf die IP-Bereiche von Cloudflare (Default Deny) und `Contact__TrustCloudflareHeader=true`. Die SCM-Site bleibt offen, der Deploy über OIDC geht weiter.
   3. GitHub-Variable `PUBLIC_BASE_URL` = `https://tauchplaetze.ch` setzen. Der Health Check in `deploy.yml` ruft sonst die gesperrte Adresse `azurewebsites.net` auf.

   Warum direkt nacheinander: Mit Proxy, aber ohne Schalter zählen alle Rate Limits die IP der Edge von Cloudflare. Wenige Besucher über dieselbe Edge sperren dann alle anderen (Kontakt, Anmeldung). Ohne Einschränkung wären WAF und Rate Limits zudem über `azurewebsites.net` umgehbar, und `CF-Connecting-IP` liesse sich fälschen.
5. Die Weiterleitungen macht weiter die App (`HostRedirects`). Redirect Rules in Cloudflare sind optional, sie entlasten die App nur wenig.
6. Bot-Regeln: Der Health Check aus GitHub Actions kommt von Rechenzentren. «Bot Fight Mode» oder eine Challenge kann ihn blockieren (HTTP 403 oder 503 im Schritt «Health»). Dann eine Custom Rule «Skip» für die Pfade `/alive` und `/health/ready` anlegen, nicht für die ganze Website.
7. Später: `media.tauchplaetze.ch` vor dem Container `media` (infrastructure.md), dann `mediaPublicBaseUri` in `main.bicepparam` setzen.
8. Das App Service Managed Certificate erneuert sich nicht immer hinter dem Proxy von Cloudflare. Schlägt die Erneuerung fehl: Cloudflare Origin Certificate (15 Jahre) erzeugen und in App Service hochladen.

## 10. Zurück auf eine frühere Version

- App: im Repository den letzten guten Commit wieder deployen (Actions → Deploy → «Run workflow» auf diesem Stand) oder `git revert` und Push.
- Migrationen fügen nur hinzu (Review-Regel). Eine ältere App läuft darum auf dem neueren Schema.
- Datenbank: Point-in-time Restore (7 Tage) im Portal in eine neue Datenbank, dann den Connection String umstellen.

---
name: contact-blocklist
description: Erweitert, zeigt oder kürzt die Sperrliste des Kontaktformulars (Secret Contact--BlockedDomains im Key Vault von Prod). Verwenden, wenn Marc Spam aus dem Kontaktformular meldet und eine Domain sperren will, die Liste sehen will oder eine Domain wieder freigeben will. Auch mit /contact-blocklist.
---

# Sperrliste des Kontaktformulars

Das Kontaktformular verwirft eine Nachricht still, wenn die Adresse der Absenderin in einer
gesperrten Domain liegt oder der Text eine nennt (`specs/infrastructure.md`, «Sperrliste
Kontaktformular»). Die Liste ist das Secret `Contact--BlockedDomains` im Key Vault
`kv-tauchplaetze-prod`: Domains, getrennt mit Komma.

## Regeln

- **Keine Domain ins Repository.** Nicht in Code, Specs, Tests, Commit-Nachrichten oder PRs. Das
  Repository ist öffentlich und nennt keine konkreten Websites (`AGENTS.md`, «Personal Data»).
  Tests nutzen `spam.example`.
- Nur die Domain sperren, nie eine ganze Adresse und nie einen grossen Mail-Anbieter (gmail.com,
  outlook.com, bluewin.ch, …): Das würde echte Besucher aussperren. Bei Zweifel Marc fragen.
- Eine Domain steht ohne `http://`, ohne `www.` und ohne Pfad da, in Kleinbuchstaben.
- Die Änderung trifft Prod. Marc hat sie mit dem Auftrag verlangt; mehr als die genannten Domains
  ändert der Skill nicht.

## Ablauf

1. **Liste lesen** (existiert das Secret noch nicht, ist die Liste leer):

   ```bash
   az keyvault secret show --vault-name kv-tauchplaetze-prod -n Contact--BlockedDomains --query value -o tsv
   ```

2. **Neue Liste bilden:** Domain ergänzen oder entfernen, Duplikate weg, sortiert, mit `, `
   getrennt. Marc die alte und die neue Liste zeigen.

3. **Secret setzen:**

   ```bash
   az keyvault secret set --vault-name kv-tauchplaetze-prod -n Contact--BlockedDomains --value "<liste>" --query attributes.updated -o tsv
   ```

   Eine leere Liste: den Wert `-` setzen (ein Secret kann nicht leer sein, `-` ist keine Domain
   und sperrt nichts).

4. **Referenzen neu laden.** Ein normaler Neustart lädt Key-Vault-Referenzen nicht neu:

   ```bash
   az rest --method post --url "$(az webapp show -g rg-tauchplaetze-prod -n app-tauchplaetze-prod --query id -o tsv)/config/configreferences/appsettings/refresh?api-version=2022-03-01"
   ```

   Lehnt die Umgebung den Befehl ab, Marc bitten, ihn auszuführen.

5. **Prüfen**, dass die App den neuen Wert sieht (nur die Zahl der Einträge ausgeben), und dass
   `/alive` mit 200 antwortet:

   ```bash
   H=$(az webapp show -g rg-tauchplaetze-prod -n app-tauchplaetze-prod --query "enabledHostNames[?contains(@,'scm')]|[0]" -o tsv)
   az rest --method get --url "https://$H/api/settings" --resource https://management.azure.com/ --query Contact__BlockedDomains -o tsv
   ```

6. **Melden:** welche Domains jetzt gesperrt sind, und dass die Sperre ab sofort gilt.

## Wirkung nachsehen

Verworfene Nachrichten stehen im Log, ohne Adresse:

```kusto
AppTraces | where Message has "Contact message dropped" | summarize count() by bin(TimeGenerated, 1d)
```

## Erster Einsatz

Die Referenz kommt mit `deploy/modules/webApp.bicep` in die App Settings. Vor dem ersten Lauf von
`infra.yml` mit dieser Änderung muss das Secret existieren (Schritt 3), sonst bleibt die Referenz
ungelöst. Das sperrt nichts, aber die Sperrliste wirkt dann auch nicht.

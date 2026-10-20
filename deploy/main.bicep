// Infrastruktur von tauchplaetze.ch (specs/infrastructure.md). Eine Umgebung: prod.
//
// Prüfen:   az deployment sub what-if  -l switzerlandnorth -f deploy/main.bicep -p deploy/main.bicepparam
// Anwenden: az deployment sub create   -l switzerlandnorth -f deploy/main.bicep -p deploy/main.bicepparam
// Die Werte ohne Default kommen aus Umgebungsvariablen (main.bicepparam). Secrets setzt Marc im Key Vault (doc/go-live.md).

targetScope = 'subscription'

@description('Region aller Ressourcen. Rückfall West Europe, falls eine Ressource in Switzerland North fehlt.')
@allowed(['switzerlandnorth', 'westeurope'])
param location string = 'switzerlandnorth'

@description('Free: Azure SQL Free Offer (Serverless, pausiert am Limit). Basic: 5 DTU, feste Kosten, pausiert nie.')
@allowed(['Free', 'Basic'])
param sqlPlan string = 'Free'

@description('Entra-Admin des SQL Servers: Anzeigename des Benutzers oder der Gruppe.')
param sqlAdminLogin string

@description('Object ID des Entra-Admins.')
param sqlAdminObjectId string

@description('User oder Group.')
@allowed(['User', 'Group'])
param sqlAdminPrincipalType string = 'User'

@description('Empfänger der Alerts und des Budgets. Leer: keine Alerts, kein Budget.')
param alertEmail string = ''

@description('Erster Tag des Budgets (Monatsanfang). Nach dem ersten Deploy nie mehr ändern, Azure lehnt das ab.')
param budgetStartDate string = '2026-11-01'

@description('Budget pro Monat in der Währung der Subscription (Ziel CHF 20).')
param monthlyBudget int = 25

@description('Absender der Mails (Postfach bei Hostpoint).')
param mailFrom string = 'tauchplaetze.ch <noreply@tauchplaetze.ch>'

@description('Öffentliche Adresse der Website, für Links in Mails.')
param publicSiteBaseUri string = 'https://tauchplaetze.ch'

@description('Adresse vor dem Container media (Cloudflare). Leer: die Adresse des Containers.')
param mediaPublicBaseUri string = ''

@description('Anzeigename des ersten Admins. Die E-Mail steht im Key Vault (Accounts--Admins--0--Email).')
param adminDisplayName string = 'Marc'

var names = {
  resourceGroup: 'rg-tauchplaetze-prod'
  plan: 'asp-tauchplaetze-prod'
  webApp: 'app-tauchplaetze-prod'
  sqlServer: 'sql-tauchplaetze-prod'
  database: 'sqldb-tauchplaetze'
  storage: 'sttauchplaetzeprod'
  keyVault: 'kv-tauchplaetze-prod'
  logs: 'log-tauchplaetze-prod'
  insights: 'appi-tauchplaetze-prod'
  actionGroup: 'ag-tauchplaetze-prod'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: names.resourceGroup
  location: location
  tags: {
    project: 'tauchplaetze'
    environment: 'prod'
  }
}

module resources 'modules/resources.bicep' = {
  scope: rg
  name: 'tauchplaetze-resources'
  params: {
    location: location
    names: names
    sqlPlan: sqlPlan
    sqlAdminLogin: sqlAdminLogin
    sqlAdminObjectId: sqlAdminObjectId
    sqlAdminPrincipalType: sqlAdminPrincipalType
    alertEmail: alertEmail
    budgetStartDate: budgetStartDate
    monthlyBudget: monthlyBudget
    mailFrom: mailFrom
    publicSiteBaseUri: publicSiteBaseUri
    mediaPublicBaseUri: mediaPublicBaseUri
    adminDisplayName: adminDisplayName
  }
}

output resourceGroup string = rg.name
output webAppName string = resources.outputs.webAppName
output webAppHostName string = resources.outputs.webAppHostName
output webAppPrincipalId string = resources.outputs.webAppPrincipalId
output keyVaultName string = resources.outputs.keyVaultName
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output storageAccountName string = resources.outputs.storageAccountName

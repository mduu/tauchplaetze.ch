// Infrastruktur von tauchplaetze.ch (specs/infrastructure.md). Eine Umgebung: prod.
// Die Resource Group legt Marc von Hand an, ebenso die Rollen der Managed Identity (doc/go-live.md). Das Template vergibt keine Rechte.
//
// Prüfen:   az deployment group what-if -g rg-tauchplaetze-prod -f deploy/main.bicep -p deploy/main.bicepparam
// Anwenden: az deployment group create  -g rg-tauchplaetze-prod -f deploy/main.bicep -p deploy/main.bicepparam

targetScope = 'resourceGroup'

@description('Region aller Ressourcen. Rückfall West Europe, falls eine Ressource in Switzerland North fehlt.')
@allowed(['switzerlandnorth', 'westeurope'])
param location string = 'switzerlandnorth'

@description('Basic: 5 DTU, 2 GB, pausiert nie (Entscheid Marc, 20.10.2026). Free: Azure SQL Free Offer, pausiert am Kontingent bis Monatsende.')
@allowed(['Basic', 'Free'])
param sqlPlan string = 'Basic'

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

@description('Absender der Mails (Postfach beim E-Mail-Anbieter).')
param mailFrom string = 'tauchplaetze.ch <noreply@tauchplaetze.ch>'

@description('Öffentliche Adresse der Website, für Links in Mails.')
param publicSiteBaseUri string = 'https://tauchplaetze.ch'

@description('Adresse vor dem Container media (Cloudflare). Leer: die Adresse des Containers.')
param mediaPublicBaseUri string = ''

@description('Anzeigename des ersten Admins. Die E-Mail steht im Key Vault (Accounts--Admins--0--Email).')
param adminDisplayName string = 'Marc'

@description('Nur Cloudflare darf die App erreichen (Access Restrictions). Erst auf true, wenn DNS und Proxy bei Cloudflare laufen (doc/go-live.md). Setzt auch Contact__TrustCloudflareHeader.')
param restrictToCloudflare bool = false

var names = {
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

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logsName: names.logs
    insightsName: names.insights
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: names.storage
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    name: names.keyVault
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    serverName: names.sqlServer
    databaseName: names.database
    plan: sqlPlan
    adminLogin: sqlAdminLogin
    adminObjectId: sqlAdminObjectId
    adminPrincipalType: sqlAdminPrincipalType
  }
}

module webApp 'modules/webApp.bicep' = {
  name: 'webApp'
  params: {
    location: location
    planName: names.plan
    name: names.webApp
    appInsightsConnectionString: monitoring.outputs.connectionString
    sqlConnectionString: sql.outputs.connectionString
    blobEndpoint: storage.outputs.blobEndpoint
    queueEndpoint: storage.outputs.queueEndpoint
    keyVaultName: keyVault.outputs.name
    mailFrom: mailFrom
    publicSiteBaseUri: publicSiteBaseUri
    mediaPublicBaseUri: mediaPublicBaseUri
    adminDisplayName: adminDisplayName
    restrictToCloudflare: restrictToCloudflare
  }
}

module workbook 'modules/workbook.bicep' = {
  name: 'workbook'
  params: {
    location: location
    insightsId: monitoring.outputs.insightsId
    workspaceId: monitoring.outputs.workspaceId
    webAppId: webApp.outputs.id
    planId: webApp.outputs.planId
    databaseId: sql.outputs.databaseId
  }
}

module dashboard 'modules/dashboard.bicep' = {
  name: 'dashboard'
  params: {
    location: location
    workspaceId: monitoring.outputs.workspaceId
    webAppId: webApp.outputs.id
    planId: webApp.outputs.planId
    databaseId: sql.outputs.databaseId
    workbookId: workbook.outputs.id
  }
}

module alerts 'modules/alerts.bicep' = if (!empty(alertEmail)) {
  name: 'alerts'
  params: {
    actionGroupName: names.actionGroup
    alertEmail: alertEmail
    webAppId: webApp.outputs.id
    workspaceId: monitoring.outputs.workspaceId
    databaseId: sql.outputs.databaseId
    freeOffer: sqlPlan == 'Free'
  }
}

module budget 'modules/budget.bicep' = if (!empty(alertEmail)) {
  name: 'budget'
  params: {
    alertEmail: alertEmail
    amount: monthlyBudget
    startDate: budgetStartDate
  }
}

output webAppName string = webApp.outputs.name
output webAppHostName string = webApp.outputs.defaultHostName
output webAppPrincipalId string = webApp.outputs.principalId
output keyVaultName string = keyVault.outputs.name
output sqlServerFqdn string = sql.outputs.serverFqdn
output storageAccountName string = storage.outputs.name

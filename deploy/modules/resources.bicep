// Alle Ressourcen in der Resource Group. Reihenfolge: Logs, Storage, Key Vault, SQL, Web App, Rollen, Alerts, Budget.

param location string
param names object
param sqlPlan string
param sqlAdminLogin string
param sqlAdminObjectId string
param sqlAdminPrincipalType string
param alertEmail string
param budgetStartDate string
param monthlyBudget int
param mailFrom string
param publicSiteBaseUri string
param mediaPublicBaseUri string
param adminDisplayName string

module monitoring 'monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logsName: names.logs
    insightsName: names.insights
  }
}

module storage 'storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: names.storage
  }
}

module keyVault 'keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    name: names.keyVault
  }
}

module sql 'sql.bicep' = {
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

module webApp 'webApp.bicep' = {
  name: 'webApp'
  params: {
    location: location
    planName: names.plan
    name: names.webApp
    appInsightsConnectionString: monitoring.outputs.connectionString
    sqlConnectionString: sql.outputs.connectionString
    blobEndpoint: storage.outputs.blobEndpoint
    queueEndpoint: storage.outputs.queueEndpoint
    keyVaultUri: keyVault.outputs.uri
    dataProtectionKeyUri: keyVault.outputs.dataProtectionKeyUri
    mailFrom: mailFrom
    publicSiteBaseUri: publicSiteBaseUri
    mediaPublicBaseUri: mediaPublicBaseUri
    adminDisplayName: adminDisplayName
  }
}

module roles 'roles.bicep' = {
  name: 'roles'
  params: {
    principalId: webApp.outputs.principalId
    storageName: storage.outputs.name
    keyVaultName: keyVault.outputs.name
  }
}

module alerts 'alerts.bicep' = if (!empty(alertEmail)) {
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

module budget 'budget.bicep' = if (!empty(alertEmail)) {
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

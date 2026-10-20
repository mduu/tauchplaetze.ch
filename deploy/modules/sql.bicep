// SQL Server nur mit Entra-Anmeldung, eine Datenbank (infrastructure.md, Azure SQL Free Offer).
// Basic (Standard): 5 DTU, 2 GB, feste Kosten, pausiert nie. Free: Serverless GP_S_Gen5_2 mit Free Offer, pausiert am Limit bis Monatsende.
// Die App verbindet sich über ihre Managed Identity. Den Benutzer dafür legt Marc einmal per SQL an (doc/go-live.md).

param location string
param serverName string
param databaseName string
param plan string
param adminLogin string
param adminObjectId string
param adminPrincipalType string

var free = plan == 'Free'

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      principalType: adminPrincipalType
      tenantId: subscription().tenantId
    }
  }
}

// 0.0.0.0 heisst «Azure-Dienste erlauben»: Die Web App hat keine feste ausgehende Adresse im VNet.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
  location: location
  sku: free ? {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  } : {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: free ? {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 15
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  } : {
    maxSizeBytes: 2147483648
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseId string = database.id
// Der lange Timeout gilt für die Option Free: Serverless braucht nach einer Pause bis zu einer Minute.
output connectionString string = 'Server=tcp:${server.properties.fullyQualifiedDomainName},1433;Database=${databaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connect Timeout=90;'

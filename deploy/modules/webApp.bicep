// App Service Plan Linux B1 und die Web App (.NET 10). Eine Instanz, Always On für die Worker.
// Health Check auf /alive: ohne Datenbank, damit die Serverless-Datenbank pausieren kann (/health/ready weckt sie).
// Secrets liest die App selbst aus dem Key Vault (`KeyVault__Uri`), darum keine Key-Vault-Referenzen in den App Settings.

param location string
param planName string
param name string
param appInsightsConnectionString string
param sqlConnectionString string
param blobEndpoint string
param queueEndpoint string
param keyVaultUri string
param dataProtectionKeyUri string
param mailFrom string
param publicSiteBaseUri string
param mediaPublicBaseUri string
param adminDisplayName string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

var settings = union({
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  ASPNETCORE_ENVIRONMENT: 'Production'
  KeyVault__Uri: keyVaultUri
  DataProtection__KeysBlobUri: '${blobEndpoint}dataprotection/keys.xml'
  DataProtection__KeyVaultKeyUri: dataProtectionKeyUri
  Database__PrepareOnStartup: 'true'
  Accounts__Admins__0__DisplayName: adminDisplayName
  Mail__Host: 'asmtp.mail.hostpoint.ch'
  Mail__Port: '587'
  Mail__UseTls: 'true'
  Mail__From: mailFrom
  PublicSite__BaseUri: publicSiteBaseUri
}, empty(mediaPublicBaseUri) ? {} : {
  Media__PublicBaseUri: mediaPublicBaseUri
})

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      webSocketsEnabled: true
      http20Enabled: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/alive'
      appSettings: [for setting in items(settings): {
        name: setting.key
        value: setting.value
      }]
      connectionStrings: [
        {
          name: 'tauchplaetze'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
        {
          name: 'blobs'
          connectionString: blobEndpoint
          type: 'Custom'
        }
        {
          name: 'queues'
          connectionString: queueEndpoint
          type: 'Custom'
        }
      ]
    }
  }
}

// Deploy nur über Entra (GitHub OIDC), keine Basic-Auth für FTP und SCM.
resource ftp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: site
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scm 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: site
  name: 'scm'
  properties: {
    allow: false
  }
}

resource logs 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: site
  name: 'logs'
  properties: {
    applicationLogs: {
      fileSystem: {
        level: 'Warning'
      }
    }
    httpLogs: {
      fileSystem: {
        enabled: false
      }
    }
  }
}

output id string = site.id
output name string = site.name
output defaultHostName string = site.properties.defaultHostName
output principalId string = site.identity.principalId

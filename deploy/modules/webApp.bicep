// App Service Plan Linux B1 und die Web App (.NET 10). Eine Instanz, Always On für die Worker.
// Health Check auf /alive: ohne Datenbank. App Service fragt jede Minute, /health/ready prüft die Datenbank und ist dafür zu teuer.
// Secrets als Key-Vault-Referenzen: App Service löst sie mit der Managed Identity auf (Rolle «Key Vault Secrets User», doc/go-live.md).
// Data Protection ohne eigene Konfiguration: App Service legt die Schlüssel unter $HOME/ASP.NET/DataProtection-Keys ab (infrastructure.md).
// Mit restrictToCloudflare erreicht nur Cloudflare die App. Der Health Check von App Service ist davon nicht betroffen, die SCM-Site für den Deploy bleibt offen.

param location string
param planName string
param name string
param appInsightsConnectionString string
param sqlConnectionString string
param blobEndpoint string
param queueEndpoint string
param keyVaultName string
param mailFrom string
param publicSiteBaseUri string
param mediaPublicBaseUri string
param adminDisplayName string

@description('Nur die IP-Bereiche von Cloudflare erreichen die App. Die SCM-Site (Deploy über OIDC) bleibt offen.')
param restrictToCloudflare bool

@description('IP-Bereiche von Cloudflare, Quelle https://www.cloudflare.com/ips (Stand 20.10.2026). Bei Änderungen bei Cloudflare nachführen.')
param cloudflareIpRanges array = [
  '173.245.48.0/20'
  '103.21.244.0/22'
  '103.22.200.0/22'
  '103.31.4.0/22'
  '141.101.64.0/18'
  '108.162.192.0/18'
  '190.93.240.0/20'
  '188.114.96.0/20'
  '197.234.240.0/22'
  '198.41.128.0/17'
  '162.158.0.0/15'
  '104.16.0.0/13'
  '104.24.0.0/14'
  '172.64.0.0/13'
  '131.0.72.0/22'
  '2400:cb00::/32'
  '2606:4700::/32'
  '2803:f800::/32'
  '2405:b500::/32'
  '2405:8100::/32'
  '2a06:98c0::/29'
  '2c0f:f248::/32'
]

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

var secretNames = [
  'Accounts--Admins--0--Email'
  'Operator--Email'
  'Mail--UserName'
  'Mail--Password'
  'Hydro--Forecast--Username'
  'Hydro--Forecast--Password'
  'Content--ExportKey'
  'Translation--ApiKey'
]

var secretSettings = toObject(secretNames, name => replace(name, '--', '__'), name => '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=${name})')

var cloudflareRules = [for (range, i) in cloudflareIpRanges: {
  name: 'cloudflare-${i}'
  ipAddress: range
  action: 'Allow'
  priority: 100 + i
}]

var settings = union(secretSettings, {
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  ASPNETCORE_ENVIRONMENT: 'Production'
  // Der erste Start migriert die leere Datenbank und legt die Stammdaten an. Das dauerte am 20.10.2026 über die Standardgrenze von 230 s.
  WEBSITES_CONTAINER_START_TIME_LIMIT: '600'
  // Startet die App direkt aus dem ZIP des Deploys: Der Wechsel ist atomar, die App lädt nie halb kopierte DLLs (500er beim Deploy am 21.10.2026).
  WEBSITE_RUN_FROM_PACKAGE: '1'
  // Echte Client-IP und HTTPS aus den Headern des Frontends von App Service. Ohne das zählen alle Besucher im Rate Limit als derselbe Client.
  ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'true'
  // Nach einem Start fragt App Service zuerst die Startseite ab: Datenbank, EF und Rendering sind warm, bevor Besucher kommen.
  WEBSITE_WARMUP_PATH: '/de'
  Database__PrepareOnStartup: 'true'
  Accounts__Admins__0__DisplayName: adminDisplayName
  // Selbstregistrierung offen (Entscheid Marc, 23.10.2026). Schutz: Honeypot, Zeitfalle, Rate Limit, Bestätigung per Mail.
  Accounts__RegistrationOpen: 'true'
  Mail__Host: 'asmtp.mail.hostpoint.ch'
  Mail__Port: '587'
  Mail__UseTls: 'true'
  Mail__From: mailFrom
  PublicSite__BaseUri: publicSiteBaseUri
  Contact__TrustCloudflareHeader: string(restrictToCloudflare)
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
    keyVaultReferenceIdentity: 'SystemAssigned'
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
      ipSecurityRestrictionsDefaultAction: restrictToCloudflare ? 'Deny' : 'Allow'
      ipSecurityRestrictions: restrictToCloudflare ? cloudflareRules : []
      scmIpSecurityRestrictionsUseMain: false
      scmIpSecurityRestrictionsDefaultAction: 'Allow'
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
output planId string = plan.id
output name string = site.name
output defaultHostName string = site.properties.defaultHostName
output principalId string = site.identity.principalId

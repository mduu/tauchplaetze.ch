// Storage Account für Blobs und Queues (infrastructure.md, Blob Storage, Storage Queues).
// Zugriff nur über Entra (Managed Identity), kein Schlüssel. Öffentlich lesbar ist nur der Container media.

param location string
param name string

var privateContainers = [
  'media-pending'
  'revisions'
  'forecast-raw'
  'models'
  'locks'
  'content-export'
]

var queues = [
  'mail-jobs'
  'mail-jobs-deadletter'
]

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: true
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

// Soft Delete 14 Tage für Blobs und Container: Ein Fehlgriff löscht die Revisionen (History) sonst endgültig.
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
  }
}

resource media 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'media'
  properties: {
    publicAccess: 'Blob'
  }
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [for container in privateContainers: {
  parent: blobService
  name: container
  properties: {
    publicAccess: 'None'
  }
}]

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource mailQueues 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = [for queue in queues: {
  parent: queueService
  name: queue
}]

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'forecast-raw-to-cool'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['forecast-raw/']
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
          }
        }
      ]
    }
  }
}

output name string = account.name
output id string = account.id
output blobEndpoint string = account.properties.primaryEndpoints.blob
output queueEndpoint string = account.properties.primaryEndpoints.queue

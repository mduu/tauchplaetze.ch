// Key Vault mit RBAC. App Service löst die Key-Vault-Referenzen der App Settings mit der Managed Identity auf.
// Bicep legt keine Secrets an: Ein Deploy würde sonst gesetzte Werte überschreiben. Marc setzt sie einmal (doc/go-live.md).

param location string
param name string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
  }
}

output name string = vault.name
output id string = vault.id
output uri string = vault.properties.vaultUri

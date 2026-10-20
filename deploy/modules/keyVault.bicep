// Key Vault mit RBAC. Die App liest die Secrets beim Start (Konfiguration `KeyVault:Uri`).
// Bicep legt keine Secrets an: Ein Deploy würde sonst gesetzte Werte überschreiben. Marc setzt sie einmal (doc/go-live.md).
// Der Key `dataprotection` schützt die Schlüssel der Data Protection im Blob Storage.

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

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

output name string = vault.name
output id string = vault.id
output uri string = vault.properties.vaultUri
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri

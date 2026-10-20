// Werte für prod. Was persönlich ist (Object ID, E-Mail), kommt aus Umgebungsvariablen und nie ins Repository.
//   export SQL_ADMIN_LOGIN="…" SQL_ADMIN_OBJECT_ID="…" ALERT_EMAIL="…"
// In GitHub Actions setzt infra.yml diese Variablen aus Repository-Variablen und Secrets.
using './main.bicep'

param location = 'switzerlandnorth'
param sqlPlan = 'Basic'
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN')
param sqlAdminObjectId = readEnvironmentVariable('SQL_ADMIN_OBJECT_ID')
param sqlAdminPrincipalType = readEnvironmentVariable('SQL_ADMIN_PRINCIPAL_TYPE', 'User')
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')
param budgetStartDate = '2026-11-01'
param monthlyBudget = 25
param mailFrom = 'tauchplaetze.ch <noreply@tauchplaetze.ch>'
param publicSiteBaseUri = 'https://tauchplaetze.ch'
param mediaPublicBaseUri = ''
param adminDisplayName = 'Marc'

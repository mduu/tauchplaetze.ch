// Workbook «Betrieb und Nutzung» (infrastructure.md, «Betrieb»): Requests, Fehler, Jobs, Datenbank, Kosten, Menschen und Bots.
// Die Abfragen stehen in deploy/workbooks/operations.json. Platzhalter __…_ID__ ersetzt das Modul durch die Ressourcen-IDs.

param location string
param insightsId string
param workspaceId string
param webAppId string
param planId string
param databaseId string

var template = loadTextContent('../workbooks/operations.json')
var withWorkspace = replace(template, '__WORKSPACE_ID__', workspaceId)
var withInsights = replace(withWorkspace, '__INSIGHTS_ID__', insightsId)
var withWebApp = replace(withInsights, '__WEBAPP_ID__', webAppId)
var withPlan = replace(withWebApp, '__PLAN_ID__', planId)
var withDatabase = replace(withPlan, '__DATABASE_ID__', databaseId)
var serialized = replace(withDatabase, '__RESOURCE_GROUP_ID__', resourceGroup().id)

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: guid(resourceGroup().id, 'workbook-operations')
  location: location
  kind: 'shared'
  properties: {
    displayName: 'tauchplaetze.ch – Betrieb und Nutzung'
    category: 'workbook'
    sourceId: toLower(insightsId)
    serializedData: serialized
  }
}

output id string = workbook.id

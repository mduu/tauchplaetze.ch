// Log Analytics und Application Insights. Tageslimit 0.15 GB (rund 4.5 GB im Monat, unter dem Gratisvolumen von 5 GB), 30 Tage.
// IP-Adressen speichert Application Insights nicht (Standard, nicht ändern; infrastructure.md).

param location string
param logsName string
param insightsName string

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.15')
    }
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: insightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    IngestionMode: 'LogAnalytics'
    RetentionInDays: 30
    DisableIpMasking: false
  }
}

output connectionString string = insights.properties.ConnectionString
output workspaceId string = logs.id

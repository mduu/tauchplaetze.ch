// Alerts aus infrastructure.md (Betrieb): Health Check, Prognose-Lauf gescheitert oder ausgeblieben, Mail in der Dead-Letter-Queue, Tageslimit der Logs,
// Kontingent des Free Offer. Log-Alerts prüfen stündlich (günstigste Stufe). Die Texte der Logs kommen aus ForecastWorker und ProcessMailQueue.

param actionGroupName string
param alertEmail string
param webAppId string
param workspaceId string
param databaseId string
param freeOffer bool

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'global'
  properties: {
    groupShortName: 'tauchplatz'
    enabled: true
    emailReceivers: [
      {
        name: 'operator'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource health 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-health-check'
  location: 'global'
  properties: {
    description: 'Der Health Check von App Service (/alive) schlägt fehl.'
    severity: 1
    enabled: true
    scopes: [webAppId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'health'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'HealthCheckStatus'
          metricNamespace: 'Microsoft.Web/sites'
          operator: 'LessThan'
          threshold: 100
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource freeAmount 'Microsoft.Insights/metricAlerts@2018-03-01' = if (freeOffer) {
  name: 'alert-sql-free-amount'
  location: 'global'
  properties: {
    description: 'Vom Free Offer bleiben weniger als 10 000 vCore-Sekunden. Bei 0 pausiert die Datenbank bis Monatsende.'
    severity: 2
    enabled: true
    scopes: [databaseId]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'free'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'free_amount_remaining'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          operator: 'LessThan'
          threshold: 10000
          timeAggregation: 'Minimum'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

var logAlerts = [
  {
    name: 'alert-forecast-failed'
    description: 'Ein Prognose-Lauf ist gescheitert.'
    severity: 2
    window: 'PT1H'
    // Ein Fehler mit Exception landet in AppExceptions, der Text steht dort nur in Properties.OriginalFormat.
    query: 'union AppTraces, AppExceptions | where tostring(Properties.OriginalFormat) == "Forecast run failed" or Message matches regex @"^Forecast run [0-9a-fA-F-]+: Failed"'
    operator: 'GreaterThan'
    threshold: 0
  }
  {
    name: 'alert-forecast-missing'
    description: 'Seit 30 Stunden kein brauchbarer Prognose-Lauf.'
    severity: 2
    window: 'P2D'
    query: 'AppTraces | where TimeGenerated > ago(30h) | where Message matches regex @"^Forecast run [0-9a-fA-F-]+: (Succeeded|PartiallySucceeded)"'
    operator: 'LessThan'
    threshold: 1
  }
  {
    name: 'alert-mail-deadletter'
    description: 'Eine Mail liegt in der Dead-Letter-Queue (ADR-010).'
    severity: 3
    window: 'PT1H'
    query: 'AppTraces | where Message has "moved to deadletter" or Message has "awaiting investigation"'
    operator: 'GreaterThan'
    threshold: 0
  }
  {
    name: 'alert-log-daily-cap'
    description: 'Log Analytics hat das Tageslimit erreicht und nimmt bis zum Reset (14:00 UTC) keine Logs mehr an. Die übrigen Log-Alerts sind bis dahin blind.'
    severity: 2
    window: 'PT1H'
    query: '_LogOperation | where Category == "Ingestion" and Detail has "OverQuota"'
    operator: 'GreaterThan'
    threshold: 0
  }
]

resource logRules 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [for rule in logAlerts: {
  name: rule.name
  location: resourceGroup().location
  properties: {
    description: rule.description
    severity: rule.severity
    enabled: true
    scopes: [workspaceId]
    evaluationFrequency: 'PT1H'
    windowSize: rule.window
    criteria: {
      allOf: [
        {
          query: rule.query
          timeAggregation: 'Count'
          operator: rule.operator
          threshold: rule.threshold
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}]

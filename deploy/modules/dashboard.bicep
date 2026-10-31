// Dashboard «tauchplaetze.ch» im Azure-Portal: Einstieg mit den wichtigsten Kacheln, Details im Workbook (workbook.bicep).
// Raster 12 Spalten breit. Die Abfragen haben ihren Zeitraum fix in der Query (IsQueryContainTimeRange).

param location string
param workspaceId string
param webAppId string
param planId string
param databaseId string
param workbookId string

var workbookUrl = '${environment().portal}/#@${tenant().tenantId}/resource${workbookId}/workbook'

var requestsQuery = '''
AppRequests
| where TimeGenerated > ago(3d)
| where not(Name startswith "Microsoft.AspNetCore.Components.Server")
| summarize Requests = sum(ItemCount) by bin(TimeGenerated, 1h), Status = strcat(substring(ResultCode, 0, 1), "xx")
'''

var latencyQuery = '''
AppRequests
| where TimeGenerated > ago(3d)
| where Name startswith "GET /"
| summarize P50 = percentilew(DurationMs, ItemCount, 50), P95 = percentilew(DurationMs, ItemCount, 95) by bin(TimeGenerated, 1h)
'''

var kpiQuery = '''
let capMb = 150.0;
let resetAt = bin(now() - 14h, 1d) + 14h;
let logs = toscalar(Usage | where TimeGenerated >= resetAt and IsBillable | summarize sum(Quantity));
AppRequests
| where TimeGenerated > ago(24h)
| where not(Name startswith "Microsoft.AspNetCore.Components.Server")
| summarize Requests = sum(ItemCount), P95 = percentilew(DurationMs, ItemCount, 95), Fehler = sumif(ItemCount, toint(ResultCode) >= 500)
| project Kennzahl = pack_array("Requests (24 h)", "Latenz P95 (ms)", "Fehler 5xx (24 h)", "Fehlerquote 5xx (%)", "Log-Limit belegt (%)"),
          Wert = pack_array(Requests, round(P95, 0), Fehler, round(100.0 * Fehler / max_of(Requests, 1), 2), round(100 * logs / capMb, 0))
| mv-expand Kennzahl to typeof(string), Wert to typeof(real)
'''

var forecastQuery = '''
AppTraces
| where TimeGenerated > ago(7d)
| where Message matches regex @"^Forecast run [0-9a-fA-F-]+: "
| extend RunId = tostring(Properties.RunId)
| extend Start = unixtime_milliseconds_todatetime(tolong(strcat("0x", substring(replace_string(RunId, "-", ""), 0, 12))))
| project Ende = TimeGenerated, Status = tostring(Properties.Status), ["Dauer (min)"] = round((TimeGenerated - Start) / 1m, 0), ["Plätze"] = toint(Properties.Sites)
| union (AppExceptions | where TimeGenerated > ago(7d) | where tostring(Properties.CategoryName) endswith "ForecastWorker" | project Ende = TimeGenerated, Status = "Failed")
| order by Ende desc
| take 6
'''

var dischargeQuery = '''
union AppTraces, AppExceptions
| where TimeGenerated > ago(24h)
| where tostring(Properties.CategoryName) endswith "DischargeWorker"
| extend Status = case(
    Type == "AppExceptions" or tostring(Properties.Failed) == "True", "Fehlgeschlagen",
    tostring(Properties.Skipped) == "True", "Übersprungen",
    toint(Properties.Missing) > 0, "Teilweise",
    "Erfolgreich")
| summarize ["Läufe (24 h)"] = count(), Zuletzt = max(TimeGenerated) by Status
'''

var pagesByVisitor = '''
let browserMax = AppRequests
| where TimeGenerated > ago(30d) and isempty(SyntheticSource)
| extend Family = extract(@"^(.+?) \d", 1, ClientBrowser), Major = toint(extract(@" (\d+)", 1, ClientBrowser))
| summarize MaxMajor = max(Major) by Family;
let pageViews = AppRequests
| where TimeGenerated > ago(14d)
| where Name startswith "GET /" and ResultCode == "200"
| where not(Name has_any ("/not-found", "/robots.txt", "/sitemap", "/health", "/alive", "/api/", "/admin", "/platform"));
let bulk = pageViews
| summarize Aufrufe = sum(ItemCount), Anmeldungen = sumif(ItemCount, Name has "/account/sign-in") by Block = bin(TimeGenerated, 1h), ClientBrowser, ClientOS, ClientModel
| where Aufrufe > 120 or Anmeldungen > 10
| project Block, ClientBrowser, ClientOS, ClientModel, Massenabruf = 1;
pageViews
| extend Family = extract(@"^(.+?) \d", 1, ClientBrowser), Major = toint(extract(@" (\d+)", 1, ClientBrowser))
| lookup kind=leftouter browserMax on Family
| extend Block = bin(TimeGenerated, 1h)
| lookup kind=leftouter bulk on Block, ClientBrowser, ClientOS, ClientModel
| project-away Block
| extend Besucher = case(
    isnotempty(SyntheticSource)
      or ClientBrowser in ("Other", "")
      or ClientBrowser matches regex @"(?i)bot|crawl|spider|slurp|curl|wget|python|go-http|java|okhttp|axios|node|headless|scrapy|http.?client|libwww|php|ruby|perl|postman|insomnia", "Bot (erkannt)",
    ClientOS matches regex @"^Android [1-7](\.|$)"
      or ClientOS matches regex @"^iOS (\d|1[0-4])\."
      or (Family in ("Chrome", "Chrome Mobile", "Chrome Mobile iOS", "Edge", "Firefox", "Firefox Mobile", "Opera") and Major < MaxMajor - 12)
      or isnotnull(Massenabruf), "Bot (Verdacht)",
    "Mensch")
'''

var visitorsQuery = '${pagesByVisitor}\n| summarize Seitenaufrufe = sum(ItemCount) by bin(TimeGenerated, 1d), Besucher\n'

var contentQuery = '''
| extend m = extract_all(@"^/(de|fr|it|en)/(tauchplaetze|dive-sites|sites-de-plongee|siti-di-immersione|gewaesser|waters|eaux|acque)/([^/?]+)$", tostring(parse_url(Url).Path))
| where array_length(m) > 0 and Besucher == "Mensch"
| extend Art = iff(tostring(m[0][1]) in ("gewaesser", "waters", "eaux", "acque"), "Gewässer", "Tauchplatz"), Slug = tostring(m[0][2])
'''

var sitesQuery = '${pagesByVisitor}${contentQuery}| where Art == "Tauchplatz"\n| summarize Aufrufe = sum(ItemCount) by Tauchplatz = Slug\n| top 15 by Aufrufe desc\n'

var watersQuery = '${pagesByVisitor}${contentQuery}| where Art == "Gewässer"\n| summarize Aufrufe = sum(ItemCount) by [\'Gewässer\'] = Slug\n| top 15 by Aufrufe desc\n'

func logsPart(id string, query string, title string, subtitle string, control string, chart string, dimensions object) object => {
  type: 'Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsDashboardPart'
  inputs: [
    { name: 'resourceTypeMode', isOptional: true, value: 'workspace' }
    { name: 'ComponentId', isOptional: true, value: id }
    { name: 'Scope', value: { resourceIds: [id] }, isOptional: true }
    { name: 'PartId', value: guid(id, title), isOptional: true }
    { name: 'Version', value: '2.0', isOptional: true }
    { name: 'TimeRange', value: 'P1D', isOptional: true }
    { name: 'DashboardId', isOptional: true }
    { name: 'DraftRequestParameters', isOptional: true }
    { name: 'Query', value: query, isOptional: true }
    { name: 'ControlType', value: control, isOptional: true }
    { name: 'SpecificChart', value: chart, isOptional: true }
    { name: 'PartTitle', value: title, isOptional: true }
    { name: 'PartSubTitle', value: subtitle, isOptional: true }
    { name: 'Dimensions', value: dimensions, isOptional: true }
    { name: 'LegendOptions', value: { isEnabled: true, position: 'Bottom' }, isOptional: true }
    { name: 'IsQueryContainTimeRange', value: true, isOptional: true }
  ]
  settings: {
    content: {
      PartTitle: title
      PartSubTitle: subtitle
    }
  }
}

func metricPart(id string, namespace string, title string, metrics array) object => {
  type: 'Extension/HubsExtension/PartType/MonitorChartPart'
  inputs: [
    {
      name: 'options'
      isOptional: true
      value: {
        chart: {
          metrics: map(metrics, m => {
            resourceMetadata: { id: id }
            name: m.name
            aggregationType: m.aggregation
            namespace: namespace
            metricVisualization: { displayName: m.label }
          })
          title: title
          titleKind: 2
          visualization: {
            chartType: 2
            legendVisualization: { isVisible: true, position: 2, hideSubtitle: false }
            axisVisualization: { x: { isVisible: true, axisType: 2 }, y: { isVisible: true, axisType: 1 } }
          }
          timespan: { relative: { duration: 259200000 }, showUTCTime: false, grain: 1 }
        }
      }
    }
    { name: 'sharedTimeRange', isOptional: true }
  ]
  settings: {}
}

func markdownPart(title string, content string) object => {
  type: 'Extension/HubsExtension/PartType/MarkdownPart'
  inputs: []
  settings: {
    content: {
      settings: {
        content: content
        title: title
        subtitle: ''
        markdownSource: 1
        markdownUri: ''
      }
    }
  }
}

var noDimensions = {}

var overview = 'Betrieb und Nutzung auf einen Blick. Details, Kosten, Heatmap und Geräte im Workbook:\n\n**[Workbook öffnen](${workbookUrl})**\n\n- Betrieb: Requests, Latenz, Fehler 5xx\n- Jobs: Prognose-Lauf, Abfluss-Job, externe Quellen\n- Datenbank: DTU, Speicher, SQL-Calls\n- Kosten: pro Tag und Monat, Budget, Log-Volumen\n- Nutzung: Menschen und Bots, Uhrzeit, Geräte'

var parts = [
  {
    position: { x: 0, y: 0, colSpan: 4, rowSpan: 4 }
    metadata: markdownPart('tauchplaetze.ch', overview)
  }
  {
    position: { x: 4, y: 0, colSpan: 4, rowSpan: 4 }
    metadata: logsPart(workspaceId, kpiQuery, 'Kennzahlen', 'letzte 24 Stunden', 'AnalyticsGrid', 'Table', noDimensions)
  }
  {
    position: { x: 8, y: 0, colSpan: 4, rowSpan: 4 }
    metadata: logsPart(workspaceId, forecastQuery, 'Prognose-Läufe', 'täglich 04:30 und 13:00', 'AnalyticsGrid', 'Table', noDimensions)
  }
  {
    position: { x: 0, y: 4, colSpan: 6, rowSpan: 4 }
    metadata: logsPart(workspaceId, requestsQuery, 'Requests nach Status', '3 Tage, pro Stunde', 'FrameControlChart', 'StackedColumn', {
      xAxis: { name: 'TimeGenerated', type: 'datetime' }
      yAxis: [{ name: 'Requests', type: 'long' }]
      splitBy: [{ name: 'Status', type: 'string' }]
      aggregation: 'Sum'
    })
  }
  {
    position: { x: 6, y: 4, colSpan: 6, rowSpan: 4 }
    metadata: logsPart(workspaceId, latencyQuery, 'Latenz der Seiten (ms)', '3 Tage, pro Stunde', 'FrameControlChart', 'Line', {
      xAxis: { name: 'TimeGenerated', type: 'datetime' }
      yAxis: [{ name: 'P50', type: 'real' }, { name: 'P95', type: 'real' }]
      splitBy: []
      aggregation: 'Sum'
    })
  }
  {
    position: { x: 0, y: 8, colSpan: 4, rowSpan: 4 }
    metadata: metricPart(databaseId, 'microsoft.sql/servers/databases', 'Datenbank: DTU und Speicher (%)', [
      { name: 'dtu_consumption_percent', aggregation: 3, label: 'DTU max' }
      { name: 'dtu_consumption_percent', aggregation: 4, label: 'DTU Ø' }
      { name: 'storage_percent', aggregation: 3, label: 'Speicher' }
    ])
  }
  {
    position: { x: 4, y: 8, colSpan: 4, rowSpan: 4 }
    metadata: metricPart(planId, 'microsoft.web/serverfarms', 'App Service: CPU und Arbeitsspeicher (%)', [
      { name: 'CpuPercentage', aggregation: 4, label: 'CPU' }
      { name: 'MemoryPercentage', aggregation: 4, label: 'Arbeitsspeicher' }
    ])
  }
  {
    position: { x: 8, y: 8, colSpan: 4, rowSpan: 4 }
    metadata: logsPart(workspaceId, dischargeQuery, 'Abfluss-Job', 'alle 30 Minuten, letzte 24 Stunden', 'AnalyticsGrid', 'Table', noDimensions)
  }
  {
    position: { x: 0, y: 12, colSpan: 8, rowSpan: 4 }
    metadata: logsPart(workspaceId, visitorsQuery, 'Seitenaufrufe: Menschen und Bots', '14 Tage, pro Tag', 'FrameControlChart', 'StackedColumn', {
      xAxis: { name: 'TimeGenerated', type: 'datetime' }
      yAxis: [{ name: 'Seitenaufrufe', type: 'long' }]
      splitBy: [{ name: 'Besucher', type: 'string' }]
      aggregation: 'Sum'
    })
  }
  {
    position: { x: 8, y: 12, colSpan: 4, rowSpan: 4 }
    metadata: metricPart(webAppId, 'microsoft.web/sites', 'Health Check (%) und HTTP 5xx', [
      { name: 'HealthCheckStatus', aggregation: 2, label: 'Health Check min' }
      { name: 'Http5xx', aggregation: 1, label: 'HTTP 5xx' }
    ])
  }
  {
    position: { x: 0, y: 16, colSpan: 6, rowSpan: 5 }
    metadata: logsPart(workspaceId, sitesQuery, 'Meistbesuchte Tauchplätze', 'Menschen, 14 Tage, alle Sprachen', 'AnalyticsGrid', 'Table', noDimensions)
  }
  {
    position: { x: 6, y: 16, colSpan: 6, rowSpan: 5 }
    metadata: logsPart(workspaceId, watersQuery, 'Meistbesuchte Gewässer', 'Menschen, 14 Tage, alle Sprachen', 'AnalyticsGrid', 'Table', noDimensions)
  }
]

resource dashboard 'Microsoft.Portal/dashboards@2022-12-01-preview' = {
  name: 'dash-tauchplaetze-prod'
  location: location
  tags: {
    'hidden-title': 'tauchplaetze.ch'
  }
  properties: {
    lenses: [
      {
        order: 0
        parts: parts
      }
    ]
    metadata: {
      model: {
        timeRange: {
          value: { relative: { duration: 24, timeUnit: 1 } }
          type: 'MsPortalFx.Composition.Configuration.ValueTypes.TimeRange'
        }
      }
    }
  }
}

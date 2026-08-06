export interface IngestionFilterRuleVM {
  enabled: boolean
  field: string
  operator: string
  value: string
  action: string
}

export interface IngestionSettingsVM {
  enabled: boolean
  endpoint: string | null
  rules: IngestionFilterRuleVM[]
}

export interface StorageSettingsVM {
  provider: string
  connectionString: string | null
}

export interface QuerySettingsVM {
  maxTracesPerRequest: number
  maxSpansPerTrace: number
  defaultLookbackSeconds: number
  slowThresholdMs: number
  dashboardWindowSeconds: number
  dashboardBuckets: number
}

export interface AlertsSettingsVM {
  enabled: boolean
  maxAlerts: number
  errorRateDownThreshold: number
}

export interface MetricsSettingsVM {
  enabled: boolean
  defaultBucketSeconds: number
  maxSeriesPoints: number
}

export interface ServiceMapSettingsVM {
  windowSeconds: number
}

export interface CollectorsSettingsVM {
  windowSeconds: number
  staleMinutes: number
}

export interface LiveSettingsVM {
  streamIntervalSeconds: number
}

export interface RetentionSettingsVM {
  enabled: boolean
  retentionDays: number
  cleanupIntervalHours: number
}

export interface TracingSettingsVM {
  ingestion: IngestionSettingsVM
  storage: StorageSettingsVM
  query: QuerySettingsVM
  alerts: AlertsSettingsVM
  metrics: MetricsSettingsVM
  serviceMap: ServiceMapSettingsVM
  collectors: CollectorsSettingsVM
  live: LiveSettingsVM
  retention: RetentionSettingsVM
}

export interface TracingSettingsPatch {
  ingestionEnabled?: boolean
  ingestionEndpoint?: string
  ingestionRules?: IngestionFilterRuleVM[]
  queryMaxTracesPerRequest?: number
  queryMaxSpansPerTrace?: number
  queryDefaultLookbackSeconds?: number
  querySlowThresholdMs?: number
  alertsEnabled?: boolean
  alertsMaxAlerts?: number
  alertsErrorRateDownThreshold?: number
  metricsEnabled?: boolean
  metricsDefaultBucketSeconds?: number
  metricsMaxSeriesPoints?: number
  serviceMapWindowSeconds?: number
  collectorsWindowSeconds?: number
  collectorsStaleMinutes?: number
  liveStreamIntervalSeconds?: number
  retentionEnabled?: boolean
  retentionDays?: number
  retentionCleanupIntervalHours?: number
}

export const FILTER_FIELDS = ['ServiceName', 'SpanName', 'HttpPath', 'StatusCode', 'Kind']
export const FILTER_OPERATORS = ['Equals', 'NotEquals', 'Contains', 'Regex']
export const FILTER_ACTIONS = ['Drop', 'Keep']

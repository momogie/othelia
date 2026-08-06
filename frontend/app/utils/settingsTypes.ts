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
  defaultLookbackSeconds: number
  slowThresholdMs: number
  dashboardWindowSeconds: number
  dashboardBuckets: number
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
  retention: RetentionSettingsVM
}

export interface TracingSettingsPatch {
  ingestionEnabled?: boolean
  ingestionEndpoint?: string
  ingestionRules?: IngestionFilterRuleVM[]
  queryMaxTracesPerRequest?: number
  queryDefaultLookbackSeconds?: number
  querySlowThresholdMs?: number
  retentionEnabled?: boolean
  retentionDays?: number
  retentionCleanupIntervalHours?: number
}

export const FILTER_FIELDS = ['ServiceName', 'SpanName', 'HttpPath', 'StatusCode', 'Kind']
export const FILTER_OPERATORS = ['Equals', 'NotEquals', 'Contains', 'Regex']
export const FILTER_ACTIONS = ['Drop', 'Keep']

export interface ApiTraceSummary {
  traceId: string
  name: string
  rootServiceName: string
  startTime: string
  duration: string
  spanCount: number
  status: string
  tags?: string[] | null
}

export interface ApiSpanEvent {
  name: string
  time: string
  attributes?: Record<string, string> | null
}

export interface ApiSpan {
  spanId: string
  parentSpanId?: string | null
  traceId: string
  name: string
  serviceName: string
  kind: string
  startTime: string
  duration: string
  status?: string | null
  attributes?: Record<string, string> | null
  events?: ApiSpanEvent[] | null
}

export interface ApiService {
  name: string
  version?: string | null
  environment?: string | null
  lastSeen: string
  totalTraces: number
  errorCount: number
}

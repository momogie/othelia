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
  resourceAttributes?: Record<string, string> | null
  events?: ApiSpanEvent[] | null
  links?: ApiSpanLink[] | null
}

export interface ApiSpanLink {
  traceId: string
  spanId: string
  attributes?: Record<string, string> | null
}

export interface ApiService {
  name: string
  version?: string | null
  environment?: string | null
  lastSeen: string
  totalTraces: number
  errorCount: number
}

export interface ApiDashboard {
  generatedAt: string
  windowSeconds: number
  buckets: number
  metrics: {
    totalRequests: number
    totalSpans: number
    errorRate: number
    p50Ms: number
    p99Ms: number
    throughputRps: number
    servicesTotal: number
    servicesUp: number
    servicesDown: number
  }
  throughput: { time: string; timestamp: string; value: number }[]
  services: ApiServiceHealth[]
  alerts: ApiAlert[]
}

export interface ApiServiceHealth {
  name: string
  version?: string | null
  environment?: string | null
  status: string
  rps: number
  errorRate: number
  p99Ms: number
  uptime: number
  lastSeen: string
  totalTraces: number
}

export interface ApiAlert {
  id: string
  level: string
  title: string
  message: string
  service: string
  time: string
  acknowledged: boolean
  traceId?: string | null
}

export interface ApiLog {
  timestamp: string
  serviceName: string
  serviceVersion?: string | null
  serviceEnvironment?: string | null
  severity: string
  body?: string | null
  traceId?: string | null
  spanId?: string | null
  attributes?: Record<string, string> | null
}

export interface ApiThroughputSeries {
  range: string
  windowSeconds: number
  bucketSeconds: number
  points: { time: string; timestamp: string; value: number }[]
}

export interface ApiMetricName {
  name: string
  unit?: string | null
  dataPoints: number
  lastSeen: string
}

export interface ApiMetricPoint {
  time: string
  timestamp: string
  value: number
}

export interface ApiMetricSeries {
  metric: string
  service?: string | null
  aggregation: string
  bucketSeconds: number
  points: ApiMetricPoint[]
}

export interface ApiServiceMapNode {
  name: string
  version?: string | null
  environment?: string | null
  totalTraces: number
  errorSpans: number
  lastSeen: string
}

export interface ApiServiceMapEdge {
  source: string
  target: string
  callCount: number
}

export interface ApiServiceMap {
  nodes: ApiServiceMapNode[]
  edges: ApiServiceMapEdge[]
}

export interface ApiCollector {
  name: string
  version?: string | null
  environment?: string | null
  running: boolean
  totalTraces: number
  errorSpans: number
  lastSeen: string
}

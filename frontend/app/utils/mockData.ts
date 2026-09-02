export interface Attr { key: string; val: string; ok?: boolean; err?: boolean }

export type TraceStatus = 'ok' | 'error' | 'slow'

export interface SpanVM {
  id: string
  name: string
  service: string
  depth: number
  offset: number
  durationMs: number
  status: TraceStatus
  statusMessage?: string | null
  attrs: Attr[]
  color: string
  barColor: string
  offsetPct: number
  widthPct: number
  duration: string
}

export interface LogVM { id: string; level: string; time: string; msg: string; span: string }

export interface EventVM { id: string; name: string; icon: string; color: string; offset: string; attrs: string }

export interface ServiceNodeVM { name: string; icon: string; latency: string }

export interface TraceVM {
  id: string
  rootSpan: string
  service: string
  method: string
  path: string
  statusCode: number
  status: TraceStatus
  errorMessage?: string | null
  duration: string
  durMs: number
  spans: number
  errorSpans: number
  serviceCount: number
  time: string
  wfWidth: number
  serviceNodes: ServiceNodeVM[]
  spanTree: SpanVM[]
  logs: LogVM[]
  events: EventVM[]
  httpAttrs: Attr[]
  resourceAttrs: Attr[]
  links?: { traceId: string; spanId: string; attrs: Attr[] }[]
}

export interface ServiceMapVM { name: string; icon: string; latency: string; status: TraceStatus }

export interface MetricVM { label: string; value: string; color: string; pct: number }

export interface RateVM { name: string; rps: number; pct: number; color: string }

export interface CollectorVM { name: string; active: boolean; attrs: Attr[] }

export const spanColors: Record<string, string> = {
  'api-gateway': '#4a9eff',
  'user-service': '#a855f7',
  'order-service': '#3ecf8e',
  'payment-service': '#f5a623',
  'inventory-service': '#ec4899',
  'notification-service': '#22d3ee',
  db: '#7c5cbf',
}

function makeSpanTree(totalMs: number, spans: Omit<SpanVM, 'color' | 'barColor' | 'offsetPct' | 'widthPct' | 'duration'>[]) {
  return spans.map((s) => ({
    ...s,
    color: spanColors[s.service] || '#9898b8',
    barColor:
      s.durationMs < 200 ? '#22c55e'
      : s.durationMs < 1000 ? '#eab308'
      : s.durationMs < 60000 ? '#ef4444'
      : '#7f1d1d',
    offsetPct: (s.offset / totalMs) * 100,
    widthPct: (s.durationMs / totalMs) * 100,
    duration: `${s.durationMs}ms`,
  }))
}

export const mockTraces: TraceVM[] = [
  {
    id: 'trace-7f3a2b1c',
    rootSpan: 'POST /api/orders',
    service: 'api-gateway',
    method: 'POST',
    path: '/api/v2/orders',
    statusCode: 201,
    status: 'ok',
    duration: '284ms',
    durMs: 284,
    spans: 12,
    errorSpans: 0,
    serviceCount: 4,
    time: '2s ago',
    wfWidth: 28,
    serviceNodes: [
      { name: 'api-gateway', icon: '🌐', latency: '12ms' },
      { name: 'order-service', icon: '📦', latency: '180ms' },
      { name: 'payment-service', icon: '💳', latency: '60ms' },
      { name: 'db', icon: '🗄️', latency: '32ms' },
    ],
    spanTree: makeSpanTree(284, [
      { id: 's1', name: 'POST /api/v2/orders', service: 'api-gateway', depth: 0, offset: 0, durationMs: 284, status: 'ok', attrs: [
        { key: 'http.method', val: 'POST' },
        { key: 'http.url', val: 'https://api.example.com/v2/orders' },
        { key: 'http.status_code', val: '201', ok: true },
        { key: 'net.peer.ip', val: '10.0.1.5' },
      ] },
      { id: 's2', name: 'authenticate_request', service: 'api-gateway', depth: 1, offset: 2, durationMs: 15, status: 'ok', attrs: [
        { key: 'auth.method', val: 'JWT' },
        { key: 'auth.user_id', val: 'usr_8x2kp' },
      ] },
      { id: 's3', name: 'validate_payload', service: 'api-gateway', depth: 1, offset: 18, durationMs: 8, status: 'ok', attrs: [
        { key: 'validation.schema', val: 'OrderCreateV2' },
        { key: 'validation.fields', val: '12' },
      ] },
      { id: 's4', name: 'create_order', service: 'order-service', depth: 1, offset: 28, durationMs: 195, status: 'ok', attrs: [
        { key: 'order.id', val: 'ord_9qm3x' },
        { key: 'order.items', val: '3' },
        { key: 'order.total', val: '199000' },
      ] },
      { id: 's5', name: 'check_inventory', service: 'inventory-service', depth: 2, offset: 32, durationMs: 45, status: 'ok', attrs: [
        { key: 'inventory.items_checked', val: '3' },
        { key: 'inventory.all_available', val: 'true', ok: true },
      ] },
      { id: 's6', name: 'db.SELECT inventory', service: 'db', depth: 3, offset: 35, durationMs: 38, status: 'ok', attrs: [
        { key: 'db.system', val: 'postgresql' },
        { key: 'db.operation', val: 'SELECT' },
        { key: 'db.rows_affected', val: '3' },
      ] },
      { id: 's7', name: 'process_payment', service: 'payment-service', depth: 2, offset: 80, durationMs: 100, status: 'ok', attrs: [
        { key: 'payment.provider', val: 'DOKU' },
        { key: 'payment.method', val: 'QRIS' },
        { key: 'payment.amount', val: '199000' },
      ] },
      { id: 's8', name: 'db.INSERT orders', service: 'db', depth: 2, offset: 185, durationMs: 28, status: 'ok', attrs: [
        { key: 'db.system', val: 'postgresql' },
        { key: 'db.operation', val: 'INSERT' },
        { key: 'db.rows_affected', val: '1' },
      ] },
      { id: 's9', name: 'send_notification', service: 'notification-service', depth: 2, offset: 218, durationMs: 50, status: 'ok', attrs: [
        { key: 'notification.channel', val: 'email+push' },
        { key: 'notification.template', val: 'order_confirmed' },
      ] },
    ]),
    logs: [
      { id: 'l1', level: 'INFO', time: '+0ms', msg: 'Order request received from usr_8x2kp', span: 'api-gateway' },
      { id: 'l2', level: 'DEBUG', time: '+2ms', msg: 'JWT validated, user authenticated', span: 'api-gateway' },
      { id: 'l3', level: 'INFO', time: '+28ms', msg: 'Creating order ord_9qm3x with 3 items', span: 'order-service' },
      { id: 'l4', level: 'DEBUG', time: '+35ms', msg: 'Checking inventory for 3 SKUs', span: 'inventory-service' },
      { id: 'l5', level: 'INFO', time: '+80ms', msg: 'Payment processing via DOKU QRIS', span: 'payment-service' },
      { id: 'l6', level: 'INFO', time: '+180ms', msg: 'Payment confirmed, inserting order', span: 'order-service' },
      { id: 'l7', level: 'INFO', time: '+284ms', msg: 'Order created successfully: ord_9qm3x', span: 'order-service' },
    ],
    events: [
      { id: 'e1', name: 'request.received', icon: '📥', color: '#4a9eff', offset: '0ms', attrs: 'method=POST path=/api/v2/orders' },
      { id: 'e2', name: 'auth.validated', icon: '🔐', color: '#3ecf8e', offset: '17ms', attrs: 'user_id=usr_8x2kp' },
      { id: 'e3', name: 'inventory.checked', icon: '📦', color: '#a855f7', offset: '73ms', attrs: 'items=3 all_available=true' },
      { id: 'e4', name: 'payment.initiated', icon: '💳', color: '#f5a623', offset: '80ms', attrs: 'provider=DOKU amount=199000' },
      { id: 'e5', name: 'payment.confirmed', icon: '✅', color: '#3ecf8e', offset: '180ms', attrs: 'txn_id=dku_9283' },
      { id: 'e6', name: 'order.created', icon: '🎉', color: '#3ecf8e', offset: '213ms', attrs: 'order_id=ord_9qm3x' },
    ],
    httpAttrs: [
      { key: 'http.method', val: 'POST' },
      { key: 'http.url', val: 'https://api.example.com/v2/orders' },
      { key: 'http.status_code', val: '201', ok: true },
      { key: 'http.request_content_length', val: '428' },
      { key: 'http.response_content_length', val: '312' },
      { key: 'http.scheme', val: 'https' },
      { key: 'http.flavor', val: '1.1' },
    ],
    resourceAttrs: [
      { key: 'service.name', val: 'api-gateway' },
      { key: 'service.version', val: '2.4.1' },
      { key: 'service.namespace', val: 'production' },
      { key: 'deployment.environment', val: 'production' },
      { key: 'host.name', val: 'api-gw-prod-02' },
      { key: 'telemetry.sdk.name', val: 'opentelemetry' },
      { key: 'telemetry.sdk.language', val: 'dotnet' },
      { key: 'telemetry.sdk.version', val: '1.8.0' },
    ],
  },
  {
    id: 'trace-9e1f4d2a',
    rootSpan: 'GET /api/users/profile',
    service: 'user-service',
    method: 'GET',
    path: '/api/v1/users/usr_8x2kp/profile',
    statusCode: 500,
    status: 'error',
    duration: '1.2s',
    durMs: 1200,
    spans: 8,
    errorSpans: 2,
    serviceCount: 2,
    time: '15s ago',
    wfWidth: 100,
    serviceNodes: [
      { name: 'api-gateway', icon: '🌐', latency: '5ms' },
      { name: 'user-service', icon: '👤', latency: '1190ms' },
      { name: 'db', icon: '🗄️', latency: '1150ms' },
    ],
    spanTree: makeSpanTree(1200, [
      { id: 's1', name: 'GET /api/v1/users/:id/profile', service: 'api-gateway', depth: 0, offset: 0, durationMs: 1200, status: 'error', attrs: [
        { key: 'http.method', val: 'GET' },
        { key: 'http.status_code', val: '500', err: true },
      ] },
      { id: 's2', name: 'get_user_profile', service: 'user-service', depth: 1, offset: 5, durationMs: 1190, status: 'error', attrs: [
        { key: 'user.id', val: 'usr_8x2kp' },
        { key: 'error.type', val: 'QueryTimeout', err: true },
      ] },
      { id: 's3', name: 'db.SELECT users (SLOW)', service: 'db', depth: 2, offset: 10, durationMs: 1150, status: 'error', attrs: [
        { key: 'db.system', val: 'postgresql' },
        { key: 'db.operation', val: 'SELECT' },
        { key: 'db.statement', val: 'SELECT * FROM users WHERE id=$1 AND...' },
        { key: 'error', val: 'query timeout exceeded 1000ms', err: true },
      ] },
    ]),
    logs: [
      { id: 'l1', level: 'INFO', time: '+0ms', msg: 'GET profile request for usr_8x2kp', span: 'api-gateway' },
      { id: 'l2', level: 'WARN', time: '+500ms', msg: 'Query running slow (>500ms), monitoring...', span: 'db' },
      { id: 'l3', level: 'ERROR', time: '+1000ms', msg: 'Query timeout exceeded threshold (1000ms)', span: 'db' },
      { id: 'l4', level: 'ERROR', time: '+1190ms', msg: 'Failed to fetch user profile: QueryTimeout', span: 'user-service' },
      { id: 'l5', level: 'ERROR', time: '+1200ms', msg: 'Internal server error returned to client', span: 'api-gateway' },
    ],
    events: [
      { id: 'e1', name: 'request.received', icon: '📥', color: '#4a9eff', offset: '0ms', attrs: 'GET /users/usr_8x2kp/profile' },
      { id: 'e2', name: 'db.query.slow', icon: '⚠️', color: '#f5a623', offset: '500ms', attrs: 'threshold=500ms elapsed=500ms' },
      { id: 'e3', name: 'db.query.timeout', icon: '⏱️', color: '#e74c3c', offset: '1000ms', attrs: 'max_duration=1000ms' },
      { id: 'e4', name: 'exception', icon: '💥', color: '#e74c3c', offset: '1190ms', attrs: 'type=QueryTimeout message=...' },
    ],
    httpAttrs: [
      { key: 'http.method', val: 'GET' },
      { key: 'http.url', val: 'https://api.example.com/v1/users/usr_8x2kp/profile' },
      { key: 'http.status_code', val: '500', err: true },
    ],
    resourceAttrs: [
      { key: 'service.name', val: 'user-service' },
      { key: 'service.version', val: '1.9.3' },
      { key: 'deployment.environment', val: 'production' },
    ],
  },
  {
    id: 'trace-3b8c7e9f',
    rootSpan: 'PUT /api/inventory/stock',
    service: 'inventory-service',
    method: 'PUT',
    path: '/api/v1/inventory/items/sku_4523',
    statusCode: 200,
    status: 'slow',
    duration: '892ms',
    durMs: 892,
    spans: 9,
    errorSpans: 0,
    serviceCount: 3,
    time: '1m ago',
    wfWidth: 75,
    serviceNodes: [
      { name: 'api-gateway', icon: '🌐', latency: '8ms' },
      { name: 'inventory-service', icon: '📦', latency: '750ms' },
      { name: 'db', icon: '🗄️', latency: '700ms' },
    ],
    spanTree: makeSpanTree(892, [
      { id: 's1', name: 'PUT /api/v1/inventory/items/:sku', service: 'api-gateway', depth: 0, offset: 0, durationMs: 892, status: 'ok', attrs: [] },
      { id: 's2', name: 'update_stock', service: 'inventory-service', depth: 1, offset: 8, durationMs: 880, status: 'ok', attrs: [] },
      { id: 's3', name: 'db.UPDATE stock (SLOW)', service: 'db', depth: 2, offset: 15, durationMs: 700, status: 'ok', attrs: [
        { key: 'db.rows_affected', val: '1' },
        { key: 'db.execution_time', val: '700ms' },
      ] },
      { id: 's4', name: 'invalidate_cache', service: 'inventory-service', depth: 2, offset: 720, durationMs: 45, status: 'ok', attrs: [] },
      { id: 's5', name: 'publish_event', service: 'inventory-service', depth: 2, offset: 770, durationMs: 120, status: 'ok', attrs: [
        { key: 'messaging.destination', val: 'inventory.stock.updated' },
        { key: 'messaging.system', val: 'rabbitmq' },
      ] },
    ]),
    logs: [
      { id: 'l1', level: 'INFO', time: '+0ms', msg: 'Stock update request for sku_4523', span: 'inventory-service' },
      { id: 'l2', level: 'WARN', time: '+715ms', msg: 'DB update took 700ms, approaching threshold', span: 'db' },
      { id: 'l3', level: 'INFO', time: '+720ms', msg: 'Cache invalidated for sku_4523', span: 'inventory-service' },
      { id: 'l4', level: 'INFO', time: '+892ms', msg: 'Stock updated successfully', span: 'inventory-service' },
    ],
    events: [
      { id: 'e1', name: 'request.received', icon: '📥', color: '#4a9eff', offset: '0ms', attrs: 'PUT /inventory/sku_4523' },
      { id: 'e2', name: 'db.query.slow', icon: '⚠️', color: '#f5a623', offset: '715ms', attrs: 'duration=700ms' },
      { id: 'e3', name: 'cache.invalidated', icon: '🗑️', color: '#ec4899', offset: '720ms', attrs: 'key=inv:sku_4523' },
      { id: 'e4', name: 'event.published', icon: '📤', color: '#22d3ee', offset: '770ms', attrs: 'topic=inventory.stock.updated' },
    ],
    httpAttrs: [
      { key: 'http.method', val: 'PUT' },
      { key: 'http.status_code', val: '200', ok: true },
    ],
    resourceAttrs: [
      { key: 'service.name', val: 'inventory-service' },
      { key: 'deployment.environment', val: 'production' },
    ],
  },
  {
    id: 'trace-2d5e8f1a',
    rootSpan: 'DELETE /api/sessions/:id',
    service: 'user-service',
    method: 'DELETE',
    path: '/api/v1/sessions/sess_77xyz',
    statusCode: 204,
    status: 'ok',
    duration: '42ms',
    durMs: 42,
    spans: 4,
    errorSpans: 0,
    serviceCount: 2,
    time: '3m ago',
    wfWidth: 10,
    serviceNodes: [
      { name: 'api-gateway', icon: '🌐', latency: '3ms' },
      { name: 'user-service', icon: '👤', latency: '35ms' },
    ],
    spanTree: makeSpanTree(42, [
      { id: 's1', name: 'DELETE /api/v1/sessions/:id', service: 'api-gateway', depth: 0, offset: 0, durationMs: 42, status: 'ok', attrs: [] },
      { id: 's2', name: 'revoke_session', service: 'user-service', depth: 1, offset: 3, durationMs: 38, status: 'ok', attrs: [] },
      { id: 's3', name: 'db.DELETE sessions', service: 'db', depth: 2, offset: 6, durationMs: 30, status: 'ok', attrs: [] },
    ]),
    logs: [
      { id: 'l1', level: 'INFO', time: '+0ms', msg: 'Session revoke request sess_77xyz', span: 'user-service' },
      { id: 'l2', level: 'INFO', time: '+42ms', msg: 'Session revoked successfully', span: 'user-service' },
    ],
    events: [
      { id: 'e1', name: 'session.revoked', icon: '🔒', color: '#a855f7', offset: '38ms', attrs: 'session_id=sess_77xyz' },
    ],
    httpAttrs: [{ key: 'http.method', val: 'DELETE' }, { key: 'http.status_code', val: '204', ok: true }],
    resourceAttrs: [{ key: 'service.name', val: 'user-service' }],
  },
  {
    id: 'trace-6a9b3c4e',
    rootSpan: 'PATCH /api/orders/:id/status',
    service: 'order-service',
    method: 'PATCH',
    path: '/api/v2/orders/ord_9qm3x/status',
    statusCode: 404,
    status: 'error',
    duration: '18ms',
    durMs: 18,
    spans: 3,
    errorSpans: 1,
    serviceCount: 2,
    time: '5m ago',
    wfWidth: 8,
    serviceNodes: [
      { name: 'api-gateway', icon: '🌐', latency: '2ms' },
      { name: 'order-service', icon: '📦', latency: '15ms' },
    ],
    spanTree: makeSpanTree(18, [
      { id: 's1', name: 'PATCH /api/v2/orders/:id/status', service: 'api-gateway', depth: 0, offset: 0, durationMs: 18, status: 'error', attrs: [
        { key: 'http.status_code', val: '404', err: true },
      ] },
      { id: 's2', name: 'update_order_status', service: 'order-service', depth: 1, offset: 2, durationMs: 16, status: 'error', attrs: [
        { key: 'order.id', val: 'ord_9qm3x' },
        { key: 'error', val: 'Order not found', err: true },
      ] },
    ]),
    logs: [
      { id: 'l1', level: 'WARN', time: '+2ms', msg: 'Order ord_9qm3x not found in database', span: 'order-service' },
      { id: 'l2', level: 'INFO', time: '+18ms', msg: '404 returned to client', span: 'api-gateway' },
    ],
    events: [
      { id: 'e1', name: 'order.not_found', icon: '❓', color: '#e74c3c', offset: '2ms', attrs: 'order_id=ord_9qm3x' },
    ],
    httpAttrs: [{ key: 'http.method', val: 'PATCH' }, { key: 'http.status_code', val: '404', err: true }],
    resourceAttrs: [{ key: 'service.name', val: 'order-service' }],
  },
]

export const allLogsData: LogVM[] = [
  { id: 'gl1', level: 'ERROR', time: '09:41:05', msg: '[user-service] QueryTimeout on SELECT users: duration exceeded 1000ms', span: 'user-service' },
  { id: 'gl2', level: 'ERROR', time: '09:41:06', msg: '[api-gateway] 500 Internal Server Error returned for GET /users/usr_8x2kp/profile', span: 'api-gateway' },
  { id: 'gl3', level: 'WARN', time: '09:40:30', msg: '[inventory-service] DB update took 700ms (threshold: 500ms)', span: 'inventory-service' },
  { id: 'gl4', level: 'INFO', time: '09:40:28', msg: '[order-service] Order ord_9qm3x created, items=3 total=199000', span: 'order-service' },
  { id: 'gl5', level: 'DEBUG', time: '09:40:20', msg: '[api-gateway] JWT validated for usr_8x2kp in 15ms', span: 'api-gateway' },
  { id: 'gl6', level: 'INFO', time: '09:40:18', msg: '[payment-service] QRIS payment confirmed txn_id=dku_9283', span: 'payment-service' },
  { id: 'gl7', level: 'WARN', time: '09:39:55', msg: '[notification-service] Email delivery delayed 2.1s (SMTP queue full)', span: 'notification-service' },
  { id: 'gl8', level: 'ERROR', time: '09:39:40', msg: '[order-service] 404 Order ord_9qm3x not found for PATCH request', span: 'order-service' },
  { id: 'gl9', level: 'DEBUG', time: '09:39:30', msg: '[db] Connection pool: 24/50 active connections', span: 'db' },
  { id: 'gl10', level: 'INFO', time: '09:39:20', msg: '[user-service] Session sess_77xyz revoked successfully', span: 'user-service' },
]

export const serviceMapData: ServiceMapVM[] = [
  { name: 'api-gateway', icon: '🌐', latency: '12ms', status: 'ok' },
  { name: 'user-service', icon: '👤', latency: '1.2s', status: 'error' },
  { name: 'order-service', icon: '📦', latency: '284ms', status: 'ok' },
  { name: 'payment-service', icon: '💳', latency: '100ms', status: 'ok' },
  { name: 'inventory-service', icon: '🎁', latency: '892ms', status: 'ok' },
  { name: 'db', icon: '🗄️', latency: '1150ms', status: 'error' },
]

export const svcMetricsData: MetricVM[] = [
  { label: 'Total RPS', value: '1,247', color: 'var(--blue)', pct: 75 },
  { label: 'Error Rate', value: '2.4%', color: 'var(--red)', pct: 24 },
  { label: 'p99 Latency', value: '1.24s', color: 'var(--yellow)', pct: 62 },
  { label: 'Apdex Score', value: '0.87', color: 'var(--green)', pct: 87 },
  { label: 'Active Spans', value: '4,830', color: 'var(--purple)', pct: 48 },
  { label: 'Throughput', value: '98.2%', color: 'var(--green)', pct: 98 },
]

export const globalMetricsData: MetricVM[] = [
  { label: 'Requests/sec', value: '1,247', color: 'var(--blue)', pct: 75 },
  { label: 'Error Rate', value: '2.4%', color: 'var(--red)', pct: 24 },
  { label: 'p50 Latency', value: '84ms', color: 'var(--green)', pct: 30 },
  { label: 'p99 Latency', value: '1.24s', color: 'var(--yellow)', pct: 62 },
]

export const serviceRatesData: RateVM[] = [
  { name: 'api-gateway', rps: 1247, pct: 100, color: 'var(--blue)' },
  { name: 'order-service', rps: 892, pct: 71, color: 'var(--green)' },
  { name: 'user-service', rps: 745, pct: 60, color: 'var(--purple)' },
  { name: 'payment-service', rps: 320, pct: 26, color: 'var(--yellow)' },
  { name: 'inventory-service', rps: 285, pct: 23, color: 'var(--pink)' },
  { name: 'notification-service', rps: 180, pct: 14, color: 'var(--cyan)' },
]

export interface DashboardMetric {
  label: string
  value: string
  sub: string
  color: string
  icon: string
  change: string
  changeType: 'up' | 'down' | 'neutral'
}

export interface ThroughputPoint {
  time: string
  value: number
}

export interface ServiceHealthItem {
  name: string
  icon: string
  status: TraceStatus
  rps: number
  errorRate: number
  p99: string
  uptime: number
  version: string
}

export interface AlertItem {
  id: string
  level: 'error' | 'warning' | 'info'
  title: string
  message: string
  service: string
  time: string
  acknowledged: boolean
}

export const dashboardMetricsData: DashboardMetric[] = [
  { label: 'Total Requests', value: '2.4M', sub: 'last 24h', color: 'var(--blue)', icon: '📡', change: '+12.3%', changeType: 'up' },
  { label: 'Error Rate', value: '2.4%', sub: 'of total requests', color: 'var(--red)', icon: '⚠️', change: '-0.8%', changeType: 'down' },
  { label: 'p99 Latency', value: '1.24s', sub: 'across all services', color: 'var(--yellow)', icon: '⏱️', change: '+120ms', changeType: 'up' },
  { label: 'Active Spans', value: '4,830', sub: 'currently in-flight', color: 'var(--purple)', icon: '🔗', change: '+320', changeType: 'up' },
  { label: 'Services Up', value: '5/6', sub: 'healthy services', color: 'var(--green)', icon: '✅', change: '0', changeType: 'neutral' },
  { label: 'Throughput', value: '1,247', sub: 'requests/sec', color: 'var(--cyan)', icon: '🚀', change: '+8.1%', changeType: 'up' },
]

export const throughputTimeSeries: ThroughputPoint[] = [
  { time: '00:00', value: 820 }, { time: '01:00', value: 780 }, { time: '02:00', value: 650 },
  { time: '03:00', value: 520 }, { time: '04:00', value: 480 }, { time: '05:00', value: 510 },
  { time: '06:00', value: 690 }, { time: '07:00', value: 920 }, { time: '08:00', value: 1100 },
  { time: '09:00', value: 1247 }, { time: '10:00', value: 1180 }, { time: '11:00', value: 1150 },
  { time: '12:00', value: 980 }, { time: '13:00', value: 1050 }, { time: '14:00', value: 1120 },
  { time: '15:00', value: 1090 }, { time: '16:00', value: 1020 }, { time: '17:00', value: 960 },
  { time: '18:00', value: 870 }, { time: '19:00', value: 790 }, { time: '20:00', value: 740 },
  { time: '21:00', value: 700 }, { time: '22:00', value: 680 }, { time: '23:00', value: 820 },
]

export const serviceHealthData: ServiceHealthItem[] = [
  { name: 'api-gateway', icon: '🌐', status: 'ok', rps: 1247, errorRate: 0.1, p99: '12ms', uptime: 99.99, version: '2.4.1' },
  { name: 'user-service', icon: '👤', status: 'error', rps: 745, errorRate: 8.2, p99: '1.2s', uptime: 99.85, version: '1.9.3' },
  { name: 'order-service', icon: '📦', status: 'ok', rps: 892, errorRate: 0.3, p99: '284ms', uptime: 99.97, version: '3.1.0' },
  { name: 'payment-service', icon: '💳', status: 'ok', rps: 320, errorRate: 0.0, p99: '100ms', uptime: 100.0, version: '2.0.5' },
  { name: 'inventory-service', icon: '🎁', status: 'slow', rps: 285, errorRate: 0.0, p99: '892ms', uptime: 99.92, version: '1.4.2' },
  { name: 'notification-service', icon: '🔔', status: 'ok', rps: 180, errorRate: 0.5, p99: '45ms', uptime: 99.95, version: '1.2.0' },
  { name: 'db', icon: '🗄️', status: 'error', rps: 0, errorRate: 15.4, p99: '1150ms', uptime: 98.2, version: 'PostgreSQL 16.4' },
]

export const recentAlertsData: AlertItem[] = [
  { id: 'a1', level: 'error', title: 'QueryTimeout detected', message: 'user-service SELECT query exceeded 1000ms threshold on db SELECT users', service: 'user-service', time: '15s ago', acknowledged: false },
  { id: 'a2', level: 'error', title: 'HTTP 500 spike', message: 'GET /api/v1/users/:id/profile returning 500 (3 consecutive failures)', service: 'api-gateway', time: '15s ago', acknowledged: false },
  { id: 'a3', level: 'warning', title: 'High DB latency', message: 'inventory-service db UPDATE taking 700ms (threshold: 500ms)', service: 'inventory-service', time: '1m ago', acknowledged: false },
  { id: 'a4', level: 'warning', title: 'Email delivery delay', message: 'notification-service SMTP queue full, delivery delayed 2.1s', service: 'notification-service', time: '5m ago', acknowledged: true },
  { id: 'a5', level: 'info', title: 'Collector restarted', message: 'otel-collector-dev-01 manually stopped', service: 'collectors', time: '2h ago', acknowledged: true },
]

export const collectorsData: CollectorVM[] = [
  {
    name: 'otel-collector-prod-01',
    active: true,
    attrs: [
      { key: 'endpoint', val: '0.0.0.0:4317 (gRPC)' },
      { key: 'exporters', val: 'jaeger, prometheus, logging' },
      { key: 'receivers', val: 'otlp, zipkin, jaeger' },
      { key: 'processors', val: 'batch, memory_limiter, resource' },
      { key: 'uptime', val: '14d 6h 22m' },
      { key: 'spans_received', val: '2,840,192 / 24h' },
    ],
  },
  {
    name: 'otel-collector-staging-01',
    active: true,
    attrs: [
      { key: 'endpoint', val: '0.0.0.0:4318 (HTTP)' },
      { key: 'exporters', val: 'jaeger, logging' },
      { key: 'uptime', val: '3d 11h 05m' },
      { key: 'spans_received', val: '124,880 / 24h' },
    ],
  },
  {
    name: 'otel-collector-dev-01',
    active: false,
    attrs: [
      { key: 'endpoint', val: '0.0.0.0:4317 (gRPC)' },
      { key: 'last_seen', val: '2 hours ago' },
      { key: 'status', val: 'Stopped — manual shutdown' },
    ],
  },
]

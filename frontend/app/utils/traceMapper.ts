import type { Attr, EventVM, ServiceNodeVM, SpanVM, TraceVM, TraceStatus } from './mockData'
import type { ApiSpan, ApiSpanEvent, ApiTraceSummary } from './otelTypes'

const COLOR_PALETTE = [
  '#4a9eff', '#a855f7', '#3ecf8e', '#f5a623', '#ec4899', '#22d3ee', '#7c5cbf',
  '#f97316', '#84cc16', '#06b6d4', '#8b5cf6', '#ef4444',
]

function hashCode(name: string): number {
  let h = 0
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0
  return h
}

function colorForService(name: string): string {
  return COLOR_PALETTE[hashCode(name) % COLOR_PALETTE.length] ?? '#9898b8'
}

export function timespanToMs(ts: string): number {
  if (!ts) return 0
  const m = /^(\d+):(\d+):(\d+)(?:\.(\d{1,7}))?$/.exec(ts)
  if (!m) return 0
  const [, h = '0', mi = '0', s = '0', frac] = m
  let ms = (+h * 3600 + +mi * 60 + +s) * 1000
  if (frac) ms += Number(`0.${frac}`) * 1000
  return Math.round(ms)
}

export function formatDuration(ms: number): string {
  if (!ms || ms < 1) return '<1ms'
  if (ms < 1000) return `${Math.round(ms)}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(ms < 10000 ? 2 : 1)}s`
  return `${(ms / 60000).toFixed(1)}min`
}

function toStatus(status?: string | null, httpCode?: number): TraceStatus {
  if (status === 'Error' || (httpCode && httpCode >= 400)) return 'error'
  return 'ok'
}

function formatTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function extractMethod(name: string): string {
  const m = /^([A-Z]{2,8})\s+\S/.exec(name.trim())
  return m?.[1] ?? ''
}

function extractPath(name: string): string {
  const trimmed = name.trim()
  const idx = trimmed.indexOf(' ')
  return idx > 0 ? trimmed.slice(idx + 1) : trimmed
}

function attrsToArray(attrs?: Record<string, string> | null): Attr[] {
  return Object.entries(attrs ?? {}).map(([key, val]) => ({
    key,
    val,
    err: /exception|error/i.test(key) || /error/i.test(val),
    ok: /status/.test(key) && /^ok$/i.test(val),
  }))
}

export function mapTraceSummary(api: ApiTraceSummary): TraceVM {
  const durMs = timespanToMs(api.duration)
  return {
    id: api.traceId,
    rootSpan: api.name || '(untitled)',
    service: api.rootServiceName || 'unknown',
    method: extractMethod(api.name),
    path: extractPath(api.name),
    statusCode: 0,
    status: api.status === 'error' ? 'error' : 'ok',
    duration: formatDuration(durMs),
    durMs,
    spans: api.spanCount,
    errorSpans: api.status === 'error' ? 1 : 0,
    serviceCount: api.tags?.length || 1,
    time: formatTime(api.startTime),
    wfWidth: Math.min(100, Math.max(3, Math.round(Math.log2(1 + durMs / 10) * 12))),
    serviceNodes: [],
    spanTree: [],
    logs: [],
    events: [],
    httpAttrs: [],
    resourceAttrs: [],
  }
}

export function buildSpanTree(apiSpans: ApiSpan[]): SpanVM[] {
  if (!apiSpans.length) return []

  const byId = new Map(apiSpans.map((s) => [s.spanId, s]))
  const starts = apiSpans.map((s) => new Date(s.startTime).getTime())
  const ends = apiSpans.map((s, i) => (starts[i] ?? 0) + timespanToMs(s.duration))
  const traceStart = Math.min(...starts)
  const total = Math.max(...ends) - traceStart

  const depthCache = new Map<string, number>()
  function depthOf(id: string, seen = new Set<string>()): number {
    if (depthCache.has(id)) return depthCache.get(id)!
    if (seen.has(id)) return 0
    seen.add(id)
    const span = byId.get(id)
    let depth = 0
    if (span?.parentSpanId) depth = depthOf(span.parentSpanId, seen) + 1
    depthCache.set(id, depth)
    return depth
  }

  return apiSpans
    .map((s) => {
      const start = new Date(s.startTime).getTime()
      const durMs = timespanToMs(s.duration)
      const httpCode = Number(
        s.attributes?.['http.response.status_code'] ?? s.attributes?.['http.status_code'] ?? 0,
      ) || undefined
      const color = colorForService(s.serviceName)
      return {
        id: s.spanId,
        name: s.name,
        service: s.serviceName,
        depth: depthOf(s.spanId),
        offset: Math.max(0, start - traceStart),
        durationMs: durMs,
        status: toStatus(s.status, httpCode),
        attrs: attrsToArray(s.attributes),
        color,
        offsetPct: total ? (Math.max(0, start - traceStart) / total) * 100 : 0,
        widthPct: total ? (durMs / total) * 100 : 100,
        duration: formatDuration(durMs),
      }
    })
    .sort((a, b) => a.offset - b.offset || a.depth - b.depth)
}

function formatOffset(ev: ApiSpanEvent, span: ApiSpan): string {
  const evT = new Date(ev.time).getTime()
  const st = new Date(span.startTime).getTime()
  return formatDuration(Math.max(0, evT - st))
}

export function collectEvents(apiSpans: ApiSpan[]): EventVM[] {
  const events: EventVM[] = []
  for (const span of apiSpans) {
    for (const ev of span.events ?? []) {
      events.push({
        id: `${span.spanId}-${ev.name}`,
        name: ev.name,
        icon: ev.name === 'exception' ? '⚠️' : '⚡',
        color: ev.name === 'exception' ? '#ef4444' : '#4a9eff',
        offset: formatOffset(ev, span),
        attrs: Object.entries(ev.attributes ?? {}).map(([k, v]) => `${k}=${v}`).join(' · '),
      })
    }
  }
  return events
}

function iconForService(name: string): string {
  const lower = name.toLowerCase()
  if (lower.includes('db') || lower.includes('sql')) return '🗄️'
  if (lower.includes('cache') || lower.includes('redis')) return '⚡'
  if (lower.includes('mq') || lower.includes('rabbit') || lower.includes('kafka')) return '📨'
  if (lower.includes('gateway') || lower.includes('api')) return '🌐'
  return '📡'
}

export function collectServiceNodes(apiSpans: ApiSpan[]): ServiceNodeVM[] {
  const seen: string[] = []
  for (const s of apiSpans) {
    if (!seen.includes(s.serviceName)) seen.push(s.serviceName)
  }
  return seen.map((name) => {
    const spans = apiSpans.filter((s) => s.serviceName === name)
    const latency = spans.reduce((sum, s) => sum + timespanToMs(s.duration), 0)
    return { name, icon: iconForService(name), latency: formatDuration(latency) }
  })
}

export function enrichTrace(trace: TraceVM, apiSpans: ApiSpan[]): TraceVM {
  const spanTree = buildSpanTree(apiSpans)
  const root = apiSpans.find((s) => !s.parentSpanId) ?? apiSpans[0]
  const httpCode = Number(
    root?.attributes?.['http.response.status_code'] ?? root?.attributes?.['http.status_code'] ?? 0,
  ) || 0

  return {
    ...trace,
    status: spanTree.some((s) => s.status === 'error') ? 'error' : 'ok',
    errorSpans: spanTree.filter((s) => s.status === 'error').length,
    serviceCount: collectServiceNodes(apiSpans).length,
    statusCode: httpCode,
    spanTree,
    events: collectEvents(apiSpans),
    serviceNodes: collectServiceNodes(apiSpans),
    httpAttrs: attrsToArray(root?.attributes),
    resourceAttrs: [],
    logs: [],
  }
}

import type {
  CollectorVM,
  MetricVM,
  RateVM,
  ServiceMapVM,
  TraceVM,
} from '../utils/mockData'
import {
  collectorsData,
  globalMetricsData,
  serviceMapData,
  serviceRatesData,
  svcMetricsData,
} from '../utils/mockData'
import type { ApiService, ApiSpan, ApiTraceSummary } from '~/utils/otelTypes'
import { enrichTrace, mapTraceSummary } from '~/utils/traceMapper'

export interface TraceFilterState {
  service: string
  status: string
  name: string
  nameNot: boolean
  path: string
  pathNot: boolean
  attributes: string
  traceId: string
  minDurationMs: number | null
  maxDurationMs: number | null
  from: string | null
  to: string | null
}

export const DEFAULT_PAGE_SIZE = 50

export function useTracing() {
  const traces = useState<TraceVM[]>('traces', () => [])
  const total = useState<number>('tracesTotal', () => 0)
  const serviceMap = useState<ServiceMapVM[]>('serviceMap', () => [...serviceMapData])
  const svcMetrics = useState<MetricVM[]>('svcMetrics', () => [...svcMetricsData])
  const globalMetrics = useState<MetricVM[]>('globalMetrics', () => [...globalMetricsData])
  const serviceRates = useState<RateVM[]>('serviceRates', () => [...serviceRatesData])
  const collectors = useState<CollectorVM[]>('collectors', () => [...collectorsData])
  const loading = useState<boolean>('tracingLoading', () => false)
  const loadingMore = useState<boolean>('tracingLoadingMore', () => false)
  const services = useState<ApiService[]>('tracingServices', () => [])
  const filter = useState<TraceFilterState>('tracingFilter', () => ({
    service: 'all',
    status: 'all',
    name: '',
    nameNot: false,
    path: '',
    pathNot: false,
    attributes: '',
    traceId: '',
    minDurationMs: null,
    maxDurationMs: null,
    from: null,
    to: null,
  }))

  const serviceList = computed(() => [...new Set(traces.value.map((t) => t.service))])
  const errorLogs = computed(() => traces.value.reduce((sum, t) => sum + (t.errorSpans || 0), 0))
  const hasMore = computed(() => traces.value.length < total.value)

  function toQuery(offset: number): Record<string, string | number> {
    const q: Record<string, string | number> = { limit: DEFAULT_PAGE_SIZE, offset }
    const f = filter.value
    if (f.service && f.service !== 'all') q.service = f.service
    if (f.status && f.status !== 'all') q.status = f.status
    if (f.name.trim()) q[f.nameNot ? 'nameNot' : 'name'] = f.name.trim()
    if (f.path.trim()) q[f.pathNot ? 'pathNot' : 'path'] = f.path.trim()
    if (f.attributes.trim()) q.attributes = f.attributes.trim()
    if (f.traceId.trim()) q.traceId = f.traceId.trim()
    if (f.minDurationMs != null) q.minDurationMs = f.minDurationMs
    if (f.maxDurationMs != null) q.maxDurationMs = f.maxDurationMs
    if (f.from) q.from = f.from
    if (f.to) q.to = f.to
    return q
  }

  async function fetchPage(offset: number) {
    const res = await $fetch.raw<ApiTraceSummary[]>('/api/traces', { query: toQuery(offset) })
    const items = res._data ?? []
    const count = Number(res.headers.get('x-total-count') ?? items.length)
    return { items, count }
  }

  async function refresh() {
    loading.value = true
    try {
      const { items, count } = await fetchPage(0)
      traces.value = (items ?? []).map(mapTraceSummary)
      total.value = count
    } catch (e) {
      console.error('Failed to load traces', e)
      traces.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }

  async function loadMore() {
    if (loadingMore.value || !hasMore.value) return
    loadingMore.value = true
    try {
      const { items } = await fetchPage(traces.value.length)
      const known = new Set(traces.value.map((t) => t.id))
      const fresh = (items ?? []).map(mapTraceSummary).filter((t) => !known.has(t.id))
      traces.value = [...traces.value, ...fresh]
    } catch (e) {
      console.error('Failed to load more traces', e)
    } finally {
      loadingMore.value = false
    }
  }

  async function loadServices() {
    if (services.value.length) return
    try {
      services.value = (await $fetch<ApiService[]>('/api/services')) ?? []
    } catch (e) {
      console.error('Failed to load services', e)
      services.value = []
    }
  }

  async function loadSpans(traceId: string): Promise<ApiSpan[]> {
    const data = await $fetch<ApiSpan[]>(`/api/traces/${encodeURIComponent(traceId)}/spans`)
    return data ?? []
  }

  async function findTrace(id: string): Promise<TraceVM | null> {
    let trace = traces.value.find((t) => t.id === id) ?? null
    if (!trace) {
      try {
        const list = await $fetch<ApiTraceSummary[]>(
          `/api/traces?traceId=${encodeURIComponent(id)}&limit=1&from=${encodeURIComponent(new Date(Date.now() - 7 * 86400000).toISOString())}`,
        )
        const first = list?.[0]
        if (!first) return null
        trace = mapTraceSummary(first)
      } catch {
        return null
      }
    }
    if (trace.spanTree.length) return trace
    try {
      const spans = await loadSpans(id)
      const enriched = enrichTrace(trace, spans)
      traces.value = traces.value.map((t) => (t.id === id ? enriched : t))
      return enriched
    } catch (e) {
      console.error('Failed to load spans', e)
      return trace
    }
  }

  const { selectedService } = useServiceScope()
  watch(selectedService, (svc) => {
    filter.value.service = svc
    refresh()
  })

  return {
    traces,
    serviceMap,
    svcMetrics,
    globalMetrics,
    serviceRates,
    collectors,
    loading,
    loadingMore,
    total,
    hasMore,
    services,
    serviceList,
    errorLogs,
    filter,
    refresh,
    loadMore,
    loadServices,
    findTrace,
  }
}

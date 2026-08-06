import type {
  CollectorVM,
  LogVM,
  MetricVM,
  RateVM,
  ServiceMapVM,
  TraceVM,
} from '../utils/mockData'
import {
  allLogsData,
  collectorsData,
  globalMetricsData,
  serviceMapData,
  serviceRatesData,
  svcMetricsData,
} from '../utils/mockData'
import type { ApiSpan, ApiTraceSummary } from '~/utils/otelTypes'
import { enrichTrace, mapTraceSummary } from '~/utils/traceMapper'

export function useTracing() {
  const traces = useState<TraceVM[]>('traces', () => [])
  const allLogs = useState<LogVM[]>('logs', () => [...allLogsData])
  const serviceMap = useState<ServiceMapVM[]>('serviceMap', () => [...serviceMapData])
  const svcMetrics = useState<MetricVM[]>('svcMetrics', () => [...svcMetricsData])
  const globalMetrics = useState<MetricVM[]>('globalMetrics', () => [...globalMetricsData])
  const serviceRates = useState<RateVM[]>('serviceRates', () => [...serviceRatesData])
  const collectors = useState<CollectorVM[]>('collectors', () => [...collectorsData])
  const loading = useState<boolean>('tracingLoading', () => false)

  const serviceList = computed(() => [...new Set(traces.value.map((t) => t.service))])
  const errorLogs = computed(() => traces.value.reduce((sum, t) => sum + (t.errorSpans || 0), 0))

  async function refresh() {
    loading.value = true
    try {
      const data = await $fetch<ApiTraceSummary[]>('/api/traces?limit=100')
      traces.value = (data ?? []).map(mapTraceSummary)
    } catch (e) {
      console.error('Failed to load traces', e)
      traces.value = []
    } finally {
      loading.value = false
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
        const list = await $fetch<ApiTraceSummary[]>(`/api/traces?traceId=${encodeURIComponent(id)}&limit=1`)
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

  return {
    traces,
    allLogs,
    serviceMap,
    svcMetrics,
    globalMetrics,
    serviceRates,
    collectors,
    loading,
    serviceList,
    errorLogs,
    refresh,
    findTrace,
  }
}

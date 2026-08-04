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
  mockTraces,
  serviceMapData,
  serviceRatesData,
  svcMetricsData,
} from '../utils/mockData'

export function useTracing() {
  const traces = useState<TraceVM[]>('traces', () => [...mockTraces])
  const allLogs = useState<LogVM[]>('logs', () => [...allLogsData])
  const serviceMap = useState<ServiceMapVM[]>('serviceMap', () => [...serviceMapData])
  const svcMetrics = useState<MetricVM[]>('svcMetrics', () => [...svcMetricsData])
  const globalMetrics = useState<MetricVM[]>('globalMetrics', () => [...globalMetricsData])
  const serviceRates = useState<RateVM[]>('serviceRates', () => [...serviceRatesData])
  const collectors = useState<CollectorVM[]>('collectors', () => [...collectorsData])
  const loading = useState<boolean>('tracingLoading', () => false)

  const serviceList = computed(() => [...new Set(traces.value.map((t) => t.service))])
  const errorLogs = computed(() => allLogs.value.filter((l) => l.level === 'ERROR').length)

  async function refresh() {
    loading.value = true
    try {
      const data = await $fetch<unknown[]>('/api/traces?limit=100')
      if (Array.isArray(data) && data.length) {
        traces.value = data as TraceVM[]
      }
    } catch {
      traces.value = [...mockTraces]
    } finally {
      loading.value = false
    }
  }

  function findTrace(id: string) {
    return traces.value.find((t) => t.id === id)
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

import type { ApiLog, ApiService } from '~/utils/otelTypes'

export interface LogFilterState {
  service: string
  severity: string
  search: string
  traceId: string
  from: string | null
  to: string | null
}

export const LOG_PAGE_SIZE = 50

export function useLogs() {
  const logs = useState<ApiLog[]>('logs', () => [])
  const total = useState<number>('logsTotal', () => 0)
  const loading = useState<boolean>('logsLoading', () => false)
  const loadingMore = useState<boolean>('logsLoadingMore', () => false)
  const services = useState<ApiService[]>('logServices', () => [])
  const filter = useState<LogFilterState>('logFilter', () => ({
    service: 'all',
    severity: 'all',
    search: '',
    traceId: '',
    from: null,
    to: null,
  }))

  const hasMore = computed(() => logs.value.length < total.value)

  function toQuery(offset: number): Record<string, string | number> {
    const q: Record<string, string | number> = { limit: LOG_PAGE_SIZE, offset }
    const f = filter.value
    if (f.service && f.service !== 'all') q.service = f.service
    if (f.severity && f.severity !== 'all') q.severity = f.severity
    if (f.search.trim()) q.search = f.search.trim()
    if (f.traceId.trim()) q.traceId = f.traceId.trim()
    if (f.from) q.from = f.from
    if (f.to) q.to = f.to
    return q
  }

  async function fetchPage(offset: number) {
    const res = await $fetch.raw<ApiLog[]>('/api/logs', { query: toQuery(offset) })
    const items = res._data ?? []
    const count = Number(res.headers.get('x-total-count') ?? items.length)
    return { items, count }
  }

  async function refresh() {
    loading.value = true
    try {
      const { items, count } = await fetchPage(0)
      logs.value = items ?? []
      total.value = count
    } catch (e) {
      console.error('Failed to load logs', e)
      logs.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }

  async function loadMore() {
    if (loadingMore.value || !hasMore.value) return
    loadingMore.value = true
    try {
      const { items } = await fetchPage(logs.value.length)
      const known = new Set(logs.value.map((l) => `${l.traceId ?? ''}:${l.timestamp}:${l.body ?? ''}`))
      const fresh = (items ?? []).filter((l) => !known.has(`${l.traceId ?? ''}:${l.timestamp}:${l.body ?? ''}`))
      logs.value = [...logs.value, ...fresh]
    } catch (e) {
      console.error('Failed to load more logs', e)
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

  const { selectedService } = useServiceScope()
  watch(selectedService, (svc) => {
    filter.value.service = svc
    refresh()
  })

  return {
    logs,
    total,
    loading,
    loadingMore,
    hasMore,
    services,
    filter,
    refresh,
    loadMore,
    loadServices,
  }
}

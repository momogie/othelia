import type { ApiExpensiveQuery, ApiService } from '~/utils/otelTypes'

export interface QueryFilterState {
  service: string
  thresholdMs: number
  from: string | null
  to: string | null
  sort: string
}

export const QUERY_PAGE_SIZE = 50

export function useQueries() {
  const items = useState<ApiExpensiveQuery[]>('queriesItems', () => [])
  const total = useState<number>('queriesTotal', () => 0)
  const loading = useState<boolean>('queriesLoading', () => false)
  const loadingMore = useState<boolean>('queriesLoadingMore', () => false)
  const services = useState<ApiService[]>('queriesServices', () => [])
  const filter = useState<QueryFilterState>('queriesFilter', () => ({
    service: 'all',
    thresholdMs: 1000,
    from: null,
    to: null,
    sort: 'max_ms',
  }))

  const hasMore = computed(() => items.value.length < total.value)

  function toQuery(offset: number): Record<string, string | number> {
    const q: Record<string, string | number> = { limit: QUERY_PAGE_SIZE, offset }
    const f = filter.value
    if (f.service && f.service !== 'all') q.service = f.service
    if (f.thresholdMs > 0) q.thresholdMs = f.thresholdMs
    if (f.from) q.from = f.from
    if (f.to) q.to = f.to
    if (f.sort && f.sort !== 'max_ms') q.sort = f.sort
    return q
  }

  async function fetchPage(offset: number) {
    const res = await $fetch.raw<ApiExpensiveQuery[]>('/api/queries/expensive', { query: toQuery(offset) })
    const data = res._data ?? []
    const count = Number(res.headers.get('x-total-count') ?? data.length)
    return { items: data, count }
  }

  async function refresh() {
    loading.value = true
    try {
      const { items: data, count } = await fetchPage(0)
      items.value = data ?? []
      total.value = count
    } catch (e) {
      console.error('Failed to load expensive queries', e)
      items.value = []
      total.value = 0
    } finally {
      loading.value = false
    }
  }

  async function loadMore() {
    if (loadingMore.value || !hasMore.value) return
    loadingMore.value = true
    try {
      const { items: data } = await fetchPage(items.value.length)
      const known = new Set(items.value.map((q) => `${q.statement}:${q.serviceName}`))
      const fresh = (data ?? []).filter((q) => !known.has(`${q.statement}:${q.serviceName}`))
      items.value = [...items.value, ...fresh]
    } catch (e) {
      console.error('Failed to load more queries', e)
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
    items,
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

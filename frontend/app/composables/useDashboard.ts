import type {
  AlertItem,
  DashboardMetric,
  ServiceHealthItem,
  ThroughputPoint,
  TraceStatus,
} from '../utils/mockData'
import type { ApiAlert, ApiDashboard, ApiServiceHealth } from '../utils/otelTypes'
import { formatDuration } from '../utils/traceMapper'

function formatCount(n: number): string {
  const value = Math.round(n ?? 0)
  if (value < 1000) return String(value)
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`
  return `${(value / 1_000).toFixed(1)}K`
}

function relativeTime(iso: string): string {
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return iso
  const s = Math.max(0, Math.floor((Date.now() - t) / 1000))
  if (s < 60) return `${s}s ago`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

function iconForService(name: string): string {
  const lower = name.toLowerCase()
  if (lower.includes('db') || lower.includes('sql')) return '🗄️'
  if (lower.includes('cache') || lower.includes('redis')) return '⚡'
  if (lower.includes('mq') || lower.includes('rabbit') || lower.includes('kafka')) return '📨'
  if (lower.includes('gateway') || lower.includes('api')) return '🌐'
  return '📡'
}

function computeChange(points: ThroughputPoint[]) {
  if (points.length < 2) return { change: '', type: 'neutral' as const }
  const mid = Math.floor(points.length / 2)
  const first = points.slice(0, mid).reduce((sum, p) => sum + p.value, 0)
  const second = points.slice(mid).reduce((sum, p) => sum + p.value, 0)
  if (!first) return { change: '', type: 'neutral' as const }
  const pct = ((second - first) / first) * 100
  if (Math.abs(pct) < 0.05) return { change: '0%', type: 'neutral' as const }
  return {
    change: `${pct > 0 ? '+' : ''}${pct.toFixed(1)}%`,
    type: (pct > 0 ? 'up' : 'down') as 'up' | 'down' | 'neutral',
  }
}

function mapService(s: ApiServiceHealth): ServiceHealthItem {
  return {
    name: s.name,
    icon: iconForService(s.name),
    status: (s.status === 'error' || s.status === 'slow' ? s.status : 'ok') as TraceStatus,
    rps: Math.round(s.rps),
    errorRate: Number(s.errorRate.toFixed(2)),
    p99: s.p99Ms ? formatDuration(s.p99Ms) : '—',
    uptime: Number(s.uptime.toFixed(2)),
    version: s.version || '—',
  }
}

function mapAlert(a: ApiAlert): AlertItem {
  return {
    id: a.id,
    level: a.level === 'error' || a.level === 'warning' ? a.level : 'info',
    title: a.title,
    message: a.message,
    service: a.service,
    time: relativeTime(a.time),
    acknowledged: a.acknowledged,
  }
}

export function useDashboard() {
  const metrics = useState<DashboardMetric[]>('dashboardMetrics', () => [])
  const throughput = useState<ThroughputPoint[]>('throughput', () => [])
  const services = useState<ServiceHealthItem[]>('serviceHealth', () => [])
  const alerts = useState<AlertItem[]>('recentAlerts', () => [])
  const loading = useState<boolean>('dashboardLoading', () => false)
  const windowLabel = useState<string>('dashboardWindowLabel', () => '24h')

  const unacknowledgedAlerts = computed(() => alerts.value.filter((a) => !a.acknowledged))
  const servicesDown = computed(() => services.value.filter((s) => s.status === 'error'))
  const servicesSlow = computed(() => services.value.filter((s) => s.status === 'slow'))

  const maxThroughput = computed(() => Math.max(1, ...throughput.value.map((p) => p.value)))

  async function acknowledgeAlert(id: string) {
    try {
      await $fetch(`/api/alerts/${encodeURIComponent(id)}/ack`, { method: 'PATCH' })
      const alert = alerts.value.find((a) => a.id === id)
      if (alert) alert.acknowledged = true
    } catch (e) {
      console.error('Failed to acknowledge alert', e)
    }
  }

  function apply(d: ApiDashboard) {
    const hours = Math.max(1, Math.round((d.windowSeconds ?? 0) / 3600))
    const window = hours >= 24 ? '24h' : `${hours}h`
    windowLabel.value = window

    const tp = (d.throughput ?? []).map((p) => ({
      time: p.time,
      timestamp: p.timestamp,
      value: Math.round(p.value),
    }))
    const change = computeChange(tp)

    metrics.value = [
      {
        label: 'Total Requests',
        value: formatCount(d.metrics.totalRequests),
        sub: `last ${window}`,
        color: 'var(--blue)',
        icon: '📡',
        change: change.change,
        changeType: change.type,
      },
      {
        label: 'Error Rate',
        value: `${d.metrics.errorRate.toFixed(2)}%`,
        sub: 'of total requests',
        color: 'var(--red)',
        icon: '⚠️',
        change: '',
        changeType: 'neutral',
      },
      {
        label: 'p99 Latency',
        value: d.metrics.p99Ms ? formatDuration(d.metrics.p99Ms) : '—',
        sub: 'across all services',
        color: 'var(--yellow)',
        icon: '⏱️',
        change: '',
        changeType: 'neutral',
      },
      {
        label: 'Active Spans',
        value: formatCount(d.metrics.totalSpans),
        sub: `last ${window}`,
        color: 'var(--purple)',
        icon: '🔗',
        change: '',
        changeType: 'neutral',
      },
      {
        label: 'Services Up',
        value: d.metrics.servicesTotal ? `${d.metrics.servicesUp}/${d.metrics.servicesTotal}` : '—',
        sub: 'healthy services',
        color: 'var(--green)',
        icon: '✅',
        change: '',
        changeType: 'neutral',
      },
      {
        label: 'Throughput',
        value: formatCount(Math.round(d.metrics.throughputRps)),
        sub: 'requests/sec',
        color: 'var(--cyan)',
        icon: '🚀',
        change: change.change,
        changeType: change.type,
      },
    ]
    throughput.value = tp
    services.value = (d.services ?? []).map(mapService)
    alerts.value = (d.alerts ?? []).map(mapAlert)
  }

  async function refresh() {
    loading.value = true
    try {
      const { selectedService } = useServiceScope()
      const data = await $fetch<ApiDashboard>('/api/dashboard', {
        query: { service: selectedService.value === 'all' ? undefined : selectedService.value },
      })
      apply(data)
    } catch (e) {
      console.error('Failed to load dashboard', e)
      metrics.value = []
      throughput.value = []
      services.value = []
      alerts.value = []
    } finally {
      loading.value = false
    }
  }

  const { selectedService } = useServiceScope()
  watch(selectedService, () => refresh())

  return {
    metrics,
    throughput,
    services,
    alerts,
    loading,
    windowLabel,
    unacknowledgedAlerts,
    servicesDown,
    servicesSlow,
    maxThroughput,
    acknowledgeAlert,
    refresh,
  }
}

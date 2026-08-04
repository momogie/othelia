import type {
  AlertItem,
  DashboardMetric,
  ServiceHealthItem,
  ThroughputPoint,
} from '../utils/mockData'
import {
  dashboardMetricsData,
  recentAlertsData,
  serviceHealthData,
  throughputTimeSeries,
} from '../utils/mockData'

export function useDashboard() {
  const metrics = useState<DashboardMetric[]>('dashboardMetrics', () => [...dashboardMetricsData])
  const throughput = useState<ThroughputPoint[]>('throughput', () => [...throughputTimeSeries])
  const services = useState<ServiceHealthItem[]>('serviceHealth', () => [...serviceHealthData])
  const alerts = useState<AlertItem[]>('recentAlerts', () => [...recentAlertsData])

  const unacknowledgedAlerts = computed(() => alerts.value.filter((a) => !a.acknowledged))
  const servicesDown = computed(() => services.value.filter((s) => s.status === 'error'))
  const servicesSlow = computed(() => services.value.filter((s) => s.status === 'slow'))

  const maxThroughput = computed(() => Math.max(...throughput.value.map((p) => p.value)))

  function acknowledgeAlert(id: string) {
    const alert = alerts.value.find((a) => a.id === id)
    if (alert) alert.acknowledged = true
  }

  return {
    metrics,
    throughput,
    services,
    alerts,
    unacknowledgedAlerts,
    servicesDown,
    servicesSlow,
    maxThroughput,
    acknowledgeAlert,
  }
}

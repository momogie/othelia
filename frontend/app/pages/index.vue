<script setup lang="ts">
const {
  metrics,
  throughput,
  services,
  alerts,
  unacknowledgedAlerts,
  servicesDown,
  servicesSlow,
  loading,
  windowLabel,
  acknowledgeAlert,
  refresh: refreshDashboard,
} = useDashboard()

const { traces, refresh: refreshTraces } = useTracing()

const recentTraces = computed(() => traces.value.slice(0, 5))

const LIVE_INTERVAL_MS = 1000

const rangeOptions = ['1m', '5m', '15m', '30m', '1h']
const activeRange = ref('1m')
const rangeWindow = ref(60)
const rangeSeries = ref<{ time: string; timestamp: string; value: number }[]>([])

async function refreshRangeSeries() {
  try {
    const { selectedService } = useServiceScope()
    const d = await $fetch<{ windowSeconds: number; points: { time: string; timestamp: string; value: number }[] }>(
      `/api/dashboard/throughput?range=${activeRange.value}`,
      { query: { service: selectedService.value === 'all' ? undefined : selectedService.value } },
    )
    rangeWindow.value = d.windowSeconds || 60
    rangeSeries.value = (d.points ?? []).map((p) => ({
      time: p.time,
      timestamp: p.timestamp,
      value: Math.round(p.value),
    }))
  } catch (e) {
    console.error('Failed to load throughput series', e)
    rangeSeries.value = []
  }
}

const live = ref(true)
const ticking = ref(false)
let timer: ReturnType<typeof setInterval> | null = null

function tick() {
  if (ticking.value) return
  ticking.value = true
  Promise.allSettled([refreshDashboard(), refreshTraces(), refreshRangeSeries()]).finally(() => {
    ticking.value = false
  })
}

function startLive() {
  live.value = true
  if (timer) return
  timer = setInterval(tick, LIVE_INTERVAL_MS)
}

function stopLive() {
  live.value = false
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

function handleVisibility() {
  if (document.visibilityState === 'visible') startLive()
  else stopLive()
}

onMounted(() => {
  refreshDashboard()
  if (!traces.value.length) refreshTraces()
  refreshRangeSeries()
  startLive()
  document.addEventListener('visibilitychange', handleVisibility)
})

onBeforeUnmount(() => {
  stopLive()
  document.removeEventListener('visibilitychange', handleVisibility)
})

function statusColor(status: string) {
  if (status === 'error') return 'var(--red)'
  if (status === 'slow') return 'var(--yellow)'
  return 'var(--green)'
}

function alertLevelClass(level: string) {
  if (level === 'error') return 'alert-error'
  if (level === 'warning') return 'alert-warning'
  return 'alert-info'
}

function methodClass(method: string) {
  return `method-${method}`
}

function statusCodeClass(code: number) {
  if (code >= 500) return 'sc-5xx'
  if (code >= 400) return 'sc-4xx'
  return 'sc-2xx'
}
</script>

<template>
  <div class="dashboard">
    <div class="dash-header">
      <div style="display:flex;align-items:center;gap:10px">
        <h1 class="dash-title">Dashboard</h1>
        <span class="dash-subtitle">System overview for the last 24 hours</span>
      </div>
      <button class="live-btn" :class="{ paused: !live }" :title="live ? 'Pause live updates' : 'Resume live updates'" @click="live ? stopLive() : startLive()">
        <div class="live-dot" :class="{ dim: !live || loading }"></div>
        <span class="live-label">{{ live ? 'Live · 1s' : 'Paused' }}</span>
      </button>
    </div>

    <div class="dash-metrics-grid">
      <div v-for="m in metrics" :key="m.label" class="dash-metric-card">
        <div class="dmc-top">
          <span class="dmc-icon">{{ m.icon }}</span>
          <span v-if="m.change" class="dmc-change" :class="`dmc-${m.changeType}`">{{ m.change }}</span>
        </div>
        <div class="dmc-value" :style="{ color: m.color }">{{ m.value }}</div>
        <div class="dmc-label">{{ m.label }}</div>
        <div class="dmc-sub">{{ m.sub }}</div>
      </div>
    </div>

    <div class="dash-two-col">
      <div class="dash-col-main">

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">📈 Request Rate</span>
            <div class="time-range-group">
              <button
                v-for="r in rangeOptions"
                :key="r"
                class="tr-btn"
                :class="{ active: activeRange === r }"
                @click="activeRange = r; refreshRangeSeries()"
              >
                {{ r }}
              </button>
            </div>
          </div>
          <div v-if="rangeSeries.length" class="dash-chart">
            <LineChart
              :points="rangeSeries"
              unit="req"
              color="#4a9eff"
              animated
              :window-seconds="rangeWindow"
            />
          </div>
          <div v-else class="dash-empty">No request data in this range yet.</div>
        </div>

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">📡 Throughput (req/s)</span>
            <span class="dash-section-badge">{{ windowLabel }}</span>
          </div>
          <div v-if="throughput.length" class="dash-chart">
            <LineChart :points="throughput" unit="req" color="#7c5cbf" />
          </div>
          <div v-else class="dash-empty">No trace data in this window yet.</div>
        </div>

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">🕸️ Service Health</span>
            <span class="dash-section-badge">{{ services.length }} services</span>
          </div>
          <div class="svc-health-list">
            <template v-if="services.length">
              <div v-for="svc in services" :key="svc.name" class="svc-health-row">
                <div class="sh-left">
                  <span class="sh-status-dot" :style="{ background: statusColor(svc.status) }"></span>
                  <span class="sh-icon">{{ svc.icon }}</span>
                  <span class="sh-name">{{ svc.name }}</span>
                  <span class="sh-version">v{{ svc.version }}</span>
                </div>
                <div class="sh-metrics">
                  <span class="sh-metric">
                    <span class="sh-metric-label">RPS</span>
                    <span class="sh-metric-val" style="color:var(--blue)">{{ svc.rps.toLocaleString() }}</span>
                  </span>
                  <span class="sh-metric">
                    <span class="sh-metric-label">Errors</span>
                    <span class="sh-metric-val" :style="{ color: svc.errorRate > 5 ? 'var(--red)' : svc.errorRate > 0 ? 'var(--yellow)' : 'var(--green)' }">
                      {{ svc.errorRate }}%
                    </span>
                  </span>
                  <span class="sh-metric">
                    <span class="sh-metric-label">p99</span>
                    <span class="sh-metric-val">{{ svc.p99 }}</span>
                  </span>
                  <span class="sh-metric">
                    <span class="sh-metric-label">Uptime</span>
                    <span class="sh-metric-val" :style="{ color: svc.uptime < 99.9 ? 'var(--yellow)' : 'var(--green)' }">
                      {{ svc.uptime }}%
                    </span>
                  </span>
                </div>
              </div>
            </template>
            <div v-else class="dash-empty">No services observed in this window yet.</div>
          </div>
        </div>

      </div>

      <div class="dash-col-side">

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">🔔 Alerts</span>
            <span v-if="unacknowledgedAlerts.length" class="dash-alert-badge">{{ unacknowledgedAlerts.length }} new</span>
          </div>
          <div class="alert-list">
            <template v-if="alerts.length">
              <div v-for="alert in alerts" :key="alert.id" class="alert-item" :class="[alertLevelClass(alert.level), { 'alert-acked': alert.acknowledged }]">
                <div class="ai-top">
                  <span class="ai-level-badge" :class="`ai-${alert.level}`">{{ alert.level.toUpperCase() }}</span>
                  <span class="ai-time">{{ alert.time }}</span>
                </div>
                <div class="ai-title">{{ alert.title }}</div>
                <div class="ai-message">{{ alert.message }}</div>
                <div class="ai-bottom">
                  <span class="ai-service">{{ alert.service }}</span>
                  <button v-if="!alert.acknowledged" class="ai-ack" @click="acknowledgeAlert(alert.id)">Ack</button>
                  <span v-else class="ai-acked-label">✓ Acknowledged</span>
                </div>
              </div>
            </template>
            <div v-else class="dash-empty">No alerts in this window.</div>
          </div>
        </div>

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">📡 Recent Traces</span>
            <NuxtLink to="/traces" class="dash-section-link">View all →</NuxtLink>
          </div>
          <div class="recent-traces-list">
            <template v-if="recentTraces.length">
              <NuxtLink
                v-for="trace in recentTraces"
                :key="trace.id"
                :to="`/traces/${trace.id}`"
                class="rt-item"
              >
                <div class="rt-top">
                  <span class="rt-status-dot" :style="{ background: statusColor(trace.status) }"></span>
                  <span class="rt-name">{{ trace.rootSpan }}</span>
                  <span class="rt-dur" :class="trace.status">{{ trace.duration }}</span>
                </div>
                <div class="rt-bottom">
                  <span class="rt-svc">{{ trace.service }}</span>
                  <span class="rt-method" :class="methodClass(trace.method)">{{ trace.method }}</span>
                  <span class="rt-status-code" :class="statusCodeClass(trace.statusCode)">{{ trace.statusCode || '—' }}</span>
                  <span class="rt-time">{{ trace.time }}</span>
                </div>
              </NuxtLink>
            </template>
            <div v-else class="dash-empty">No traces yet — waiting for OTLP data on :4318.</div>
          </div>
        </div>

      </div>
    </div>
  </div>
</template>

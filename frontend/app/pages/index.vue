<script setup lang="ts">
const {
  metrics,
  throughput,
  services,
  alerts,
  unacknowledgedAlerts,
  servicesDown,
  servicesSlow,
  maxThroughput,
  acknowledgeAlert,
} = useDashboard()

const { traces } = useTracing()

const recentTraces = computed(() => traces.value.slice(0, 5))

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

function barHeight(val: number) {
  return `${(val / maxThroughput.value) * 100}%`
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
      <div style="display:flex;align-items:center;gap:6px">
        <div class="live-dot"></div>
        <span style="font-size:11px;color:var(--green);font-weight:600;">Live</span>
      </div>
    </div>

    <div class="dash-metrics-grid">
      <div v-for="m in metrics" :key="m.label" class="dash-metric-card">
        <div class="dmc-top">
          <span class="dmc-icon">{{ m.icon }}</span>
          <span class="dmc-change" :class="`dmc-${m.changeType}`">{{ m.change }}</span>
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
            <span class="dash-section-title">📡 Throughput (req/s)</span>
            <span class="dash-section-badge">24h</span>
          </div>
          <div class="throughput-chart">
            <div
              v-for="(pt, i) in throughput"
              :key="i"
              class="tp-bar"
              :style="{ height: barHeight(pt.value) }"
              :title="`${pt.time}: ${pt.value} req/s`"
            >
              <span class="tp-tooltip">{{ pt.value }}</span>
            </div>
          </div>
          <div class="tp-labels">
            <span>00:00</span>
            <span>06:00</span>
            <span>12:00</span>
            <span>18:00</span>
            <span>23:00</span>
          </div>
        </div>

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">🕸️ Service Health</span>
            <span class="dash-section-badge">{{ services.length }} services</span>
          </div>
          <div class="svc-health-list">
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
          </div>
        </div>

        <div class="dash-section">
          <div class="dash-section-header">
            <span class="dash-section-title">📡 Recent Traces</span>
            <NuxtLink to="/traces" class="dash-section-link">View all →</NuxtLink>
          </div>
          <div class="recent-traces-list">
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
                <span class="rt-status-code" :class="statusCodeClass(trace.statusCode)">{{ trace.statusCode }}</span>
                <span class="rt-time">{{ trace.time }}</span>
              </div>
            </NuxtLink>
          </div>
        </div>

      </div>
    </div>
  </div>
</template>

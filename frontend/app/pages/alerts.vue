<script setup lang="ts">
import type { ApiAlert } from '~/utils/otelTypes'

const alerts = ref<ApiAlert[]>([])
const loading = ref(false)
const { selectedService } = useServiceScope()

function alertLevelClass(level: string) {
  if (level === 'error') return 'alert-error'
  if (level === 'warning') return 'alert-warning'
  return 'alert-info'
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

function openTrace(alert: ApiAlert) {
  if (alert.traceId) navigateTo(`/traces/${alert.traceId}`)
}

async function refresh() {
  loading.value = true
  try {
    alerts.value = (await $fetch<ApiAlert[]>('/api/alerts', {
      query: { service: selectedService.value === 'all' ? undefined : selectedService.value },
    })) ?? []
  } catch (e) {
    console.error('Failed to load alerts', e)
    alerts.value = []
  } finally {
    loading.value = false
  }
}

async function acknowledge(id: string) {
  try {
    await $fetch(`/api/alerts/${encodeURIComponent(id)}/ack`, { method: 'PATCH' })
    const alert = alerts.value.find((a) => a.id === id)
    if (alert) alert.acknowledged = true
  } catch (e) {
    console.error('Failed to acknowledge alert', e)
  }
}

onMounted(refresh)
watch(selectedService, refresh)
</script>

<template>
  <div class="detail-body" style="padding:16px 20px;overflow:auto">
    <div class="dash-section-title">
      <span>🔔 Alerts</span>
      <span class="dash-alert-badge">{{ alerts.filter((a) => !a.acknowledged).length }} unacked</span>
    </div>

    <div class="alert-list" style="max-height:none;padding:0">
      <template v-if="alerts.length">
        <div
          v-for="alert in alerts"
          :key="alert.id"
          class="alert-item"
          :class="[alertLevelClass(alert.level), { 'alert-acked': alert.acknowledged }, { 'alert-linkable': alert.traceId }]"
          @click="openTrace(alert)"
        >
          <div class="ai-head">
            <span class="ai-level-badge" :class="`ai-${alert.level}`">{{ alert.level.toUpperCase() }}</span>
            <span class="ai-time">{{ relativeTime(alert.time) }}</span>
          </div>
          <div class="ai-title">{{ alert.title }}</div>
          <div class="ai-message">{{ alert.message }}</div>
          <div class="ai-foot">
            <span class="ai-service">{{ alert.service }}</span>
            <span v-if="alert.traceId" class="ai-trace-link">View trace →</span>
            <button v-if="!alert.acknowledged" class="ai-ack" @click.stop="acknowledge(alert.id)">Ack</button>
            <span v-else class="ai-acked-label">✓ Acknowledged</span>
          </div>
        </div>
      </template>
      <div v-else class="dash-empty">{{ loading ? 'Loading alerts...' : 'No alerts in this window.' }}</div>
    </div>
  </div>
</template>

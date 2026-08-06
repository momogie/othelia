<script setup lang="ts">
import type { ApiMetricName, ApiMetricPoint } from '~/utils/otelTypes'

const metricNames = ref<ApiMetricName[]>([])
const selectedMetric = ref('')
const selectedUnit = ref('')
const aggregation = ref('avg')
const activeRange = ref('1h')
const points = ref<ApiMetricPoint[]>([])
const loading = ref(false)
const { selectedService } = useServiceScope()

const ranges = ['15m', '1h', '6h', '24h']
const rangeSeconds: Record<string, number> = { '15m': 900, '1h': 3600, '6h': 21600, '24h': 86400 }
const aggregations = ['avg', 'min', 'max', 'sum', 'last']

function bucketFor(range: string): number {
  const sec = rangeSeconds[range] ?? 3600
  if (sec <= 900) return 15
  if (sec <= 3600) return 60
  if (sec <= 21600) return 300
  return 900
}

async function loadNames() {
  try {
    metricNames.value = (await $fetch<ApiMetricName[]>('/api/metrics', {
      query: { service: selectedService.value === 'all' ? undefined : selectedService.value },
    })) ?? []
    if (!selectedMetric.value && metricNames.value.length) {
      selectedMetric.value = metricNames.value[0].name
      selectedUnit.value = metricNames.value[0].unit ?? ''
    }
  } catch (e) {
    console.error('Failed to load metric names', e)
    metricNames.value = []
  }
}

async function loadSeries() {
  if (!selectedMetric.value) return
  loading.value = true
  try {
    const now = Date.now()
    const from = new Date(now - (rangeSeconds[activeRange.value] ?? 3600) * 1000).toISOString()
    const series = await $fetch<{ aggregation: string; points: ApiMetricPoint[] }>('/api/metrics/query', {
      query: {
        metric: selectedMetric.value,
        service: selectedService.value === 'all' ? undefined : selectedService.value,
        aggregation: aggregation.value,
        bucketSeconds: bucketFor(activeRange.value),
        from,
      },
    })
    points.value = series.points ?? []
  } catch (e) {
    console.error('Failed to load metric series', e)
    points.value = []
  } finally {
    loading.value = false
  }
}

function onMetricChange() {
  const meta = metricNames.value.find((m) => m.name === selectedMetric.value)
  selectedUnit.value = meta?.unit ?? ''
  loadSeries()
}

onMounted(async () => {
  await loadNames()
  loadSeries()
})

watch(selectedService, async () => {
  await loadNames()
  loadSeries()
})
</script>

<template>
  <div class="detail-body" style="padding:16px 20px;overflow:auto">
    <div class="toolbar" style="border:none;padding:0 0 12px">
      <div class="filter-chip" style="cursor:default">
        <span>Metric</span>
        <select v-model="selectedMetric" style="max-width:280px" @change="onMetricChange">
          <option v-for="m in metricNames" :key="m.name" :value="m.name">{{ m.name }}</option>
        </select>
      </div>
      <div class="filter-chip" style="cursor:default">
        <span>Agg</span>
        <select v-model="aggregation" @change="loadSeries">
          <option v-for="a in aggregations" :key="a" :value="a">{{ a }}</option>
        </select>
      </div>
      <div class="time-range-group">
        <button
          v-for="r in ranges"
          :key="r"
          class="tr-btn"
          :class="{ active: activeRange === r }"
          @click="activeRange = r; loadSeries()"
        >
          {{ r }}
        </button>
      </div>
      <div class="tb-spacer"></div>
      <span style="font-size:11px;color:var(--text3)">
        {{ metricNames.length }} metrics · {{ selectedUnit || '—' }} unit
      </span>
    </div>

    <div
      class="dash-section"
      style="padding:16px;max-width:980px"
    >
      <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
        <span style="font-size:13px;font-weight:600;color:var(--text)">{{ selectedMetric || 'Select a metric' }}</span>
        <span v-if="points.length" style="font-size:11px;color:var(--text2);font-family:var(--mono)">
          {{ aggregation }} · {{ points[points.length - 1].value.toFixed(4) }} {{ selectedUnit }}
        </span>
      </div>
      <LineChart :points="points" :height="220" :unit="selectedUnit || 'val'" color="#4a9eff" />
    </div>

    <div v-if="!metricNames.length && !loading" class="empty-state" style="padding:48px 20px;text-align:center">
      <div class="empty-icon">📈</div>
      <div class="empty-text">No OTLP metrics yet — waiting for metric export on :4318</div>
    </div>
  </div>
</template>

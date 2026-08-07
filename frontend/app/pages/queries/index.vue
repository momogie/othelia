<script setup lang="ts">
import type { ApiExpensiveQuery } from '~/utils/otelTypes'

const {
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
} = useQueries()

const activeTimeRange = ref('1h')
const expanded = ref<Record<string, boolean>>({})

const timeRanges = ['15m', '1h', '6h', '24h', '7d']
const rangeSeconds: Record<string, number> = {
  '15m': 900,
  '1h': 3600,
  '6h': 21600,
  '24h': 86400,
  '7d': 604800,
}

const sortOptions = [
  { value: 'max_ms', label: 'Max' },
  { value: 'avg_ms', label: 'Avg' },
  { value: 'executions', label: 'Executions' },
  { value: 'total_ms', label: 'Total' },
  { value: 'last_seen', label: 'Last seen' },
]

function setRange(range: string) {
  activeTimeRange.value = range
  filter.value.from = new Date(Date.now() - (rangeSeconds[range] ?? 3600) * 1000).toISOString()
  filter.value.to = null
  refresh()
}

function setSort(value: string) {
  filter.value.sort = value
  refresh()
}

let thresholdTimer: ReturnType<typeof setTimeout> | null = null
function onThresholdInput() {
  if (thresholdTimer) clearTimeout(thresholdTimer)
  thresholdTimer = setTimeout(() => refresh(), 400)
}

function toggleExpand(q: ApiExpensiveQuery) {
  expanded.value[`${q.statement}:${q.serviceName}`] = !expanded.value[`${q.statement}:${q.serviceName}`]
}

function statementLabel(q: ApiExpensiveQuery): string {
  const s = (q.summary || q.statement || '').trim()
  return s.length > 90 ? `${s.slice(0, 90)}…` : s
}

function fmtMs(ms: number): string {
  if (ms == null || Number.isNaN(ms)) return '-'
  if (ms < 1) return '<1ms'
  if (ms < 1000) return `${Math.round(ms)}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(ms < 10000 ? 2 : 1)}s`
  return `${(ms / 60000).toFixed(1)}min`
}

function fmtTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

const { selectedService } = useServiceScope()

onMounted(() => {
  filter.value.service = selectedService.value
  filter.value.from = new Date(Date.now() - 3600 * 1000).toISOString()
  loadServices()
  refresh()
})
</script>

<template>
  <div class="toolbar">
    <div class="time-range-group">
      <button
        v-for="t in timeRanges"
        :key="t"
        class="tr-btn"
        :class="{ active: activeTimeRange === t }"
        @click="setRange(t)"
      >
        {{ t }}
      </button>
    </div>
    <div class="tb-sep"></div>
    <div class="filter-chip" style="cursor:default">
      <span>Service</span>
      <select v-model="filter.service" @change="refresh">
        <option value="all">All</option>
        <option v-for="s in services" :key="s.name" :value="s.name">{{ s.name }}</option>
      </select>
    </div>
    <div class="filter-chip" style="cursor:default">
      <span>Threshold (ms)</span>
      <input
        v-model.number="filter.thresholdMs"
        type="number"
        min="0"
        step="100"
        style="width:70px;background:var(--input-bg);border:1px solid var(--border2);color:var(--text);border-radius:6px;padding:4px 6px;font-family:var(--mono)"
        @input="onThresholdInput"
      />
    </div>
    <div class="filter-chip" style="cursor:default">
      <span>Sort</span>
      <select :value="filter.sort" @change="setSort(($event.target as HTMLSelectElement).value)">
        <option v-for="o in sortOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
      </select>
    </div>
    <div class="tb-spacer"></div>
    <span style="font-size:11px;color:var(--text3)">{{ total }} expensive queries</span>
    <button class="hdr-btn" @click="refresh">⟳ Refresh</button>
  </div>

  <div class="detail-body" style="padding:12px 16px;overflow-y:auto">
    <div v-if="items.length" class="trace-overview-card" style="padding:0;overflow:hidden">
      <div class="panel-header">
        <span class="panel-title">🐢 Expensive Queries</span>
        <span class="panel-count">{{ total }}</span>
      </div>

      <div style="overflow-x:auto">
        <table style="width:100%;border-collapse:collapse;font-size:11.5px">
          <thead>
            <tr style="color:var(--text3);text-transform:uppercase;font-size:10px;letter-spacing:.5px;border-bottom:1px solid var(--border)">
              <th style="padding:8px 12px;text-align:left;min-width:300px">Query</th>
              <th style="padding:8px 6px;text-align:left">Service</th>
              <th style="padding:8px 6px;text-align:right">Exec</th>
              <th style="padding:8px 6px;text-align:right">Avg</th>
              <th style="padding:8px 6px;text-align:right">Max</th>
              <th style="padding:8px 6px;text-align:right">Total</th>
              <th style="padding:8px 12px;text-align:right">Last seen</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="q in items"
              :key="`${q.statement}:${q.serviceName}`"
              class="rule-row"
              style="border-bottom:1px solid var(--border);cursor:pointer"
              :title="q.sampleTraceId ? 'Buka trace sampel' : undefined"
              @click="q.sampleTraceId && navigateTo(`/traces/${q.sampleTraceId}`)"
            >
              <td style="padding:8px 12px">
                <div
                  style="display:flex;align-items:center;gap:6px;font-family:var(--mono);font-size:11px;color:var(--text)"
                  @click.stop="toggleExpand(q)"
                >
                  <span style="color:var(--text3);flex-shrink:0">{{ expanded[`${q.statement}:${q.serviceName}`] ? '▾' : '▸' }}</span>
                  <span class="wf-span-dot" :style="{ background: q.status === 'slow' ? 'var(--red)' : 'var(--yellow)' }"></span>
                  <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap">{{ statementLabel(q) }}</span>
                </div>
                <div
                  v-if="expanded[`${q.statement}:${q.serviceName}`]"
                  style="margin-top:6px;padding:8px 10px;background:var(--input-bg);border-radius:6px;font-family:var(--mono);font-size:10.5px;color:var(--text2);white-space:pre-wrap;word-break:break-all;max-height:220px;overflow-y:auto"
                  @click.stop
                >
                  {{ q.statement }}
                  <div v-if="q.sampleTraceId" style="margin-top:8px">
                    <NuxtLink :to="`/traces/${q.sampleTraceId}`" class="log-trace-link">→ View trace</NuxtLink>
                  </div>
                </div>
              </td>
              <td style="padding:8px 6px;white-space:nowrap">
                <span class="ti-service">{{ q.serviceName }}</span>
              </td>
              <td style="padding:8px 6px;text-align:right;font-family:var(--mono)">{{ q.executions }}</td>
              <td style="padding:8px 6px;text-align:right;font-family:var(--mono)">{{ fmtMs(q.avgMs) }}</td>
              <td style="padding:8px 6px;text-align:right;font-family:var(--mono)" :style="{ color: q.status === 'slow' ? 'var(--red)' : 'var(--yellow)' }">{{ fmtMs(q.maxMs) }}</td>
              <td style="padding:8px 6px;text-align:right;font-family:var(--mono)">{{ fmtMs(q.totalMs) }}</td>
              <td style="padding:8px 12px;text-align:right;font-family:var(--mono);white-space:nowrap;color:var(--text3)">{{ fmtTime(q.lastSeen) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="hasMore" class="load-more-bar" style="border-top:none">
        <button class="hdr-btn" :disabled="loadingMore" @click="loadMore">
          {{ loadingMore ? 'Loading...' : `Load more (${items.length}/${total})` }}
        </button>
      </div>
    </div>

    <div v-else class="empty-state" style="padding:48px 20px;text-align:center">
      <div class="empty-icon">{{ loading ? '⏳' : '🐢' }}</div>
      <div class="empty-text">
        {{
          loading
            ? 'Loading expensive queries...'
            : `No query di atas threshold ${filter.thresholdMs}ms dalam rentang waktu ini.`
        }}
      </div>
    </div>
  </div>
</template>

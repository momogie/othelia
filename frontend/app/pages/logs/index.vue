<script setup lang="ts">
const {
  logs,
  total,
  loading,
  loadingMore,
  hasMore,
  filter,
  refresh,
  loadMore,
} = useLogs()

const activeTimeRange = ref('15m')
const searchQuery = ref('')
const expanded = ref<Record<number, boolean>>({})

const timeRanges = ['5m', '15m', '1h', '6h', '24h', '7d']
const rangeSeconds: Record<string, number> = {
  '5m': 300,
  '15m': 900,
  '1h': 3600,
  '6h': 21600,
  '24h': 86400,
  '7d': 604800,
}

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    filter.value.search = searchQuery.value.trim()
    refresh()
  }, 400)
}

function setRange(range: string) {
  activeTimeRange.value = range
  filter.value.from = new Date(Date.now() - (rangeSeconds[range] ?? 900) * 1000).toISOString()
  filter.value.to = null
  refresh()
}

function toggleExpand(index: number) {
  expanded.value[index] = !expanded.value[index]
}

function fmtTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function attrEntries(attrs: Record<string, string> | null | undefined): [string, string][] {
  return Object.entries(attrs ?? {})
}

const { selectedService } = useServiceScope()

onMounted(() => {
  filter.value.service = selectedService.value
  filter.value.from = new Date(Date.now() - (rangeSeconds['15m'] ?? 900) * 1000).toISOString()
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
    <div class="search-bar">
      <span class="search-icon">🔍</span>
      <input
        v-model="searchQuery"
        placeholder="Search log body / attributes..."
        @input="onSearchInput"
      />
    </div>
    <div class="filter-chip" :class="{ active: filter.severity !== 'all' }" style="cursor:default">
      <span>Level</span>
      <select v-model="filter.severity" @change="refresh">
        <option value="all">All</option>
        <option value="error">ERROR</option>
        <option value="warn">WARN</option>
      </select>
    </div>
    <div class="tb-spacer"></div>
    <span style="font-size:11px;color:var(--text3)">{{ total }} entries</span>
  </div>

  <div class="detail-body" style="padding:12px 16px;overflow-y:auto">
    <template v-if="logs.length">
      <div v-for="(log, i) in logs" :key="i" class="log-card" :class="log.severity">
        <div class="log-head" @click="toggleExpand(i)">
          <span class="log-level" :class="`ll-${log.severity}`">{{ log.severity }}</span>
          <span class="log-time">{{ fmtTime(log.timestamp) }}</span>
          <span class="log-service">{{ log.serviceName }}</span>
          <span class="log-msg">{{ log.body || '(no message)' }}</span>
          <span class="log-expand">{{ expanded[i] ? '▾' : '▸' }}</span>
        </div>
        <div v-if="expanded[i]" class="log-detail">
          <div class="attr-section-title" style="margin-bottom:8px">Attributes</div>
          <div v-if="attrEntries(log.attributes).length" class="attr-grid">
            <div v-for="[k, v] in attrEntries(log.attributes)" :key="k" class="attr-row">
              <span class="attr-key">{{ k }}</span>
              <span class="attr-val">{{ v }}</span>
            </div>
          </div>
          <div v-else class="attr-empty">No attributes</div>
          <div v-if="log.traceId" style="margin-top:10px">
            <NuxtLink :to="`/traces/${log.traceId}`" class="log-trace-link">→ View trace</NuxtLink>
          </div>
        </div>
      </div>
      <div v-if="hasMore" class="load-more-bar" style="border-top:none">
        <button class="hdr-btn" :disabled="loadingMore" @click="loadMore">
          {{ loadingMore ? 'Loading...' : `Load more (${logs.length}/${total})` }}
        </button>
      </div>
    </template>
    <div v-else class="empty-state" style="padding:48px 20px;text-align:center">
      <div class="empty-icon">{{ loading ? '⏳' : '📝' }}</div>
      <div class="empty-text">
        {{ loading ? 'Loading logs...' : 'No logs yet — waiting for OTLP logs on :4318 (WARN+)' }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'

const {
  traces,
  total,
  filter,
  findTrace,
  refresh,
  loading,
  loadingMore,
  hasMore,
  loadMore,
} = useTracing()

const searchQuery = ref('')
const activeTimeRange = ref('15m')
const activeTraceId = ref<string | null>(null)
const showFilters = ref(false)

const timeRanges = ['5m', '15m', '1h', '6h', '24h', '7d']
const rangeSeconds: Record<string, number> = {
  '5m': 300,
  '15m': 900,
  '1h': 3600,
  '6h': 21600,
  '24h': 86400,
  '7d': 604800,
}

const activeTrace = computed<TraceVM | null>(() => {
  if (!activeTraceId.value) return null
  return traces.value.find((t) => t.id === activeTraceId.value) ?? null
})

const hasAdvancedFilter = computed(() =>
  !!filter.value.name.trim()
  || !!filter.value.path.trim()
  || !!filter.value.traceId.trim()
  || filter.value.minDurationMs != null
  || filter.value.maxDurationMs != null,
)

let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    filter.value.name = searchQuery.value.trim()
    refresh()
  }, 400)
}

let advancedTimer: ReturnType<typeof setTimeout> | null = null
function onAdvancedInput() {
  if (advancedTimer) clearTimeout(advancedTimer)
  advancedTimer = setTimeout(() => refresh(), 400)
}

function clearAdvancedFilters() {
  filter.value.name = ''
  filter.value.nameNot = false
  filter.value.path = ''
  filter.value.pathNot = false
  filter.value.traceId = ''
  filter.value.minDurationMs = null
  filter.value.maxDurationMs = null
  searchQuery.value = ''
  showFilters.value = false
  refresh()
}

function setRange(range: string) {
  activeTimeRange.value = range
  filter.value.from = new Date(Date.now() - (rangeSeconds[range] ?? 900) * 1000).toISOString()
  refresh()
}

function selectTrace(trace: TraceVM) {
  activeTraceId.value = trace.id
  findTrace(trace.id)
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
        placeholder="Search trace name..."
        @input="onSearchInput"
      />
    </div>
    <div class="filter-chip" :class="{ active: filter.status !== 'all' }" style="cursor:default">
      <span>Status</span>
      <select v-model="filter.status" @change="refresh">
        <option value="all">All</option>
        <option value="ok">OK</option>
        <option value="error">Error</option>
        <option value="slow">Slow</option>
      </select>
    </div>
    <div class="tb-sep"></div>
    <button
      class="filter-chip"
      :class="{ active: showFilters || hasAdvancedFilter }"
      style="cursor:pointer"
      @click="showFilters = !showFilters"
    >
      ⚙ Filters
    </button>
    <div class="tb-spacer"></div>
    <span style="font-size:11px;color:var(--text3)">{{ total }} traces</span>
  </div>

  <div v-if="showFilters" class="filter-panel">
    <div class="fp-group">
      <label class="fp-label">Name</label>
      <div class="fp-op-row">
        <select v-model="filter.nameNot" class="fp-op" @change="onAdvancedInput">
          <option :value="false">Contains</option>
          <option :value="true">Not contains</option>
        </select>
        <input
          v-model="filter.name"
          class="text-input"
          type="text"
          placeholder="e.g. update"
          @input="onAdvancedInput"
        >
      </div>
    </div>
    <div class="fp-group">
      <label class="fp-label">Path</label>
      <div class="fp-op-row">
        <select v-model="filter.pathNot" class="fp-op" @change="onAdvancedInput">
          <option :value="false">Contains</option>
          <option :value="true">Not contains</option>
        </select>
        <input
          v-model="filter.path"
          class="text-input"
          type="text"
          placeholder="e.g. /api/Main/GetList"
          @input="onAdvancedInput"
        >
      </div>
    </div>
    <div class="fp-group">
      <label class="fp-label">Trace ID</label>
      <input
        v-model="filter.traceId"
        class="text-input"
        type="text"
        placeholder="e.g. 512c70c55dd74513"
        @input="onAdvancedInput"
      >
    </div>
    <div class="fp-group">
      <label class="fp-label">Min duration (ms)</label>
      <input
        v-model.number="filter.minDurationMs"
        class="text-input"
        type="number"
        min="0"
        placeholder="0"
        @change="refresh"
      >
    </div>
    <div class="fp-group">
      <label class="fp-label">Max duration (ms)</label>
      <input
        v-model.number="filter.maxDurationMs"
        class="text-input"
        type="number"
        min="0"
        placeholder="—"
        @change="refresh"
      >
    </div>
    <div class="fp-actions">
      <button
        v-if="hasAdvancedFilter"
        class="hdr-btn"
        @click="clearAdvancedFilters"
      >
        ✕ Clear
      </button>
    </div>
  </div>

  <div class="content">
    <div class="trace-list-panel">
      <div class="panel-header">
        <span class="panel-title">Traces</span>
        <span class="panel-count">{{ total }}</span>
      </div>
      <div class="trace-list">
        <template v-if="traces.length">
          <TraceListItem
            v-for="trace in traces"
            :key="trace.id"
            :trace="trace"
            :active="activeTraceId === trace.id"
            @select="selectTrace"
          />
        </template>
        <div v-else class="empty-state" style="padding:40px 20px;text-align:center">
          <div class="empty-icon">{{ loading ? '⏳' : '📡' }}</div>
          <div class="empty-text">
            {{ loading ? 'Loading traces...' : 'No traces found for the current filters' }}
          </div>
        </div>
      </div>
      <div v-if="hasMore" class="load-more-bar">
        <button class="hdr-btn" :disabled="loadingMore" @click="loadMore">
          {{ loadingMore ? 'Loading...' : `Load more (${traces.length}/${total})` }}
        </button>
      </div>
    </div>

    <TraceDetail v-if="activeTrace" :key="activeTrace.id" :trace="activeTrace" />

    <div v-else class="trace-detail">
      <div class="empty-state">
        <div class="empty-icon">🔭</div>
        <div class="empty-text">Select a trace to inspect</div>
      </div>
    </div>
  </div>
</template>

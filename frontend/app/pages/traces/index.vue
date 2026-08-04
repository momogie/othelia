<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'

const { traces, serviceList } = useTracing()

const searchQuery = ref('')
const filterStatus = ref('all')
const filterService = ref('all')
const activeTimeRange = ref('15m')
const activeTraceId = ref<string | null>(null)

const timeRanges = ['5m', '15m', '1h', '6h', '24h', '7d']

const activeTrace = computed<TraceVM | null>(() => {
  if (!activeTraceId.value) return null
  return traces.value.find((t) => t.id === activeTraceId.value) ?? null
})

const filteredTraces = computed(() =>
  traces.value.filter((t) => {
    if (filterStatus.value !== 'all' && t.status !== filterStatus.value) return false
    if (filterService.value !== 'all' && t.service !== filterService.value) return false
    if (searchQuery.value) {
      const q = searchQuery.value.toLowerCase()
      return (
        t.rootSpan.toLowerCase().includes(q)
        || t.id.toLowerCase().includes(q)
        || t.path.toLowerCase().includes(q)
        || t.service.toLowerCase().includes(q)
      )
    }
    return true
  }),
)

function selectTrace(trace: TraceVM) {
  activeTraceId.value = trace.id
}
</script>

<template>
  <div class="toolbar">
    <div class="time-range-group">
      <button
        v-for="t in timeRanges"
        :key="t"
        class="tr-btn"
        :class="{ active: activeTimeRange === t }"
        @click="activeTimeRange = t"
      >
        {{ t }}
      </button>
    </div>
    <div class="tb-sep"></div>
    <div class="search-bar">
      <span class="search-icon">🔍</span>
      <input v-model="searchQuery" placeholder="Search service, trace ID, endpoint..." />
    </div>
    <div class="filter-chip" :class="{ active: filterStatus !== 'all' }" style="cursor:default">
      <span>Status</span>
      <select v-model="filterStatus">
        <option value="all">All</option>
        <option value="ok">OK</option>
        <option value="error">Error</option>
        <option value="slow">Slow</option>
      </select>
    </div>
    <div class="filter-chip" :class="{ active: filterService !== 'all' }" style="cursor:default">
      <span>Service</span>
      <select v-model="filterService">
        <option value="all">All</option>
        <option v-for="s in serviceList" :key="s" :value="s">{{ s }}</option>
      </select>
    </div>
    <div class="tb-spacer"></div>
    <span style="font-size:11px;color:var(--text3)">{{ filteredTraces.length }} traces</span>
  </div>

  <div class="content">
    <div class="trace-list-panel">
      <div class="panel-header">
        <span class="panel-title">Traces</span>
        <span class="panel-count">{{ filteredTraces.length }}</span>
      </div>
      <div class="trace-list">
        <TraceListItem
          v-for="trace in filteredTraces"
          :key="trace.id"
          :trace="trace"
          :active="activeTraceId === trace.id"
          @select="selectTrace"
        />
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

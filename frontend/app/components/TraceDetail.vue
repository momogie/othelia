<script setup lang="ts">
import type { ApiLog } from '~/utils/otelTypes'
import type { TraceVM } from '~/utils/mockData'

const props = defineProps<{ trace: TraceVM }>()

const activeTab = ref('waterfall')
const hoveredSvc = ref<string | null>(null)
const traceLogs = ref<ApiLog[]>([])
const logsLoading = ref(false)

const detailTabs = [
  { id: 'waterfall', label: '🌊 Waterfall' },
  { id: 'logs', label: '📋 Logs' },
  { id: 'events', label: '⚡ Events' },
  { id: 'attributes', label: '🔖 Attributes' },
]

const statusTag = computed(() => {
  if (props.trace.status === 'error') return 'tag-error'
  if (props.trace.status === 'slow') return 'tag-warn'
  return 'tag-ok'
})

async function loadTraceLogs() {
  if (traceLogs.value.length || logsLoading.value) return
  logsLoading.value = true
  try {
    traceLogs.value = (await $fetch<ApiLog[]>(`/api/logs`, {
      query: { traceId: props.trace.id, limit: 100 },
    })) ?? []
  } catch (e) {
    console.error('Failed to load trace logs', e)
    traceLogs.value = []
  } finally {
    logsLoading.value = false
  }
}

watch(activeTab, (tab) => {
  if (tab === 'logs') loadTraceLogs()
})

function fmtLogTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString([], { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

const traceLinks = computed(() => props.trace.links ?? [])
</script>

<template>
  <div class="trace-detail">
    <div class="detail-tabs">
      <div
        v-for="t in detailTabs"
        :key="t.id"
        class="detail-tab"
        :class="{ active: activeTab === t.id }"
        @click="activeTab = t.id"
      >
        {{ t.label }}
      </div>
    </div>

    <div class="detail-body">
      <template v-if="activeTab === 'waterfall'">
        <div class="trace-overview-card">
          <div class="toc-header">
            <span class="toc-name">{{ trace.rootSpan }}</span>
            <span class="tag" :class="statusTag">{{ trace.status.toUpperCase() }}</span>
            <span class="toc-id">{{ trace.id }}</span>
          </div>
          <div class="toc-metrics">
            <div class="toc-metric">
              <span class="toc-metric-label">Duration</span>
              <span
                class="toc-metric-value"
                :style="{ color: trace.status === 'error' ? 'var(--red)' : trace.status === 'slow' ? 'var(--yellow)' : 'var(--green)' }"
              >
                {{ trace.duration }}
              </span>
              <span class="toc-metric-sub">Total trace time</span>
            </div>
            <div class="toc-metric">
              <span class="toc-metric-label">Spans</span>
              <span class="toc-metric-value" style="color:var(--blue)">{{ trace.spans }}</span>
              <span class="toc-metric-sub">{{ trace.errorSpans || 0 }} errors</span>
            </div>
            <div class="toc-metric">
              <span class="toc-metric-label">Services</span>
              <span class="toc-metric-value" style="color:var(--purple)">{{ trace.serviceCount || 1 }}</span>
              <span class="toc-metric-sub">Involved</span>
            </div>
            <div class="toc-metric">
              <span class="toc-metric-label">Status</span>
              <span class="toc-metric-value" style="font-size:14px">{{ trace.statusCode }}</span>
              <span class="toc-metric-sub">HTTP Status</span>
            </div>
          </div>
        </div>

        <div class="svc-map">
          <div class="svc-map-title">📡 Service Dependency</div>
          <div class="svc-nodes">
            <template v-for="(node, idx) in trace.serviceNodes" :key="node.name">
              <div
                class="svc-node"
                :class="{ active: hoveredSvc === node.name }"
                @mouseenter="hoveredSvc = node.name"
                @mouseleave="hoveredSvc = null"
              >
                <span class="svc-icon">{{ node.icon }}</span>
                <span class="svc-name">{{ node.name }}</span>
                <span class="svc-latency">{{ node.latency }}</span>
              </div>
              <span v-if="idx < trace.serviceNodes.length - 1" class="svc-arrow">→</span>
            </template>
          </div>
        </div>

        <TraceWaterfall :trace="trace" />
      </template>

      <template v-if="activeTab === 'logs'">
        <div class="attr-section-title" style="margin-bottom:10px">Trace Logs</div>
        <template v-if="traceLogs.length">
          <div v-for="(log, i) in traceLogs" :key="i" class="log-card" :class="log.severity">
            <div class="log-head">
              <span class="log-level" :class="`ll-${log.severity}`">{{ log.severity }}</span>
              <span class="log-time">{{ fmtLogTime(log.timestamp) }}</span>
              <span class="log-service">{{ log.serviceName }}</span>
              <span class="log-msg">{{ log.body || '(no message)' }}</span>
            </div>
          </div>
        </template>
        <div v-else class="attr-empty" style="padding:20px 0">
          {{ logsLoading ? 'Loading logs...' : 'No logs recorded for this trace' }}
        </div>
      </template>

      <template v-if="activeTab === 'events'">
        <div class="trace-overview-card">
          <div class="attr-section-title" style="margin-bottom:10px">Span Events</div>
          <div v-for="ev in trace.events" :key="ev.id" class="event-row">
            <div class="ev-icon" :style="{ background: `${ev.color}18` }">{{ ev.icon }}</div>
            <div style="flex:1">
              <div class="ev-name">{{ ev.name }}</div>
              <div class="ev-attrs">{{ ev.attrs }}</div>
            </div>
            <div class="ev-time">+{{ ev.offset }}</div>
          </div>
          <div v-if="!trace.events.length" class="attr-empty">No events</div>
        </div>
        <div class="trace-overview-card" style="margin-top:12px">
          <div class="attr-section-title" style="margin-bottom:10px">Span Links</div>
          <div v-for="link in traceLinks" :key="`${link.traceId}:${link.spanId}`" class="event-row">
            <div class="ev-icon" style="background:rgba(124,92,191,0.15)">🔗</div>
            <div style="flex:1">
              <NuxtLink :to="`/traces/${link.traceId}`" class="log-trace-link">
                Trace {{ link.traceId }}
              </NuxtLink>
              <div v-if="link.attrs.length" class="ev-attrs">
                {{ link.attrs.map((a) => `${a.key}=${a.val}`).join(' · ') }}
              </div>
            </div>
          </div>
          <div v-if="!traceLinks.length" class="attr-empty">No links</div>
        </div>
      </template>

      <template v-if="activeTab === 'attributes'">
        <SpanAttributes title="HTTP Attributes" :items="trace.httpAttrs" />
        <SpanAttributes title="Resource Attributes" :items="trace.resourceAttrs" />
      </template>
    </div>
  </div>
</template>

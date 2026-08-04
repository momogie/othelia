<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'

const props = defineProps<{ trace: TraceVM }>()

const activeTab = ref('waterfall')
const hoveredSvc = ref<string | null>(null)

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
              <span class="toc-metric-value" style="color:var(--purple)">{{ trace.serviceCount || 3 }}</span>
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
        <div v-for="log in trace.logs" :key="log.id" class="log-entry" :class="log.level">
          <span class="log-level" :class="`ll-${log.level}`">{{ log.level }}</span>
          <span class="log-time">{{ log.time }}</span>
          <span class="log-msg">{{ log.msg }}</span>
          <span class="log-span">{{ log.span }}</span>
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
        </div>
      </template>

      <template v-if="activeTab === 'attributes'">
        <SpanAttributes title="HTTP Attributes" :items="trace.httpAttrs" />
        <SpanAttributes title="Resource Attributes" :items="trace.resourceAttrs" />
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { SpanVM, TraceVM } from '~/utils/mockData'
import { halfDur } from '~/utils/traceFormat'

defineProps<{ trace: TraceVM }>()
const activeSpan = ref<SpanVM | null>(null)

function selectSpan(span: SpanVM) {
  activeSpan.value = activeSpan.value?.id === span.id ? null : span
}
</script>

<template>
  <div class="waterfall-container">
    <div class="wf-header-row">
      <div class="wf-name-col">Span Name</div>
      <div class="wf-bar-col">
        <div class="wf-timeline">
          <span class="wf-tick">0ms</span>
          <span class="wf-tick" style="text-align:center">{{ halfDur(trace.duration) }}</span>
          <span class="wf-tick" style="text-align:right">{{ trace.duration }}</span>
        </div>
      </div>
      <div style="width:52px; font-size:10.5px; color:var(--text3); text-align:right">Duration</div>
    </div>

    <div
      v-for="span in trace.spanTree"
      :key="span.id"
      class="wf-row"
      :class="{ active: activeSpan && activeSpan.id === span.id, 'error-row': span.status === 'error' }"
      @click="selectSpan(span)"
    >
      <div class="wf-name-cell">
        <div class="wf-indent" :style="{ width: `${span.depth * 16}px` }"></div>
        <div class="wf-span-dot" :style="{ background: span.color }"></div>
        <span class="wf-span-name" :title="span.name">{{ span.name }}</span>
        <span class="wf-span-svc">{{ span.service }}</span>
      </div>
      <div class="wf-bar-cell">
        <div
          class="wf-bar"
          :style="{ left: `${span.offsetPct}%`, width: `${Math.max(span.widthPct, 0.5)}%`, background: `${span.color}bb` }"
        >
          <span class="wf-bar-label">{{ span.duration }}</span>
        </div>
      </div>
      <div class="wf-dur">{{ span.duration }}</div>
    </div>
  </div>

  <template v-if="activeSpan">
    <div class="trace-overview-card">
      <div class="toc-header">
        <span class="toc-name" style="font-size:13px">{{ activeSpan.name }}</span>
        <span class="tag" :class="activeSpan.status === 'error' ? 'tag-error' : 'tag-ok'">{{ activeSpan.status.toUpperCase() }}</span>
      </div>
      <SpanAttributes title="Span Attributes" :items="activeSpan.attrs" />
    </div>
  </template>
</template>

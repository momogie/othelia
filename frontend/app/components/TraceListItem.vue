<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'
import { statusCodeClass, statusColor } from '~/utils/traceFormat'

defineProps<{ trace: TraceVM; active: boolean }>()
const emit = defineEmits<{ select: [trace: TraceVM] }>()
</script>

<template>
  <div
    class="trace-item"
    :class="{ active }"
    @click="emit('select', trace)"
  >
    <div class="ti-top">
      <div class="ti-status" :style="{ background: statusColor(trace.status) }"></div>
      <span class="ti-name">{{ trace.rootSpan }}</span>
      <span class="ti-dur" :class="trace.status">{{ trace.duration }}</span>
    </div>
    <div class="ti-mid">
      <span class="ti-service">{{ trace.service }}</span>
      <span class="ti-method" :class="`method-${trace.method}`">{{ trace.method }}</span>
      <span class="ti-status-code" :class="statusCodeClass(trace.statusCode)">{{ trace.statusCode }}</span>
      <span class="ti-path">{{ trace.path }}</span>
    </div>
    <div class="ti-bot">
      <span class="ti-spans">{{ trace.spans }} spans</span>
      <span class="ti-time">{{ trace.time }}</span>
    </div>
    <div class="ti-waterfall">
      <div
        class="ti-wf-bar"
        :style="{ width: `${trace.wfWidth}%`, background: `${statusColor(trace.status)}88` }"
      ></div>
    </div>
  </div>
</template>

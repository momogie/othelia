<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'

const route = useRoute()
const { findTrace } = useTracing()

const traceId = computed(() => String(route.params.id ?? ''))
const trace = computed<TraceVM | null>(() => findTrace(traceId.value))

watch(traceId, () => {
  if (!trace.value) {
    navigateTo('/traces')
  }
}, { immediate: true })
</script>

<template>
  <div class="content">
    <TraceDetail v-if="trace" :key="trace.id" :trace="trace" />

    <div v-else class="trace-detail">
      <div class="empty-state">
        <div class="empty-icon">🔭</div>
        <div class="empty-text">Trace not found — <NuxtLink to="/traces">back to list</NuxtLink></div>
      </div>
    </div>
  </div>
</template>

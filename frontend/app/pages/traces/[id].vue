<script setup lang="ts">
import type { TraceVM } from '~/utils/mockData'

const route = useRoute()
const { findTrace } = useTracing()

const trace = ref<TraceVM | null>(null)

watch(
  () => route.params.id,
  async (id) => {
    trace.value = null
    const t = await findTrace(String(id))
    if (t) trace.value = t
    else navigateTo('/traces')
  },
  { immediate: true },
)
</script>

<template>
  <div class="content">
    <TraceDetail v-if="trace" :key="trace.id" :trace="trace" />

    <div v-else class="trace-detail">
      <div class="empty-state">
        <div class="empty-icon">🔭</div>
        <div class="empty-text">Loading trace…</div>
      </div>
    </div>
  </div>
</template>
